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
using Octokit;

namespace DevOps.Util.Triage
{
    public sealed class AutoTriageUtil
    {
        public DevOpsServer Server { get; }
        public GitHubClient GitHubClient { get; }
        public DotNetQueryUtil QueryUtil { get; }

        public TriageUtil TriageUtil { get; }

        public ReportBuilder ReportBuilder { get; } = new ReportBuilder();

        public TriageDbContext Context => TriageUtil.Context;

        public AutoTriageUtil(
            DevOpsServer server,
            GitHubClient gitHubClient,
            TriageDbContext context)
        {
            Server = server;
            GitHubClient = gitHubClient;
            QueryUtil = new DotNetQueryUtil(server);
            TriageUtil = new TriageUtil(context);
        }

        // TODO: don't do this if the issue is closed
        // TODO: limit builds to report on to 100 because after that the tables get too large

        // TODO: eventually this won't be necessary
        public void EnsureTriageIssues()
        {
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "core-eng", 9635),
                text: "unable to load shared library 'advapi32.dll' or one of its dependencies");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "core-eng", 9634),
                text: "HTTP request to.*api.nuget.org.*timed out");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "runtime", 35223),
                text: "Notification of assignment to an agent was never received");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "runtime", 35074),
                text: "HTTP request to.*api.nuget.org.*timed out");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "runtime", 34015),
                text: "Failed to install dotnet");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "runtime", 34015),
                text: "Failed to install dotnet");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "runtime", 34472),
                text: "Received request to deprovision: The request was cancelled by the remote provider");
            TriageUtil.TryCreateTimelineQuery(
                IssueKind.Infra,
                new GitHubIssueKey("dotnet", "core-eng", 34472),
                text: "Received request to deprovision: The request was cancelled by the remote provider");
        }

        public async Task Triage(string buildQuery)
        {
            foreach (var build in await QueryUtil.ListBuildsAsync(buildQuery))
            {
                await Triage(build);
            }
        }

        // TODO: need overload that takes builds and groups up the issue and PR updates
        // or maybe just make that a separate operation from triage
        public async Task Triage(Build build)
        {
            await DoSearchTimeline(build, TriageUtil.Context.ModelTimelineQueries);
            // TODO: update GitHub issues
            // TODO: update PRs
            // TODO: update the processed build table? At least the caller needs to be concerned
            // with that
        }

        private async Task DoSearchTimeline(Build build, IEnumerable<ModelTimelineQuery> timelineQueries)
        {
            Console.WriteLine($"Searching {DevOpsUtil.GetBuildUri(build)}");

            var buildKey = build.GetBuildKey();
            var timeline = await Server.GetTimelineAsync(build);
            if (timeline is null)
            {
                Console.WriteLine("Error: No timeline");
                return;
            }

            var buildInfo = build.GetBuildInfo();
            foreach (var timelineQuery in timelineQueries)
            {
                Console.Write($@"  Text: ""{timelineQuery.SearchText}"" ... ");
                if (TriageUtil.IsProcessed(timelineQuery, buildInfo))
                {
                    Console.WriteLine("skipping");
                    continue;
                }

                var count = 0;
                foreach (var result in QueryUtil.SearchTimeline(build, timeline, text: timelineQuery.SearchText))
                {
                    count++;
                    TriageUtil.CreateTimelineItem(timelineQuery, result);
                }
                Console.WriteLine($"{count} jobs");
            }
        }

        public async Task UpdateQueryIssues()
        {
            await UpdateIssuesForTimelineQueries();

            async Task UpdateIssuesForTimelineQueries()
            {
                foreach (var timelineQuery in Context.ModelTimelineQueries)
                {
                    // TODO: don't update closed issues
                    await UpdateIssueForTimelineQuery(timelineQuery);
                }
            }

            async Task UpdateIssueForTimelineQuery(ModelTimelineQuery timelineQuery)
            {
                var timelineItems = Context.ModelTimelineItems
                    .Include(x => x.ModelBuild)
                    .ThenInclude(b => b.ModelBuildDefinition)
                    .Where(x => x.ModelTimelineQueryId == timelineQuery.Id)
                    .OrderByDescending(x => x.BuildNumber)
                    .ToList();

                var footer = new StringBuilder();
                var mostRecent = timelineItems
                    .Select(x => x.ModelBuild)
                    .OrderByDescending(x => x.StartTime)
                    .FirstOrDefault();
                if (mostRecent is object)
                {
                    Debug.Assert(mostRecent.StartTime.HasValue);
                    var buildKey = TriageUtil.GetBuildKey(mostRecent);
                    footer.AppendLine($"Most [recent]({buildKey.BuildUri}) failure {mostRecent.StartTime.Value.ToLocalTime()}");
                }

                const int limit = 100;
                if (timelineItems.Count > limit)
                {
                    footer.AppendLine($"Limited to {limit} items (removed {timelineItems.Count - limit})");
                    timelineItems = timelineItems.Take(limit).ToList();
                }
                
                // TODO: we use same Server here even if the Organization setting in the 
                // item specifies a different organization. Need to replace Server with 
                // a map from org -> DevOpsServer
                var results = timelineItems
                    .Select(x => (TriageUtil.GetBuildInfo(x.ModelBuild), x.TimelineRecordName));
                var reportBody = ReportBuilder.BuildSearchTimeline(
                    results,
                    markdown: true,
                    includeDefinition: true,
                    footer.ToString());

                var gitHubIssueKey = TriageUtil.GetGitHubIssueKey(timelineQuery);
                Console.Write($"Updating {gitHubIssueKey.IssueUri} ... ");
                var succeeded = await UpdateGitHubIssueReport(gitHubIssueKey, reportBody);
                Console.WriteLine(succeeded ? "succeeded" : "failed");
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
                    Console.WriteLine("Cannot find the replacement section in the issue");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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
            const int buildLimit = 200;
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
            header.AppendLine($"The build numbers given in the tables below cover the last {buildLimit} builds of the repository");
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
                header.AppendLine($"- [{title}](https://github.com/dotnet/runtime/issues?q=is%3Aopen+is%3Aissue+label%3{label})");

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
                var issueKey = new GitHubIssueKey("jaredpar", "devops-util", 5);
                var issueClient = GitHubClient.Issue;
                var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Number);
                var updateIssue = issue.ToUpdate();
                updateIssue.Body = header.ToString() + body.ToString() + footer.ToString();
                await GitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, updateIssue);
            }

            int? GetImpactedBuildsCount(GitHubIssueKey issueKey, BuildDefinitionKey definitionKey)
            {
                if (!TriageUtil.TryGetTimelineQuery(issueKey, out var timelineQuery))
                {
                    return null;
                }

                // TODO: need to be able to filter to the repo the build ran against
                var count = Context.ModelTimelineItems
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition)
                    .Where(x =>
                        x.ModelTimelineQueryId == timelineQuery.Id &&
                        x.ModelBuild.ModelBuildDefinition.AzureOrganization == definitionKey.Organization &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == definitionKey.Project &&
                        x.ModelBuild.ModelBuildDefinition.DefinitionId == definitionKey.Id)
                    .Count();
                return count;;
            }
        }
    }
}
