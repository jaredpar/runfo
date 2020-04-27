#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Util.Triage
{
    public sealed class AutoTriageUtil
    {
        public DevOpsServer Server { get; }
        public GitHubClient GitHubClient { get; }
        public DotNetQueryUtil QueryUtil { get; }

        public TriageContextUtil TriageContextUtil { get; }

        public ReportBuilder ReportBuilder { get; } = new ReportBuilder();

        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public AutoTriageUtil(
            DevOpsServer server,
            GitHubClient gitHubClient,
            TriageContext context,
            ILogger logger)
        {
            Server = server;
            GitHubClient = gitHubClient;
            QueryUtil = new DotNetQueryUtil(server);
            TriageContextUtil = new TriageContextUtil(context);
            Logger = logger;
        }

        // TODO: don't do this if the issue is closed
        // TODO: limit builds to report on to 100 because after that the tables get too large

        // TODO: eventually this won't be necessary
        public void EnsureTriageIssues()
        {
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "unable to load shared library 'advapi32.dll' or one of its dependencies",
                Create("dotnet", "core-eng", 9635));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "HTTP request to.*api.nuget.org.*timed out",
                Create("dotnet", "core-eng", 9635),
                Create("dotnet", "runtime", 35074));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "Failed to install dotnet",
                Create("dotnet", "runtime", 34015));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "Notification of assignment to an agent was never received",
                Create("dotnet", "runtime", 35223));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "Received request to deprovision: The request was cancelled by the remote provider",
                Create("dotnet", "runtime", 34472),
                Create("dotnet", "core-eng", 9532));

            static ModelTriageGitHubIssue Create(string organization, string repository, int number) =>
                new ModelTriageGitHubIssue()
                {
                    Organization = organization,
                    Repository = repository, 
                    IssueNumber = number
                };
        }

        public async Task Triage(string projectName, int buildNumber)
        {
            var build = await Server.GetBuildAsync(projectName, buildNumber).ConfigureAwait(false);
            await Triage(build).ConfigureAwait(false);
        }

        public async Task Triage(string buildQuery)
        {
            foreach (var build in await QueryUtil.ListBuildsAsync(buildQuery))
            {
                await Triage(build).ConfigureAwait(false);
            }
        }

        // TODO: need overload that takes builds and groups up the issue and PR updates
        // or maybe just make that a separate operation from triage
        public async Task Triage(Build build)
        {
            Logger.LogInformation($"Triaging {DevOpsUtil.GetBuildUri(build)}");

            var buildInfo = build.GetBuildInfo();
            var modelBuild = TriageContextUtil.EnsureBuild(buildInfo);

            Timeline? timeline = null;
            try
            {
                timeline = await Server.GetTimelineAsync(build);
                if (timeline is null)
                {
                    Logger.LogWarning("No timeline");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting timeline: {ex.Message}");
            }

            foreach (var modelTriageIssue in Context.ModelTriageIssues)
            {
                switch (modelTriageIssue.SearchKind)
                {
                    case SearchKind.SearchTimeline:
                        if (timeline is object)
                        {
                            DoSearchTimeline(modelTriageIssue, build, modelBuild, timeline);
                        }
                        break;
                    default:
                        Logger.LogWarning($"Unknown search kind {modelTriageIssue.SearchKind} in {modelTriageIssue.Id}");
                        break;
                }
            }
        }

        private void DoSearchTimeline(
            ModelTriageIssue modelTriageIssue,
            Build build,
            ModelBuild modelBuild,
            Timeline timeline)
        {
            var searchText = modelTriageIssue.SearchText;
            Logger.LogInformation($@"Text: ""{searchText}""");
            if (TriageContextUtil.IsProcessed(modelTriageIssue, modelBuild))
            {
                Logger.LogInformation($@"Skipping");
                return;
            }

            var count = 0;
            foreach (var result in QueryUtil.SearchTimeline(build, timeline, text: searchText))
            {
                count++;

                var modelTriageIssueResult = new ModelTriageIssueResult()
                {
                    TimelineRecordName = result.TimelineRecord.Name,
                    // TODO: uncomment when this is available
                    // JobName = result.JobRecord.Name,
                    Line = result.Line,
                    ModelBuild = modelBuild,
                    ModelTriageIssue = modelTriageIssue,
                    BuildNumber = result.Build.GetBuildKey().Number,
                };
                Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = modelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            try
            {
                Logger.LogInformation($@"Saving {count} jobs");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save timeline complete: {ex.Message}");
            }
        }

        public async Task UpdateGithubIssues()
        {
            foreach (var modelTriageIssue in Context.ModelTriageIssues)
            {
                switch (modelTriageIssue.SearchKind)
                {
                    case SearchKind.SearchTimeline:
                        await UpdateIssuesForSearchTimeline(modelTriageIssue);
                        break;
                    default:
                        Logger.LogWarning($"Unknown search kind {modelTriageIssue.SearchKind} in {modelTriageIssue.Id}");
                        break;
                }
            }

            async Task UpdateIssuesForSearchTimeline(ModelTriageIssue triageIssue)
            {
                foreach (var gitHubIssue in triageIssue.ModelTriageGitHubIssues.ToList())
                {
                    await UpdateIssueForSearchTimeline(triageIssue, gitHubIssue);
                }
            }

            async Task UpdateIssueForSearchTimeline(ModelTriageIssue triageIssue, ModelTriageGitHubIssue gitHubIssue)
            {
                var results = Context.ModelTriageIssueResults
                    .Include(x => x.ModelBuild)
                    .ThenInclude(b => b.ModelBuildDefinition)
                    .Where(x => x.ModelTriageIssueId == triageIssue.Id)
                    .OrderByDescending(x => x.BuildNumber)
                    .ToList();

                var footer = new StringBuilder();
                var mostRecent = results
                    .Select(x => x.ModelBuild)
                    .OrderByDescending(x => x.StartTime)
                    .FirstOrDefault();
                if (mostRecent is object)
                {
                    Debug.Assert(mostRecent.StartTime.HasValue);
                    var buildKey = TriageContextUtil.GetBuildKey(mostRecent);
                    footer.AppendLine($"Most [recent]({buildKey.BuildUri}) failure {mostRecent.StartTime.Value.ToLocalTime()}");
                }

                const int limit = 100;
                if (results.Count > limit)
                {
                    footer.AppendLine($"Limited to {limit} items (removed {results.Count - limit})");
                    results = results.Take(limit).ToList();
                }
                
                // TODO: we use same Server here even if the Organization setting in the 
                // item specifies a different organization. Need to replace Server with 
                // a map from org -> DevOpsServer
                var searchResults = results
                    .Select(x => (TriageContextUtil.GetBuildInfo(x.ModelBuild), x.TimelineRecordName));
                var reportBody = ReportBuilder.BuildSearchTimeline(
                    searchResults,
                    markdown: true,
                    includeDefinition: true,
                    footer.ToString());

                var succeeded = await UpdateGitHubIssueReport(gitHubIssue.IssueKey, reportBody);
                Logger.LogInformation($"Updated {gitHubIssue.IssueKey.IssueUri}");
            }
        }

        private async Task<bool> UpdateGitHubIssueReport(GitHubIssueKey issueKey, string reportBody)
        {
            try
            {
                var issueClient = GitHubClient.Issue;
                var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Number);
                if (TryUpdateIssueText(reportBody, issue.Body, out var newIssueBody))
                {
                    var issueUpdate = issue.ToUpdate();
                    issueUpdate.Body = newIssueBody;
                    await issueClient.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, issueUpdate);
                    return true;
                }
                else
                {
                    Logger.LogInformation("Cannot find the replacement section in the issue");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating issue {issueKey}: {ex.Message}");
            }

            return false;
        }

        private static bool TryUpdateIssueText(string reportBody, string oldIssueText, [NotNullWhen(true)] out string? newIssueText)
        {
            var builder = new StringBuilder();
            var inReportBody = false;
            var foundEnd = false;
            using var reader = new StringReader(oldIssueText);
            do
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (inReportBody)
                {
                    // Skip until we hit the end of the existing report
                    if (ReportBuilder.MarkdownReportEndRegex.IsMatch(line))
                    {
                        inReportBody = false;
                        foundEnd = true;
                    }
                }
                else if (ReportBuilder.MarkdownReportStartRegex.IsMatch(line))
                {
                    builder.AppendLine(reportBody);
                    inReportBody = true;
                }
                else
                {
                    builder.AppendLine(line);
                }
            } while (true);

            if (foundEnd)
            {
                newIssueText = builder.ToString();
                return true;
            }
            else
            {
                newIssueText = null;
                return false;
            }

        }

        public async Task UpdateStatusIssue()
        {
            var header = new StringBuilder();
            var body = new StringBuilder();
            var footer = new StringBuilder();
            header.AppendLine("## Overview");
            header.AppendLine("Please use this queries to discover issues");

            await BuildOne("Blocking CI", "blocking-clean-ci", DotNetUtil.GetBuildDefinitionKeyFromFriendlyName("runtime"));
            await BuildOne("Blocking Official Build", "blocking-official-build", DotNetUtil.GetBuildDefinitionKeyFromFriendlyName("runtime-official"));
            await BuildOne("Blocking CI Optional", "blocking-clean-ci-optional", DotNetUtil.GetBuildDefinitionKeyFromFriendlyName("runtime"));
            await BuildOne("Blocking Outerloop", "blocking-outerloop", null);

            // Blank line to move past the table 
            header.AppendLine("");
            BuildFooter();

            await UpdateIssue();

            void BuildFooter()
            {
                footer.AppendLine(@"## Goals

1. A minimum 95% passing rate for the `runtime` pipeline

## Resources

1. [runtime pipeline analytics](https://dnceng.visualstudio.com/public/_build?definitionId=686&view=ms.vss-pipelineanalytics-web.new-build-definition-pipeline-analytics-view-cardmetrics)");

            }

            async Task BuildOne(string title, string label, BuildDefinitionKey? definitionKey)
            {
                header.AppendLine($"- [{title}](https://github.com/dotnet/runtime/issues?q=is%3Aopen+is%3Aissue+label%3A{label})");

                body.AppendLine($"## {title}");
                body.AppendLine("|Status|Issue|Build Count|");
                body.AppendLine("|---|---|---|");

                var query = (await DoSearch(label))
                    .Select(x => 
                        {
                            var issueKey = x.GetIssueKey();
                            var count = definitionKey.HasValue
                                ? GetImpactedBuildsCount(issueKey, definitionKey.Value)
                                : null;

                            return (x, Count: count);
                        })
                    .OrderByDescending(x => x.Count);
                foreach (var (issue, count) in query)
                {
                    var emoji = issue.Labels.Any(x => x.Name == "intermittent")
                        ? ":warning:"
                        : ":fire:";
                    var titleLimit = 75;
                    var issueText = issue.Title.Length >= titleLimit
                        ? issue.Title.Substring(0, titleLimit - 5) + " ..."
                        : issue.Title;
                    var issueEntry = $"[{issueText}]({issue.HtmlUrl})";
                    var countStr = count.HasValue ? count.ToString() : "N/A";

                    body.AppendLine($"|{emoji}|{issueEntry}|{countStr}|");
                }
            }

            async Task<List<Octokit.Issue>> DoSearch(string label)
            {
                var request = new SearchIssuesRequest()
                {
                    Labels = new [] { label },
                    State = ItemState.Open,
                    Type = IssueTypeQualifier.Issue,
                    Repos = { { "dotnet", "runtime" } },
                };
                var result = await GitHubClient.Search.SearchIssues(request);
                return result.Items.ToList();
            }

            async Task UpdateIssue()
            {
                var issueKey = new GitHubIssueKey("dotnet", "runtime", 702);
                var issueClient = GitHubClient.Issue;
                var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Number);
                var updateIssue = issue.ToUpdate();
                updateIssue.Body = header.ToString() + body.ToString() + footer.ToString();
                await GitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, updateIssue);
            }

            int? GetImpactedBuildsCount(GitHubIssueKey issueKey, BuildDefinitionKey definitionKey)
            {
                if (!TriageContextUtil.TryGetTriageIssue(issueKey, out var triageIssue))
                {
                    return null;
                }

                // TODO: need to be able to filter to the repo the build ran against
                var count = Context.ModelTriageIssueResults
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition)
                    .Where(x =>
                        x.ModelTriageIssueId == triageIssue.Id &&
                        x.ModelBuild.ModelBuildDefinition.AzureOrganization == definitionKey.Organization &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == definitionKey.Project &&
                        x.ModelBuild.ModelBuildDefinition.DefinitionId == definitionKey.Id)
                    .Count();
                return count;;
            }
        }
    }
}
