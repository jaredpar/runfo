
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Util.DotNet.Triage
{
    /// <summary>
    /// This builds the status page for the dotnet/runtime repository
    /// of the model
    /// </summary>
    public sealed class StatusPageUtil
    {
        public IGitHubClientFactory GitHubClientFactory { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public ReportBuilder ReportBuilder { get; } = new ReportBuilder();
        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public StatusPageUtil(
            IGitHubClientFactory gitHubClientFactory,
            TriageContext context,
            ILogger logger)
        {
            GitHubClientFactory = gitHubClientFactory;
            TriageContextUtil = new TriageContextUtil(context);
            Logger = logger;
        }

        public async Task UpdateStatusIssue()
        {
            var gitHubClient = await GitHubClientFactory.CreateForAppAsync("dotnet", "runtime").ConfigureAwait(false);
            var text = await GetStatusIssueTextAsync(gitHubClient).ConfigureAwait(false);
            var issueKey = new GitHubIssueKey("dotnet", "runtime", 702);
            var issueClient = gitHubClient.Issue;
            var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Number).ConfigureAwait(false);
            var updateIssue = issue.ToUpdate();
            updateIssue.Body = text;
            await gitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, updateIssue).ConfigureAwait(false);
        }

        public async Task<string> GetStatusIssueTextAsync()
        {
            var gitHubClient = await GitHubClientFactory.CreateForAppAsync("dotnet", "runtime").ConfigureAwait(false);
            return await GetStatusIssueTextAsync(gitHubClient).ConfigureAwait(false);
        }

        public async Task<string> GetStatusIssueTextAsync(IGitHubClient gitHubClient)
        {
            var header = new StringBuilder();
            var body = new StringBuilder();
            var footer = new StringBuilder();
            header.AppendLine("## Overview");
            header.AppendLine("Please use these queries to discover issues");

            await BuildOne("Blocking CI", "blocking-clean-ci", DotNetUtil.GetDefinitionKeyFromFriendlyName("runtime")).ConfigureAwait(false);
            await BuildOne("Blocking Official Build", "blocking-official-build", DotNetUtil.GetDefinitionKeyFromFriendlyName("runtime-official")).ConfigureAwait(false);
            await BuildOne("Blocking CI Optional", "blocking-clean-ci-optional", DotNetUtil.GetDefinitionKeyFromFriendlyName("runtime"));
            await BuildOne("Blocking Outerloop", "blocking-outerloop", null);

            // Blank line to move past the table 
            header.AppendLine("");
            BuildFooter();

            return header.ToString() + body.ToString() + footer.ToString();

            void BuildFooter()
            {
                footer.AppendLine(@"## Goals

1. A minimum 95% passing rate for the `runtime` pipeline

## Resources

1. [runtime pipeline analytics](https://dnceng.visualstudio.com/public/_build?definitionId=686&view=ms.vss-pipelineanalytics-web.new-build-definition-pipeline-analytics-view-cardmetrics)");

            }

            async Task BuildOne(string title, string label, DefinitionKey? definitionKey)
            {
                header.AppendLine($"- [{title}](https://github.com/dotnet/runtime/issues?q=is%3Aopen+is%3Aissue+label%3A{label})");

                body.AppendLine($"## {title}");
                body.AppendLine("|Status|Issue|Build Count|");
                body.AppendLine("|---|---|---|");

                var query = (await DoSearchAsync(gitHubClient, label).ConfigureAwait(false))
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

            async Task<List<(Octokit.Issue Issue, int? Count)>> DoSearchAsync(IGitHubClient gitHubClient, string label)
            {
                var request = new SearchIssuesRequest()
                {
                    Labels = new [] { label },
                    State = ItemState.Open,
                    Type = IssueTypeQualifier.Issue,
                    Repos = { { "dotnet", "runtime" } },
                };
                var result = await gitHubClient.Search.SearchIssues(request).ConfigureAwait(false);
                var list = new List<(Octokit.Issue Issue, int? Count)>();
                foreach (var issue in result.Items)
                {
                    var count = await GetImpactedBuildsCountAsync(issue.GetIssueKey()).ConfigureAwait(false);
                    list.Add((issue, count));

                }
                return list;
            }

            async Task<int?> GetImpactedBuildsCountAsync(GitHubIssueKey issueKey)
            {
                var modelTrackingIssue = await TriageContextUtil
                    .GetModelTrackingIssuesQuery(issueKey)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                if (modelTrackingIssue is null)
                {
                    return null;
                }

                var count = await Context
                    .ModelTrackingIssueResults
                    .Where(x => x.IsPresent && x.ModelTrackingIssueId == modelTrackingIssue.Id)
                    .CountAsync()
                    .ConfigureAwait(false);
                return count;;
            }
        }
    }
}
