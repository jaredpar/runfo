using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Model;
using Octokit;

internal sealed class AutoTriageUtil
{
    internal DevOpsServer Server { get; }
    internal GitHubClient GitHubClient { get; }
    internal DotNetQueryUtil QueryUtil { get; }

    internal AutoTriageUtil(DevOpsServer server, GitHubClient gitHubClient)
    {
        Server = server;
        GitHubClient = gitHubClient;
        QueryUtil = new DotNetQueryUtil(server);
    }

    internal async Task Triage()
    {
        await DoSearchTimeline(
            TriageReasonItem.Infra,
            new GitHubIssueKey("dotnet", "runtime", 35074),
            updateIssue: true,
            buildQuery: "-d runtime -c 100 -pr",
            text: "HTTP request to.*api.nuget.org.*timed out");
        await DoSearchTimeline(
            TriageReasonItem.Infra,
            new GitHubIssueKey("dotnet", "runtime", 34015),
            updateIssue: false,
            buildQuery: "-d runtime -c 100 -pr",
            text: "Failed to install dotnet");
        await DoSearchTimeline(
            TriageReasonItem.Infra,
            new GitHubIssueKey("dotnet", "runtime", 34015),
            updateIssue: false,
            buildQuery: "-d runtime-official -c 20",
            text: "Failed to install dotnet");
    }

    private async Task DoSearchTimeline(TriageReasonItem reason, GitHubIssueKey issueKey, bool updateIssue, string buildQuery, string text)
    {
        Console.WriteLine($"Searching Timeline");
        Console.WriteLine($"  Issue: {issueKey.IssueUri}");
        Console.WriteLine($"  Query: {buildQuery}");
        Console.WriteLine($"  Text: {text}");
        using var triageUtil = new TriageUtil();
        var builds = await QueryUtil.ListBuildsAsync(buildQuery);
        var searchTimelineResults = await QueryUtil.SearchTimelineAsync(builds, text);
        var count = 0;
        foreach (var result in searchTimelineResults)
        {
            var buildKey = DevOpsUtil.GetBuildKey(result.Build);
            if (triageUtil.TryAddReason(buildKey, reason, issueKey.IssueUri))
            {
                count++;
            }
        }

        Console.WriteLine($"  New builds found: {count}");

        // TODO: the report should be built off the info in our table storage, not the most
        // recent query.
        if (updateIssue)
        {
            var reportBuilder = new ReportBuilder();
            var reportBody = reportBuilder.BuildSearchTimeline(searchTimelineResults, builds.Count, markdown: true, includeDefinition: false);
            var status = await UpdateIssue(issueKey, reportBody) ? "succeeded" : "failed";
            Console.WriteLine("$  Update issue {status}");
        }
    }

    private async Task<bool> UpdateIssue(GitHubIssueKey issueKey, string reportBody)
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
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return false;
    }

    private static bool TryUpdateIssueText(string reportBody, string oldIssueText, out string newIssueText)
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
                if (Regex.IsMatch(line, @"<!--\s*runfo report end\s*-->"))
                {
                    inReportBody = false;
                    foundEnd = true;
                }
            }
            else if (Regex.IsMatch(line, @"<!--\s*runfo report start\s*-->"))
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
}
