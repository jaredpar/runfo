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
                .GetModelBuildQuery(buildKey)
                .SelectMany(x => x.ModelBuildAttempts)
                .Select(x => x.Attempt)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var attempt in attempts)
            {
                await TriageAsync(new BuildAttemptKey(buildKey, attempt), modelTrackingIssueId).ConfigureAwait(false);
            }
        }

        public async Task TriageAsync(BuildAttemptKey attemptKey, int modelTrackingIssueId)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == modelTrackingIssueId)
                .SingleAsync().ConfigureAwait(false);
            await TriageAsync(attemptKey, modelTrackingIssue).ConfigureAwait(false);
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
                await TriageAsync(modelBuildAttempt.GetBuildAttemptKey(), trackingIssue).ConfigureAwait(false);
            }
        }

        public async Task TriageAsync(BuildAttemptKey attemptKey, ModelTrackingIssue modelTrackingIssue)
        {
            var data = await TriageContextUtil
                .GetModelBuildAttemptQuery(attemptKey)
                .Select(x => new
                {
                    x.Id,
                    x.ModelBuildId,
                    x.ModelBuildDefinitionId,
                })
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);
            if (data is object)
            {
                await TriageAsync(
                    attemptKey,
                    modelBuildAttemptId: data.Id,
                    modelBuildId: data.ModelBuildId,
                    modelDefinitionId: data.ModelBuildDefinitionId,
                    modelTrackingIssue).ConfigureAwait(false);
            }
        }

        private async Task TriageAsync(BuildAttemptKey attemptKey, int modelBuildAttemptId, int modelBuildId, int modelDefinitionId, ModelTrackingIssue modelTrackingIssue)
        {
            if (modelTrackingIssue.ModelBuildDefinitionId is { } definitionId &&
                definitionId != modelDefinitionId)
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
                    isPresent = await TriageTestAsync(attemptKey, modelBuildAttemptId, modelBuildId, modelTrackingIssue).ConfigureAwait(false);
                    break;
                case TrackingKind.Timeline:
                    isPresent = await TriageTimelineAsync(attemptKey, modelBuildAttemptId, modelBuildId, modelTrackingIssue).ConfigureAwait(false);
                    break;
                default:
                    throw new Exception($"Unknown value {modelTrackingIssue.TrackingKind}");
            }

            var result = new ModelTrackingIssueResult()
            {
                ModelBuildAttemptId = modelBuildAttemptId,
                ModelTrackingIssue = modelTrackingIssue,
                IsPresent = isPresent
            };
            Context.ModelTrackingIssueResults.Add(result);

            // This can race with other attempts to associate issues here. That is okay though because triage attempts are 
            // retried because they assume races with other operations can happen. 
            if (isPresent && modelTrackingIssue.GetGitHubIssueKey() is { } issueKey)
            {
                await TriageContextUtil.EnsureGitHubIssueAsync(attemptKey.BuildKey, modelBuildId, issueKey, saveChanges: false).ConfigureAwait(false);
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);

            async Task<bool> WasTriaged()
            {
                var query = Context
                    .ModelTrackingIssueResults
                    .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id && x.ModelBuildAttemptId == modelBuildAttemptId);
                return await query.AnyAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> TriageTestAsync(BuildAttemptKey attemptKey, int modelBuildAttemptId, int modelBuildId, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var testsQuery = Context
                .ModelTestResults
                .Where(x => x.ModelBuildId == modelBuildId && x.Attempt == attemptKey.Attempt);

            var request = new SearchTestsRequest(modelTrackingIssue.SearchQuery);
            CleanupTrackingRequest(request);

            var data = await request.Filter(testsQuery)
                .Select(x => new
                {
                    ModelTestResultId = x.Id,
                    JobName = x.TestRunName
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var any = false;
            foreach (var testResult in data)
            {
                var modelMatch = new ModelTrackingIssueMatch()
                {
                    ModelTrackingIssue = modelTrackingIssue,
                    ModelBuildAttemptId = modelBuildAttemptId,
                    ModelTestResultId = testResult.ModelTestResultId,
                    JobName = testResult.JobName,
                };

                Context.ModelTrackingIssueMatches.Add(modelMatch);
                any = true;
            }

            return any;
        }

        private async Task<bool> TriageTimelineAsync(BuildAttemptKey attemptKey, int modelBuildAttemptId, int modelBuildId, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var timelineQuery = Context
                .ModelTimelineIssues
                .Where(x => x.ModelBuildId == modelBuildId && x.Attempt == attemptKey.Attempt);

            var request = new SearchTimelinesRequest(modelTrackingIssue.SearchQuery);
            CleanupTrackingRequest(request);

            timelineQuery = request.Filter(timelineQuery);

            var any = false;
            foreach (var modelTimelineIssue in await timelineQuery.ToListAsync().ConfigureAwait(false))
            {
                var modelMatch = new ModelTrackingIssueMatch()
                {
                    ModelTrackingIssue = modelTrackingIssue,
                    ModelBuildAttemptId = modelBuildAttemptId,
                    ModelTimelineIssue = modelTimelineIssue,
                    JobName = modelTimelineIssue.JobName,
                };
                Context.ModelTrackingIssueMatches.Add(modelMatch);
                any = true;
            }

            return any;
        }

        /// <summary>
        /// This removes unnecessary data in the tracking request. The <see cref="ModelTrackingIssue.SearchQuery"/> field
        /// can support any arbitrary search string. Several of the fields though aren't valid in this context and using
        /// them causes us to not hit important search indexs. 
        /// </summary>
        /// <param name="request"></param>
        private static void CleanupTrackingRequest(SearchRequestBase request)
        {
            // This isn't a supported field in a tracking query
            request.Started = null;

            // The attempts are pre-filtered for their definition and adding it back causes us to miss and 
            // index
            request.Definition = null;
        }

        private Task<ModelBuildAttempt> GetModelBuildAttemptAsync(BuildAttemptKey attemptKey) => TriageContextUtil
            .GetModelBuildAttemptQuery(attemptKey)
            .Include(x => x.ModelBuild)
            .SingleAsync();
    }
}
