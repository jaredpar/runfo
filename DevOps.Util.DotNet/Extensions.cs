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

        /// <summary>
        /// List the set of <see cref="TestCaseResult"/> for a given <see cref="TestRun"/>. 
        ///
        /// Ideally we could include sub results in the ListTestResultsAsync call because it's a supported parameter of
        /// the ListTestResults AzDO REST API call. Unfortunately there is a bug right now where they are not respecting 
        /// that parameter. 
        ///
        /// As a fallback for now we simply iterate the results, look for ones that are likely theories, and then request the 
        /// results individually. This means we also limit to only failed outcomes bc otherwise we'd be making an additional
        /// API call for every single result. That could be extremely expensive
        /// </summary>
        public static async Task<List<TestCaseResult>> ListTestResultsAsync(
            this DevOpsServer server,
            string project,
            int testRunId,
            TestOutcome[]? outcomes,
            bool includeSubResults,
            Action<Exception>? onError = null)
        {
            var testCaseResults = await server.ListTestResultsAsync(project, testRunId, outcomes: outcomes).ConfigureAwait(false);
            if (includeSubResults)
            {
                var comparer = StringComparer.OrdinalIgnoreCase;
                for (int i = 0; i < testCaseResults.Count; i++)
                {
                    var testCaseResult = testCaseResults[i];
                    if (string.IsNullOrEmpty(testCaseResult.ErrorMessage) && IsFailedOutcome(testCaseResult.Outcome))
                    {
                        try
                        {
                            var otherResult = await server.GetTestCaseResultAsync(project, testRunId, testCaseResult.Id, ResultDetail.SubResults).ConfigureAwait(false);
                            if (otherResult.SubResults is object)
                            {
                                otherResult.SubResults = otherResult
                                    .SubResults
                                    .Where(x => IsFailedOutcome(x.Outcome))
                                    .ToArray();
                            }

                            testCaseResults[i] = otherResult;
                        }
                        catch (Exception ex)
                        {
                            // If we can't fetch the sub-result then just continue
                            onError?.Invoke(ex);
                        }
                    }

                    bool IsFailedOutcome(string outcome) => DotNetUtil.FailedTestOutcomes.Any(x => comparer.Equals(x.ToString(), outcome));
                }
            }

            return testCaseResults;
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
