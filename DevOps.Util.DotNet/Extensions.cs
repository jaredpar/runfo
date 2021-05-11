using Microsoft.DotNet.Helix.Client;
using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

                    bool IsFailedOutcome(string outcome) => DevOpsUtil.FailedTestOutcomes.Any(x => comparer.Equals(x.ToString(), outcome));
                }
            }

            return testCaseResults;
        }

        public static async Task<DotNetTestRun> GetDotNetTestRunAsync(
            this DevOpsServer server,
            string project,
            int testRunId,
            string testRunName,
            TestOutcome[] outcomes,
            bool includeSubResults,
            Action<Exception>? onError = null)
        {
            var testCaseResults = await server.ListTestResultsAsync(project, testRunId, outcomes, includeSubResults, onError).ConfigureAwait(false);
            var list = ToDotNetTestCaseResult(testCaseResults);
            return new DotNetTestRun(project, testRunId, testRunName, new ReadOnlyCollection<DotNetTestCaseResult>(list));

            static List<DotNetTestCaseResult> ToDotNetTestCaseResult(List<TestCaseResult> testCaseResults)
            {
                var list = new List<DotNetTestCaseResult>();
                foreach (var testCaseResult in testCaseResults)
                {
                    var helixInfo = HelixUtil.TryGetHelixInfo(testCaseResult);
                    if (helixInfo is null)
                    {
                        list.Add(new DotNetTestCaseResult(testCaseResult));
                        continue;
                    }

                    var isHelixWorkItem = HelixUtil.IsHelixWorkItem(testCaseResult);
                    list.Add(new DotNetTestCaseResult(testCaseResult, helixInfo, isHelixWorkItem));
                }

                return list;
            }
        }

        public static async Task<List<DotNetTestRun>> ListDotNetTestRunsAsync(
            this DevOpsServer server,
            string project,
            int buildNumber,
            TestOutcome[] outcomes,
            bool includeSubResults,
            Action<Exception>? onError = null)
        {
            var testRuns = await server.ListTestRunsAsync(project, buildNumber).ConfigureAwait(false);
            var list = new List<Task<DotNetTestRun>>();
            foreach (var testRun in testRuns)
            {
                list.Add(GetDotNetTestRunAsync(server, project, testRun.Id, testRun.Name, outcomes, includeSubResults, onError));
            }

            await Task.WhenAll(list).ConfigureAwait(false);

            return list.Select(x => x.Result).ToList();
        }

        public static async Task<List<HelixInfo>> ListHelixInfosAsync(
            this DevOpsServer server,
            string project,
            int buildNumber,
            TestOutcome[] outcomes,
            Action<Exception>? onError = null)
        {
            // Don't need sub results to find the Helix info for test cases. All sub results will have a corresponding
            // work item node that we can get the info from
            var testRuns = await server.ListDotNetTestRunsAsync(project, buildNumber, outcomes, includeSubResults: false, onError).ConfigureAwait(false);
            return testRuns
                .SelectMany(x => x.TestCaseResults)
                .SelectNullableValue(x => x.HelixInfo)
                .ToHashSet()
                .OrderBy(x => (x.JobId, x.WorkItemName))
                .ToList();
        }

        public static async Task<Dictionary<HelixInfo, HelixLogInfo>> GetHelixMapAsync(
            this DevOpsServer server,
            string project,
            int buildNumber,
            TestOutcome[] outcomes,
            IHelixApi helixApi,
            Action<Exception>? onError = null)
        {
            // Don't need sub results to find the Helix info for test cases. All sub results will have a corresponding
            // work item node that we can get the info from
            var testRuns = await ListDotNetTestRunsAsync(server, project, buildNumber, outcomes, includeSubResults: false, onError).ConfigureAwait(false);
            return await helixApi.GetHelixMapAsync(testRuns.SelectMany(x => x.TestCaseResults)).ConfigureAwait(false);
        }

        #endregion

        #region IHelixApi

        public static Task<Dictionary<HelixInfo, HelixLogInfo>> GetHelixMapAsync(this IHelixApi helixApi, DotNetTestRun testRun) =>
            GetHelixMapAsync(helixApi, testRun.TestCaseResults);

        public static async Task<Dictionary<HelixInfo, HelixLogInfo>> GetHelixMapAsync(this IHelixApi helixApi, IEnumerable<DotNetTestCaseResult> testCaseResults)
        {
            var query = testCaseResults
                .SelectNullableValue(x => x.HelixInfo)
                .Distinct()
                .ToList()
                .AsParallel()
                .Select(async helixInfo => (helixInfo, await HelixUtil.GetHelixLogInfoAsync(helixApi, helixInfo).ConfigureAwait(false)));
            await Task.WhenAll(query).ConfigureAwait(false);
            return query.ToDictionary(x => x.Result.helixInfo, x => x.Result.Item2);
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
