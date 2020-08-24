using DevOps.Util.DotNet;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
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

        public async Task<ModelBuild> EnsureModelInfoAsync(Build build)
        {
            var buildInfo = build.GetBuildInfo();
            var modelBuild = await TriageContextUtil.EnsureBuildAsync(buildInfo).ConfigureAwait(false);
            await TriageContextUtil.EnsureResultAsync(modelBuild, build).ConfigureAwait(false);
            await EnsureTimeline().ConfigureAwait(false);
            await EnsureTestRuns().ConfigureAwait(false);

            return modelBuild;

            async Task EnsureTimeline()
            {
                try
                {
                    var timeline = await Server.GetTimelineAttemptAsync(buildInfo.Project, buildInfo.Number, attempt: 1).ConfigureAwait(false);
                    if (timeline is null)
                    {
                        Logger.LogWarning("No timeline");
                    }
                    else
                    {
                        await TriageContextUtil.EnsureBuildAttemptAsync(buildInfo, timeline);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting timeline: {ex.Message}");
                }
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

                foreach (var testRun in testRuns)
                {
                    await EnsureTestRun(testRun).ConfigureAwait(false);
                }
            }

            async Task EnsureTestRun(TestRun testRun)
            {
                try
                {
                    var modelTestRun = await TriageContextUtil.FindModelTestRunAsync(modelBuild, testRun.Id).ConfigureAwait(false);
                    if (modelTestRun is object)
                    {
                        return;
                    }

                    var dotNetTestRun = await QueryUtil.GetDotNetTestRunAsync(build, testRun, DotNetUtil.FailedTestOutcomes).ConfigureAwait(false);
                    var helixMap = await Server.GetHelixMapAsync(dotNetTestRun).ConfigureAwait(false);

                    await TriageContextUtil.EnsureTestRunAsync(modelBuild, dotNetTestRun, helixMap).ConfigureAwait(false);
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
