using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Util.Triage
{
    public sealed class TrackingIssueUtil
    {
        internal DotNetQueryUtil QueryUtil { get; }

        internal TriageContextUtil TriageContextUtil { get; }

        private ILogger Logger { get; }

        internal TriageContext Context => TriageContextUtil.Context;

        internal DevOpsServer Server => QueryUtil.Server;

        public TrackingIssueUtil(
            DotNetQueryUtil queryUtil,
            TriageContextUtil triageContextUtil,
            ILogger logger)
        {
            QueryUtil = queryUtil;
            TriageContextUtil = triageContextUtil;
            Logger = logger;
        }

        public async Task TriageAsync(BuildAttemptKey attemptKey)
        {
            var query = Context
                .ModelBuildAttempts
                .Where(x =>
                    x.Attempt == attemptKey.Attempt &&
                    x.ModelBuild.BuildNumber == attemptKey.Number &&
                    x.ModelBuild.ModelBuildDefinition.AzureOrganization == attemptKey.Organization &&
                    x.ModelBuild.ModelBuildDefinition.AzureProject == attemptKey.Project)
                .Include(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition);
            var modelBuildAttempt = await query.SingleAsync().ConfigureAwait(false);
            await TriageAsync(modelBuildAttempt).ConfigureAwait(false);
        }

        public async Task TriageAsync(ModelBuildAttempt modelBuildAttempt)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);

            Logger.LogInformation($"Triaging {modelBuildAttempt.ModelBuild.GetBuildInfo().BuildUri}");

            var trackingIssues = await (Context
                .ModelTrackingIssues
                .Where(x => x.IsActive && (x.ModelBuildDefinition == null || x.ModelBuildDefinition.Id == modelBuildAttempt.ModelBuild.ModelBuildDefinition.Id))
                .ToListAsync()).ConfigureAwait(false);

            foreach (var trackingIssue in trackingIssues)
            {
                await TriageAsync(modelBuildAttempt, trackingIssue).ConfigureAwait(false);
            }
        }

        internal async Task TriageAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);

            // Quick spot check to avoid doing extra work if we've already triaged this attempt against this
            // issue
            if (await WasTriaged().ConfigureAwait(false))
            {
                return;
            }

            bool isPresent;
            switch (modelTrackingIssue.TrackingKind)
            {
                case TrackingKind.Test:
                    isPresent = await TriageTestAsync(modelBuildAttempt, modelTrackingIssue).ConfigureAwait(false);
                    break;
                case TrackingKind.Timeline:
                    isPresent = await TriageTimelineAsync(modelBuildAttempt, modelTrackingIssue).ConfigureAwait(false);
                    break;
                default:
                    throw new Exception($"Unknown value {modelTrackingIssue.TrackingKind}");
            }

            var result = new ModelTrackingIssueResult()
            {
                ModelBuildAttempt = modelBuildAttempt,
                ModelTrackingIssue = modelTrackingIssue,
                IsPresent = isPresent
            };
            Context.ModelTrackingIssueResults.Add(result);
            await Context.SaveChangesAsync().ConfigureAwait(false);

            async Task<bool> WasTriaged()
            {
                var query = Context
                    .ModelTrackingIssueResults
                    .Where(x => x.ModelBuildAttemptId == modelBuildAttempt.Id);
                return await query.AnyAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> TriageTestAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            Debug.Assert(modelTrackingIssue.SearchRegexText is object);

            var nameRegex = DotNetQueryUtil.CreateSearchRegex(modelTrackingIssue.SearchRegexText);
            var testQuery = Context
                .ModelTestResults
                .Where(x =>
                    x.ModelBuildId == modelBuildAttempt.ModelBuild.Id &&
                    x.ModelTestRun.Attempt == modelBuildAttempt.Attempt);
            foreach (var testResult in await testQuery.ToListAsync().ConfigureAwait(false))
            {
                if (nameRegex.IsMatch(testResult.TestFullName))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> TriageTimelineAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            Debug.Assert(modelTrackingIssue.SearchRegexText is object);

            var textRegex = DotNetQueryUtil.CreateSearchRegex(modelTrackingIssue.SearchRegexText);
            var timelineQuery = Context
                .ModelTimelineIssues
                .Where(x =>
                    x.ModelBuildId == modelBuildAttempt.ModelBuild.Id &&
                    x.Attempt == modelBuildAttempt.Attempt);
            foreach (var modelTimelineIssue in await timelineQuery.ToListAsync().ConfigureAwait(false))
            {
                if (textRegex.IsMatch(modelTimelineIssue.Message))
                {
                    return true;
                }
            }

            return false;
        }

        /*
        private void DoSearchTimeline(ModelTriageIssue modelTriageIssue, Timeline timeline)
        {
            var searchText = modelTriageIssue.SearchText;
            Logger.LogInformation($@"Text: ""{searchText}""");
            if (TriageContextUtil.IsProcessed(modelTriageIssue, ModelBuild))
            {
                Logger.LogInformation($@"Skipping");
                return;
            }

            var count = 0;
            foreach (var result in QueryUtil.SearchTimeline(Build.GetBuildInfo(), timeline, text: searchText))
            {
                count++;

                var modelTriageIssueResult = new ModelTriageIssueResult()
                {
                    TimelineRecordName = result.Record.RecordName,
                    JobName = result.Record.JobName,
                    JobRecordId = result.Record.JobRecord?.Id,
                    Line = result.Line,
                    ModelBuild = ModelBuild,
                    ModelTriageIssue = modelTriageIssue,
                    BuildNumber = result.BuildInfo.GetBuildKey().Number,
                };
                Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = ModelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            try
            {
                Logger.LogInformation($@"Saving {count} jobs");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save timeline complete: {ex.Message}");
            }
        }

        private async Task DoSearchHelixAsync(ModelTriageIssue modelTriageIssue, HelixLogKind kind)
        {
            await EnsureHelixLogInfosAsync().ConfigureAwait(false);
            Debug.Assert(HelixLogInfos is object);
            await DoSearchHelixAsync(modelTriageIssue, kind, HelixLogInfos);
        }

        private async Task DoSearchHelixAsync(
            ModelTriageIssue modelTriageIssue,
            HelixLogKind kind,
            List<(HelixWorkItem WorkItem, HelixLogInfo LogInfo)> helixLogInfos)
        {
            Logger.LogInformation($@"Helix Search {kind}: ""{modelTriageIssue.SearchText}""");

            // Guarding against a catostrophic PR failure here. When more than this many work items
            // fail abandon auto-triage
            //
            // The number chosen here is 100% arbitrary. Can adjust as more data comes in.
            if (helixLogInfos.Count > 10)
            {
                Logger.LogWarning($@"Too many results, not searching");
                return;
            }

            var count = 0;
            var jobMap = await EnsureHelixJobToRecordMap().ConfigureAwait(false);
            if (jobMap is null)
            {
                return;
            }

            foreach (var tuple in helixLogInfos)
            {
                var workItem = tuple.WorkItem;
                var helixLogInfo = tuple.LogInfo;
                var uri = helixLogInfo.GetUri(kind);
                if (uri is null)
                {
                    continue;
                }

                // TODO: should cache the log download so it can be used in multiple searches
                Logger.LogInformation($"Downloading helix log {kind} {uri}");
                var isMatch = await QueryUtil.SearchFileForAnyMatchAsync(
                    uri,
                    DotNetQueryUtil.CreateSearchRegex(modelTriageIssue.SearchText),
                    ex => Logger.LogWarning($"Error searching log: {ex.Message}"));
                if (isMatch)
                {
                    string? recordName = null;
                    string? jobName = null;
                    if (jobMap.TryGetValue(workItem.JobId, out var result))
                    {
                        recordName = result.Record.RecordName;
                        jobName = result.Record.JobName;
                    }

                    var modelTriageIssueResult = new ModelTriageIssueResult()
                    {
                        HelixJobId = workItem.JobId,
                        HelixWorkItem = workItem.WorkItemName,
                        ModelBuild = ModelBuild,
                        ModelTriageIssue = modelTriageIssue,
                        BuildNumber = BuildInfo.Number,
                        TimelineRecordName = recordName,
                        JobName = jobName,
                    };
                    Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
                    count++;
                }
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = ModelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            // TODO: should batch all the saves and not do it issue by issue
            try
            {
                Logger.LogInformation($@"Saving {count} helix info");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save helix complete: {ex.Message}");
            }
        }

        private async Task DoSearchTestAsync(ModelTriageIssue modelTriageIssue)
        {
            if (modelTriageIssue.SearchText is null)
            {
                Logger.LogError($"Search text is null for {modelTriageIssue.Id}");
                return;
            }

            var testRuns = await EnsureDotNetTestRuns().ConfigureAwait(false);
            if (testRuns is null)
            {
                return;
            }

            var jobMap = await EnsureHelixJobToRecordMap().ConfigureAwait(false);
            if (jobMap is null)
            {
                return;
            }

            var nameRegex = DotNetQueryUtil.CreateSearchRegex(modelTriageIssue.SearchText);
            var query = testRuns
                .SelectMany(x => x.TestCaseResults)
                .Where(x => nameRegex.IsMatch(x.TestCaseTitle));
            var count = 0;
            foreach (var testCaseResult in query)
            {
                string? recordName = null;
                string? jobName = null;
                string? jobId = null;
                string? workItemName = null;
                if (testCaseResult.HelixInfo is HelixInfo helixInfo)
                {
                    jobId = helixInfo.JobId;
                    workItemName = helixInfo.WorkItemName;
                    if (jobMap.TryGetValue(helixInfo.JobId, out var result))
                    {
                        recordName = result.Record.RecordName;
                        jobName = result.Record.JobName;
                    }
                }

                var modelTriageIssueResult = new ModelTriageIssueResult()
                {
                    HelixJobId = jobId,
                    HelixWorkItem = workItemName,
                    ModelBuild = ModelBuild,
                    ModelTriageIssue = modelTriageIssue,
                    BuildNumber = BuildInfo.Number,
                    TimelineRecordName = recordName,
                    JobName = jobName,
                };
                Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
                count++;
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = ModelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            try
            {
                Logger.LogInformation($@"Saving {count} test results");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save helix complete: {ex.Message}");
            }
        }

        private async Task EnsureHelixWorkItemsAsync()
        {
            if (HelixWorkItems is object)
            {
                return;
            }

            try
            {
                HelixWorkItems = await QueryUtil.ListHelixWorkItemsAsync(
                    Build,
                    DotNetUtil.FailedTestOutcomes);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting helix work items: {ex.Message}");
            }

            HelixWorkItems ??= new List<HelixWorkItem>();
        }

        private async Task EnsureHelixLogInfosAsync()
        {
            if (HelixLogInfos is object)
            {
                return;
            }

            await EnsureHelixWorkItemsAsync().ConfigureAwait(false);
            Debug.Assert(HelixWorkItems is object);

            var list = new List<(HelixWorkItem, HelixLogInfo)>();
            foreach (var helixWorkItem in HelixWorkItems)
            {
                try
                {
                    var helixLogInfo = await HelixUtil.GetHelixLogInfoAsync(Server, helixWorkItem).ConfigureAwait(false);
                    list.Add((helixWorkItem, helixLogInfo));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting log info for {helixWorkItem.HelixInfo}: {ex.Message}");
                }
            }

            HelixLogInfos = list;
        }

        private async Task<Dictionary<string, HelixTimelineResult>?> EnsureHelixJobToRecordMap()
        {
            if (HelixJobToRecordMap is object)
            {
                return HelixJobToRecordMap;
            }

            if (Timeline is null)
            {
                return null;
            }

            var map = new Dictionary<string, HelixTimelineResult>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var result in await QueryUtil.ListHelixJobsAsync(Timeline).ConfigureAwait(false))
                {
                    map[result.HelixJob.JobId] = result;
                }

                HelixJobToRecordMap = map;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting helix job to record map: {ex.Message}");
            }

            return HelixJobToRecordMap;
        }

        private async Task<List<DotNetTestRun>?> EnsureDotNetTestRuns()
        {
            if (DotNetTestRuns is object)
            {
                return DotNetTestRuns;
            }

            try
            {
                DotNetTestRuns = await QueryUtil.ListDotNetTestRunsAsync(
                    Build,
                    DotNetUtil.FailedTestOutcomes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error getting test runs: {ex.Message}");
            }

            return DotNetTestRuns;
        }
        */
    }
}
