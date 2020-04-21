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
using Model;
using Octokit;

internal sealed class AutoTriageUtil : IDisposable
{
    internal DevOpsServer Server { get; }
    internal GitHubClient GitHubClient { get; }
    internal DotNetQueryUtil QueryUtil { get; }

    internal TriageUtil TriageUtil { get; }

    internal ReportBuilder ReportBuilder { get; } = new ReportBuilder();

    internal TriageDbContext Context => TriageUtil.Context;

    internal AutoTriageUtil(DevOpsServer server, GitHubClient gitHubClient)
    {
        Server = server;
        GitHubClient = gitHubClient;
        QueryUtil = new DotNetQueryUtil(server);
        TriageUtil = new TriageUtil();
    }

    public void Dispose()
    {
        TriageUtil.Dispose();
    }

    // TODO: don't do this if the issue is closed
    // TODO: limit builds to report on to 100 because after that the tables get too large

    // TODO: eventually this won't be necessary
    internal void EnsureTriageIssues()
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
    }

    internal async Task Triage(string buildQuery)
    {
        foreach (var build in await QueryUtil.ListBuildsAsync(buildQuery))
        {
            await Triage(build);
        }
    }

    // TODO: need overload that takes builds and groups up the issue and PR updates
    // or maybe just make that a separate operation from triage
    internal async Task Triage(Build build)
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

        foreach (var timelineQuery in timelineQueries)
        {
            Console.Write($@"  Text: ""{timelineQuery.SearchText}"" ... ");
            if (TriageUtil.IsProcessed(timelineQuery, buildKey))
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

    internal async Task UpdateQueryIssues()
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
                .Where(x => x.ModelTimelineQueryId == timelineQuery.Id)
                .OrderByDescending(x => x.BuildNumber)
                .ToList();
            
            // TODO: we use same Server here even if the Organization setting in the 
            // item specifies a different organization. Need to replace Server with 
            // a map from org -> DevOpsServer
            // TODO: using .Result here, need to fix
            // TODO: be nice if we didn't have to query the server here. Should change 
            // ReportBuilder to not require Build but rather build information 
            var results = timelineItems
                .Select(x => (Server.GetBuildAsync(x.ModelBuild.AzureProject, x.ModelBuild.BuildNumber).Result, x.TimelineRecordName));
            var reportBody = ReportBuilder.BuildSearchTimeline(results, markdown: true, includeDefinition: true);

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
            var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Id);
            if (TryUpdateIssueText(reportBody, issue.Body, out var newIssueBody))
            {
                var issueUpdate = issue.ToUpdate();
                issueUpdate.Body = newIssueBody;
                await issueClient.Update(issueKey.Organization, issueKey.Repository, issueKey.Id, issueUpdate);
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
                    builder.Append(line);
                    inReportBody = false;
                    foundEnd = true;
                }
            }
            else if (ReportBuilder.MarkdownReportStartRegex.IsMatch(line))
            {
                builder.AppendLine(line);
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

    internal async Task UpdateStatusIssue()
    {
        var builder = new StringBuilder();

        await BuildOne("Blocking CI", "blocking-clean-ci");
        await BuildOne("Blocking Official Build", "blocking-official-build");
        await BuildOne("Blocking CI Optional", "blocking-clean-ci-optional");
        await BuildOne("Blocking Outerloop", "blocking-outerloop");

        await UpdateIssue();

            /*
            BlockingOfficial = await DoSearch(gitHub, "blocking-official-build");
            BlockingNormalOptional = await DoSearch(gitHub, "blocking-clean-ci-optional");
            BlockingOuterloop = await DoSearch(gitHub, "blocking-outerloop");
            */

        async Task BuildOne(string title, string label)
        {
            builder.AppendLine($"## {title}");
            builder.AppendLine($"See [all issues](https://github.com/dotnet/runtime/issues?q=is%3Aopen+is%3Aissue+label%3{label})");
            builder.AppendLine("|Status|Issue|Impacted Builds|");
            builder.AppendLine("|---|---|---|");

            foreach (var issue in await DoSearch(label))
            {
                var issueKey = issue.GetIssueKey();
                var emoji = issue.Labels.Any(x => x.Name == "intermittent")
                    ? ":warning:"
                    : ":fire:";
                var titleLimit = 75;
                string issueText = issue.Title.Length >= titleLimit
                    ? issue.Title.Substring(0, titleLimit - 5) + " ..."
                    : issue.Title;
                string issueEntry = $"[{issueText}]({issue.HtmlUrl})";
                var impactedBuilds = GetImpactedBuilds(issueKey);

                builder.AppendLine($"|{emoji}|{issueEntry}|{impactedBuilds}|");
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
            var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Id);
            var updateIssue = issue.ToUpdate();
            updateIssue.Body = builder.ToString();
            await GitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Id, updateIssue);
        }

        string GetImpactedBuilds(GitHubIssueKey issueKey)
        {
            if (!TriageUtil.TryGetTimelineQuery(issueKey, out var timelineQuery))
            {
                return "N/A";
            }

            // TODO: need to be able to filter to the repo the build ran against
            var count = Context.ModelTimelineItems
                .Where(x => x.ModelTimelineQueryId == timelineQuery.Id)
                .Count();
            return count.ToString();
        }
    }

}
