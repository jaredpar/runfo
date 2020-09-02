using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public static class Extensions
    {
        #region DevOpsServer

        public static Task<List<Timeline>> ListTimelineAttemptsAsync(this DevOpsServer server, string project, int buildNumber)
        {
            IAzureUtil azureUtil = new AzureUtil(server);
            return azureUtil.ListTimelineAttemptsAsync(project, buildNumber);
        }

        public static Task<Dictionary<HelixInfo, HelixLogInfo>> GetHelixMapAsync(this DevOpsServer server, DotNetTestRun testRun) =>
            GetHelixMapAsync(server, testRun.TestCaseResults);

        public static async Task<Dictionary<HelixInfo, HelixLogInfo>> GetHelixMapAsync(this DevOpsServer server, IEnumerable<DotNetTestCaseResult> testCaseResults)
        {
            var query = testCaseResults
                .Where(x => x.HelixWorkItem.HasValue)
                .Select(x => x.HelixWorkItem!.Value)
                .GroupBy(x => x.HelixInfo)
                .ToList()
                .AsParallel()
                .Select(async g => (g.Key, await HelixUtil.GetHelixLogInfoAsync(server, g.First())));
            await Task.WhenAll(query).ConfigureAwait(false);
            return query.ToDictionary(x => x.Result.Key, x => x.Result.Item2);
        }

        #endregion

        #region HelixLogKind

        public static string GetDisplayFileName(this HelixLogKind kind) => kind switch
        {
            HelixLogKind.Console => "console.log",
            HelixLogKind.CoreDump => "core dump",
            HelixLogKind.RunClient => "runclient.py",
            HelixLogKind.TestResults => "test results",
            _ => throw new InvalidOperationException($"Invalid kind {kind}"),
        };

        public static string GetDisplayName(this HelixLogKind kind) => kind switch 
        {
            HelixLogKind.Console => "Console",
            HelixLogKind.CoreDump => "Core Dump",
            HelixLogKind.RunClient => "Run Client",
            HelixLogKind.TestResults => "Test Results",
            _ => throw new InvalidOperationException($"Invalid kind {kind}")
        };

        #endregion

    }
}
