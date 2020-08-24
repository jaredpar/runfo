#nullable enable

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
    }
}
