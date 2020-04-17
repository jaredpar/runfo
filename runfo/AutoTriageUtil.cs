using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Model;

internal sealed class AutoTriageUtil
{
    internal DevOpsServer Server { get; }
    internal RuntimeQueryUtil QueryUtil { get; }

    internal AutoTriageUtil(DevOpsServer server)
    {
        Server = server;
        QueryUtil = new RuntimeQueryUtil(server);
    }

    internal async Task Triage()
    {
        await DoSearchTimeline(
            TriageReasonItem.Infra,
            new GitHubIssueKey("dotnet", "runtime", 34015),
            buildQuery: "-d runtime -c 100 -pr",
            text: "Failed to install dotnet");
    }

    private async Task DoSearchTimeline(TriageReasonItem reason, GitHubIssueKey issueKey, string buildQuery, string text)
    {
        Console.WriteLine($"Searching Timeline");
        Console.WriteLine($"  Issue: {issueKey.IssueUri}");
        Console.WriteLine($"  Query: {buildQuery}");
        Console.WriteLine($"  Text: {text}");
        using var triageUtil = new TriageUtil();
        var builds = await QueryUtil.ListBuildsAsync(buildQuery);
        var count = 0;
        foreach (var tuple in await QueryUtil.SearchTimelineAsync(builds, text))
        {
            var buildKey = DevOpsUtil.GetBuildKey(tuple.Build);
            if (triageUtil.TryAddReason(buildKey, reason, issueKey.IssueUri))
            {
                count++;
            }
        }

        Console.WriteLine($"  New builds found: {count}");
    }
}
