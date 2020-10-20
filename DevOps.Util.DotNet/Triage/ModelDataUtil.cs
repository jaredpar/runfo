using DevOps.Util.DotNet;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    /// <summary>
    /// Responsible for ensuring Azure data is uploaded into our DB
    /// </summary>
    public sealed class ModelDataUtil
    {
        internal DevOpsServer Server { get; }

        internal DotNetQueryUtil QueryUtil { get; }

        internal TriageContextUtil TriageContextUtil { get; }

        internal ILogger Logger { get; }

        public ModelDataUtil(
            DotNetQueryUtil queryUtil,
            TriageContextUtil triageContextUtil, 
            ILogger logger)
        {
            Server = queryUtil.Server;
            QueryUtil = queryUtil;
            TriageContextUtil = triageContextUtil;
            Logger = logger;
        }

        public async Task<BuildAttemptKey> EnsureModelInfoAsync(Build build, bool includeTests = true)
        {
            var buildInfo = build.GetBuildResultInfo();
            var modelBuild = await TriageContextUtil.EnsureBuildAsync(buildInfo).ConfigureAwait(false);
            await TriageContextUtil.EnsureResultAsync(modelBuild, build).ConfigureAwait(false);
            var modelBuildAttempt = await EnsureTimeline().ConfigureAwait(false);

            if (includeTests)
            {
                await EnsureTestRuns().ConfigureAwait(false);
            }

            return new BuildAttemptKey(new BuildKey(build), modelBuildAttempt.Attempt);

            async Task<ModelBuildAttempt> EnsureTimeline()
            {
                try
                {
                    var timeline = await Server.GetTimelineAsync(buildInfo.Project, buildInfo.Number).ConfigureAwait(false);
                    if (timeline is null)
                    {
                        Logger.LogWarning("No timeline");
                    }
                    else
                    {
                        return await TriageContextUtil.EnsureBuildAttemptAsync(buildInfo, timeline).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting timeline: {ex.Message}");
                }

                return await TriageContextUtil.EnsureBuildAttemptWithoutTimelineAsync(modelBuild, build).ConfigureAwait(false);
            }

            async Task EnsureTestRuns()
            {
                TestRun[] testRuns;
                try
                {
                    testRuns = await Server.ListTestRunsAsync(buildInfo.Project, buildInfo.Number).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting test runs: {ex.Message}");
                    return;
                }

                var attempt = modelBuildAttempt?.Attempt ?? 1;
                foreach (var testRun in testRuns)
                {
                    await EnsureTestRun(testRun, attempt).ConfigureAwait(false);
                }
            }

            async Task EnsureTestRun(TestRun testRun, int attempt)
            {
                try
                {
                    var modelTestRun = await TriageContextUtil.FindModelTestRunAsync(modelBuild.GetBuildKey(), testRun.Id).ConfigureAwait(false);
                    if (modelTestRun is object)
                    {
                        return;
                    }

                    // TODO: Need to record when the maximum test results are exceeded. The limit here is to 
                    // protect us from a catastrophic run that has say several million failures (this is a real
                    // possibility
                    const int maxTestCaseResultCount = 200;
                    var dotNetTestRun = await QueryUtil.GetDotNetTestRunAsync(
                        build,
                        testRun,
                        DotNetUtil.FailedTestOutcomes,
                        includeSubResults: true,
                        onError: ex => Logger.LogWarning($"Error fetching test data {ex.Message}")).ConfigureAwait(false);
                    if (dotNetTestRun.TestCaseResults.Count > maxTestCaseResultCount)
                    {
                        dotNetTestRun = new DotNetTestRun(
                            dotNetTestRun.TestRunInfo,
                            dotNetTestRun.TestCaseResults.Take(maxTestCaseResultCount).ToReadOnlyCollection());
                    }
                    var helixMap = await Server.GetHelixMapAsync(dotNetTestRun).ConfigureAwait(false);

                    await TriageContextUtil.EnsureTestRunAsync(modelBuild, attempt, dotNetTestRun, helixMap).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error uploading test run: {ex.Message}");
                    return;
                }


            }
        }
    }
}
