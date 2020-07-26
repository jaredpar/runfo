
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

    /// <summary>
    /// This class is responsible for making the updates to GitHub based on the stored state 
    /// of the model
    /// </summary>
    public sealed class TriageGitHubUtil
    {
        public IGitHubClient GitHubClient { get; }

        public TriageContextUtil TriageContextUtil { get; }

        public ReportBuilder ReportBuilder { get; } = new ReportBuilder();

        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public TriageGitHubUtil(
            IGitHubClient gitHubClient,
            TriageContext context,
            ILogger logger)
        {
            GitHubClient = gitHubClient;
            TriageContextUtil = new TriageContextUtil(context);
            Logger = logger;
        }

        public async Task UpdateGithubIssues()
        {
            foreach (var modelTriageIssue in Context.ModelTriageIssues.Include(x => x.ModelTriageGitHubIssues).ToList())
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
                var results = TriageContextUtil.FindModelTriageIssueResults(triageIssue, gitHubIssue);
                var footer = new StringBuilder();
                var mostRecent = results
                    .Select(x => x.ModelBuild)
                    .OrderByDescending(x => x.StartTime)
                    .FirstOrDefault();
                if (mostRecent is object && mostRecent.StartTime is DateTime startTime)
                {
                    Debug.Assert(mostRecent.StartTime.HasValue);
                    var buildKey = TriageContextUtil.GetBuildKey(mostRecent);
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                    startTime = TimeZoneInfo.ConvertTimeFromUtc(startTime, zone);

                    footer.AppendLine($"Most [recent]({buildKey.BuildUri}) failure {startTime}");
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
                    .Select(x => (TriageContextUtil.GetBuildInfo(x.ModelBuild), x.JobName));
                var reportBody = ReportBuilder.BuildSearchTimeline(
                    searchResults,
                    markdown: true,
                    includeDefinition: gitHubIssue.IncludeDefinitions,
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
            header.AppendLine("Please use these queries to discover issues");

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
