using DevOps.Util.DotNet;
using Microsoft.DotNet.Helix.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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

        public async Task<BuildAttemptKey> EnsureModelInfoAsync(Build build, bool includeTests = true, bool includeAllAttempts = false)
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
                        var modelBuildAttempt = await TriageContextUtil.EnsureBuildAttemptAsync(buildInfo, timeline).ConfigureAwait(false);
                        if (includeAllAttempts && timeline.GetAttempt() > 1)
                        {
                            await EnsurePreviousAttempts(modelBuildAttempt).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting timeline: {ex.Message}");
                }

                return await TriageContextUtil.EnsureBuildAttemptWithoutTimelineAsync(modelBuild, build).ConfigureAwait(false);
            }

            async Task EnsurePreviousAttempts(ModelBuildAttempt modelBuildAttempt)
            {
                Debug.Assert(modelBuildAttempt.Attempt > 1);
                try
                {
                    for (var i = 1; i < modelBuildAttempt.Attempt; i++)
                    {
                        var timeline = await Server.GetTimelineAttemptAsync(buildInfo.Project, buildInfo.Number, i);
                        if (timeline is object)
                        {
                            await TriageContextUtil.EnsureBuildAttemptAsync(buildInfo, timeline).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting populating previous attempts: {ex.Message}");
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
                    await EnsureTestRun(testRun, modelBuildAttempt).ConfigureAwait(false);
                }
            }

            async Task EnsureTestRun(TestRun testRun, ModelBuildAttempt modelBuildAttempt)
            {
                Debug.Assert(modelBuildAttempt.ModelBuild is object);
                try
                {
                    var modelTestRun = await TriageContextUtil.FindModelTestRunAsync(modelBuildAttempt.ModelBuildId, testRun.Id).ConfigureAwait(false);
                    if (modelTestRun is { AreHelixLogsComplete: true })
                    {
                        return;
                    }

                    // TODO: Need to record when the maximum test results are exceeded. The limit here is to 
                    // protect us from a catastrophic run that has say several million failures (this is a real
                    // possibility)
                    const int maxTestCaseResultCount = 200;
                    var dotNetTestRun = await QueryUtil.GetDotNetTestRunAsync(
                        build,
                        testRun,
                        DevOpsUtil.FailedTestOutcomes,
                        includeSubResults: true,
                        onError: ex => Logger.LogWarning($"Error fetching test data {ex.Message}")).ConfigureAwait(false);
                    if (dotNetTestRun.TestCaseResults.Count > maxTestCaseResultCount)
                    {
                        dotNetTestRun = new DotNetTestRun(
                            dotNetTestRun.ProjectName,
                            dotNetTestRun.TestRunId,
                            dotNetTestRun.TestRunName,
                            dotNetTestRun.TestCaseResults.Take(maxTestCaseResultCount).ToReadOnlyCollection());
                    }

                    var helixApi = HelixServer.GetHelixApi();
                    var helixMap = await helixApi.GetHelixMapAsync(dotNetTestRun).ConfigureAwait(false);

                    if (modelTestRun is null)
                    {
                        modelTestRun = await TriageContextUtil.EnsureTestRunAsync(modelBuildAttempt, dotNetTestRun, helixMap).ConfigureAwait(false);
                    }

                    await EnsureHelixLogs().ConfigureAwait(false);
                    Debug.Assert(modelTestRun.AreHelixLogsComplete);

                    async Task EnsureHelixLogs()
                    {
                        var context = TriageContextUtil.Context;
                        var httpClient = Server.HttpClient;
                        foreach (var pair in helixMap)
                        {
                            // TODO: more than just console logs
                            await ProcessFile(pair.Key.JobId, pair.Key.WorkItemName, pair.Value.ConsoleUri, ModelHelixLogKind.Console);
                        }

                        modelTestRun.AreHelixLogsComplete = true;
                        await context.SaveChangesAsync().ConfigureAwait(false);

                        async ValueTask ProcessFile(string jobId, string workItemName, string? uri, ModelHelixLogKind kind)
                        {
                            try
                            {
                                if (uri is null)
                                {
                                    return;
                                }

                                if (await context.ModelHelixLogs.Where(x => x.LogUri == uri).AnyAsync().ConfigureAwait(false))
                                {
                                    return;
                                }

                                var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
                                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                var log = new ModelHelixLog()
                                {
                                    HelixLogKind = kind,
                                    JobId = jobId,
                                    WorkItemName = workItemName,
                                    LogUri = uri,
                                    ModelBuild = modelBuildAttempt.ModelBuild,
                                    ModelBuildAttempt = modelBuildAttempt,
                                    ModelTestRun = modelTestRun,
                                };

                                if (content.Length > 1_000_000)
                                {
                                    log.IsContentTooLarge = true;
                                    log.Content = "";
                                }
                                else
                                {
                                    log.Content = content;
                                }

                                context.ModelHelixLogs.Add(log);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Can't download helix log {uri}: {ex.Message}");
                            }
                        }
                    }
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
