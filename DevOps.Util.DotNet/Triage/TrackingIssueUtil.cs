﻿using System;
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

namespace DevOps.Util.DotNet.Triage
{
    public sealed class TrackingIssueUtil
    {
        internal HelixServer HelixServer { get; }
        internal DotNetQueryUtil QueryUtil { get; }
        internal TriageContextUtil TriageContextUtil { get; }
        private ILogger Logger { get; }

        internal TriageContext Context => TriageContextUtil.Context;
        internal DevOpsServer Server => QueryUtil.Server;

        public TrackingIssueUtil(
            HelixServer helixServer,
            DotNetQueryUtil queryUtil,
            TriageContextUtil triageContextUtil,
            ILogger logger)
        {
            HelixServer = helixServer;
            QueryUtil = queryUtil;
            TriageContextUtil = triageContextUtil;
            Logger = logger;
        }

        public async Task TriageAsync(BuildKey buildKey, int modelTrackingIssueId)
        {
            var attempts = await TriageContextUtil
                .GetModelBuildAttemptsQuery(buildKey)
                .Include(x => x.ModelBuild)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var attempt in attempts)
            {
                await TriageAsync(attempt.GetBuildAttemptKey(), modelTrackingIssueId).ConfigureAwait(false);
            }
        }

        public async Task TriageAsync(BuildAttemptKey attemptKey, int modelTrackingIssueId)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == modelTrackingIssueId)
                .SingleAsync().ConfigureAwait(false);
            var modelBuildAttempt = await GetModelBuildAttemptAsync(attemptKey).ConfigureAwait(false);
            await TriageAsync(modelBuildAttempt, modelTrackingIssue).ConfigureAwait(false);
        }

        public async Task TriageAsync(BuildAttemptKey attemptKey)
        {
            var modelBuildAttempt = await GetModelBuildAttemptAsync(attemptKey).ConfigureAwait(false);
            await TriageAsync(modelBuildAttempt).ConfigureAwait(false);
        }

        public async Task TriageAsync(ModelBuildAttempt modelBuildAttempt)
        {
            if (modelBuildAttempt.ModelBuild is null)
            {
                throw new Exception("The attempt must include the build");
            }

            Logger.LogInformation($"Triaging {modelBuildAttempt.ModelBuild.GetBuildResultInfo().BuildUri}");

            var trackingIssues = await (Context
                .ModelTrackingIssues
                .Where(x => x.IsActive && (x.ModelBuildDefinition == null || x.ModelBuildDefinition.Id == modelBuildAttempt.ModelBuild.ModelBuildDefinitionId))
                .ToListAsync()).ConfigureAwait(false);

            foreach (var trackingIssue in trackingIssues)
            {
                await TriageAsync(modelBuildAttempt, trackingIssue).ConfigureAwait(false);
            }
        }

        public async Task TriageAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            if (modelBuildAttempt.ModelBuild is null)
            {
                throw new Exception("The attempt must include the build");
            }

            if (modelTrackingIssue.ModelBuildDefinitionId is { } definitionId &&
                definitionId != modelBuildAttempt.ModelBuild.ModelBuildDefinitionId)
            {
                return;
            }

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
                case TrackingKind.HelixLogs:
                    isPresent = await TriageHelixLogsAsync(modelBuildAttempt, modelTrackingIssue).ConfigureAwait(false);
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

            // This can race with other attempts to associate issues here. That is okay though because triage attempts are 
            // retried because they assume races with other operations can happen. 
            if (isPresent && modelTrackingIssue.GetGitHubIssueKey() is { } issueKey)
            {
                await TriageContextUtil.EnsureGitHubIssueAsync(modelBuildAttempt.ModelBuild, issueKey, saveChanges: false).ConfigureAwait(false);
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);

            async Task<bool> WasTriaged()
            {
                var query = Context
                    .ModelTrackingIssueResults
                    .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id && x.ModelBuildAttemptId == modelBuildAttempt.Id);
                return await query.AnyAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> TriageTestAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            // The only actual build range filtering we do at this point is making sure that the
            // definition filtering matches
            if (modelTrackingIssue.ModelBuildDefinitionId is { } definitionId && modelBuildAttempt.ModelBuildDefinitionId != definitionId)
            {
                return false;
            }

            var testsQuery = Context
                .ModelTestResults
                .Where(x => x.ModelBuildId == modelBuildAttempt.ModelBuildId && x.Attempt == modelBuildAttempt.Attempt);

            var request = new SearchTestsRequest(modelTrackingIssue.SearchQuery)
            {
                Started = null,
            };

            testsQuery = request.Filter(testsQuery).Include(x => x.ModelTestRun);

            var any = false;
            foreach (var testResult in await testsQuery.ToListAsync().ConfigureAwait(false))
            {
                var modelMatch = new ModelTrackingIssueMatch()
                {
                    ModelTrackingIssue = modelTrackingIssue,
                    ModelBuildAttempt = modelBuildAttempt,
                    ModelTestResult = testResult,
                    JobName = testResult.ModelTestRun.Name,
                };

                Context.ModelTrackingIssueMatches.Add(modelMatch);
                any = true;
            }

            return any;
        }

        private async Task<bool> TriageTimelineAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            // The only actual build range filtering we do at this point is making sure that the
            // definition filtering matches
            if (modelTrackingIssue.ModelBuildDefinitionId is { } definitionId && modelBuildAttempt.ModelBuildDefinitionId != definitionId)
            {
                return false;
            }

            var timelineQuery = Context
                .ModelTimelineIssues
                .Where(x => x.ModelBuildId == modelBuildAttempt.ModelBuildId && x.Attempt == modelBuildAttempt.Attempt);

            var request = new SearchTimelinesRequest(modelTrackingIssue.SearchQuery)
            {
                Started = null,
            };

            timelineQuery = request.Filter(timelineQuery);

            var any = false;
            foreach (var modelTimelineIssue in await timelineQuery.ToListAsync().ConfigureAwait(false))
            {
                var modelMatch = new ModelTrackingIssueMatch()
                {
                    ModelTrackingIssue = modelTrackingIssue,
                    ModelBuildAttempt = modelBuildAttempt,
                    ModelTimelineIssue = modelTimelineIssue,
                    JobName = modelTimelineIssue.JobName,
                };
                Context.ModelTrackingIssueMatches.Add(modelMatch);
                any = true;
            }

            return any;
        }

        private async Task<bool> TriageHelixLogsAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var request = new SearchHelixLogsRequest()
            {
                Started = null,
                Limit = 100,
            };
            request.ParseQueryString(modelTrackingIssue.SearchQuery);

            var query = request.Filter(Context.ModelTestResults)
                .Where(x => x.ModelBuildAttemptId == modelBuildAttempt.Id);
            
            // TODO: selecting a lot of info here. Can improve perf by selecting only the needed 
            // columns. The search helix logs page already optimizes this. Consider factoring out
            // the shared code.
            var testResultList = await query.ToListAsync().ConfigureAwait(false);
            var buildInfo = modelBuildAttempt.ModelBuild.GetBuildInfo();
            var helixLogInfos = testResultList
                .Select(x => x.GetHelixLogInfo())
                .SelectNotNull()
                .Select(x => (buildInfo, x));

            var results = await HelixServer.SearchHelixLogsAsync(
                helixLogInfos,
                request,
                onError: x => Logger.LogWarning(x.Message)).ConfigureAwait(false);
            var any = false;
            foreach (var result in results.Where(x => x.IsMatch))
            {
                any = true;
                var modelMatch = new ModelTrackingIssueMatch()
                {
                    ModelBuildAttempt = modelBuildAttempt,
                    ModelTrackingIssue = modelTrackingIssue,
                    HelixLogKind = result.HelixLogKind,
                    HelixLogUri = result.HelixLogUri,
                    JobName = "",
                };
                Context.ModelTrackingIssueMatches.Add(modelMatch);
            }
            return any;
        }

        private Task<ModelBuildAttempt> GetModelBuildAttemptAsync(BuildAttemptKey attemptKey) => TriageContextUtil
            .GetModelBuildAttemptQuery(attemptKey)
            .Include(x => x.ModelBuild)
            .SingleAsync();
    }
}
