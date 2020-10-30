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
            if (modelBuildAttempt.ModelBuild is null ||
                modelBuildAttempt.ModelBuild.ModelBuildDefinition is null)
            {
                throw new Exception("The attempt must include the build and definition");
            }

            Logger.LogInformation($"Triaging {modelBuildAttempt.ModelBuild.GetBuildResultInfo().BuildUri}");

            var trackingIssues = await (Context
                .ModelTrackingIssues
                .Where(x => x.IsActive && (x.ModelBuildDefinition == null || x.ModelBuildDefinition.Id == modelBuildAttempt.ModelBuild.ModelBuildDefinition.Id))
                .ToListAsync()).ConfigureAwait(false);

            foreach (var trackingIssue in trackingIssues)
            {
                await TriageAsync(modelBuildAttempt, trackingIssue).ConfigureAwait(false);
            }
        }

        public async Task TriageAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            if (modelBuildAttempt.ModelBuild is null ||
                modelBuildAttempt.ModelBuild.ModelBuildDefinition is null)
            {
                throw new Exception("The attempt must include the build and definition");
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
                case TrackingKind.HelixConsole:
                    isPresent = await TriageHelixLogsAsync(modelBuildAttempt, modelTrackingIssue, HelixLogKind.Console).ConfigureAwait(false);
                    break;
                case TrackingKind.HelixRunClient:
                    isPresent = await TriageHelixLogsAsync(modelBuildAttempt, modelTrackingIssue, HelixLogKind.RunClient).ConfigureAwait(false);
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
                    .Where(x => x.ModelBuildAttemptId == modelBuildAttempt.Id && x.ModelTrackingIssueId == modelTrackingIssue.Id);
                return await query.AnyAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> TriageTestAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var request = new SearchTestsRequest();
            request.ParseQueryString(modelTrackingIssue.SearchQuery);
            IQueryable<ModelTestResult> testQuery = request
                .Filter(Context.ModelTestResults)
                .Where(x =>
                    x.ModelBuildId == modelBuildAttempt.ModelBuildId &&
                    x.ModelTestRun.Attempt == modelBuildAttempt.Attempt)
                .Include(x => x.ModelTestRun);
            var any = false;
            foreach (var testResult in await testQuery.ToListAsync().ConfigureAwait(false))
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
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var request = new SearchTimelinesRequest();
            request.ParseQueryString(modelTrackingIssue.SearchQuery);
            var timelineQuery = request.Filter(Context.ModelTimelineIssues)
                .Where(x =>
                    x.ModelBuildId == modelBuildAttempt.ModelBuild.Id &&
                    x.Attempt == modelBuildAttempt.Attempt);
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

        private async Task<bool> TriageHelixLogsAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue, HelixLogKind helixLogKind)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var request = new SearchHelixLogsRequest()
            {
                Limit = 100,
            };
            request.ParseQueryString(modelTrackingIssue.SearchQuery);
            request.HelixLogKinds.Clear();
            request.HelixLogKinds.Add(helixLogKind);

            var query = Context
                .ModelTestResults
                .Where(x => x.IsHelixTestResult && x.ModelBuild.Id == modelBuildAttempt.ModelBuild.Id && x.ModelTestRun.Attempt == modelBuildAttempt.Attempt);
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
            foreach (var result in results)
            {
                any = true;
                var modelMatch = new ModelTrackingIssueMatch()
                {
                    ModelBuildAttempt = modelBuildAttempt,
                    ModelTrackingIssue = modelTrackingIssue,
                    HelixLogUri = result.HelixLogUri,
                };
                Context.ModelTrackingIssueMatches.Add(modelMatch);
            }
            return any;
        }

        private Task<ModelBuildAttempt> GetModelBuildAttemptAsync(BuildAttemptKey attemptKey) => TriageContextUtil
            .GetModelBuildAttemptQuery(attemptKey)
            .Include(x => x.ModelBuild)
            .ThenInclude(x => x.ModelBuildDefinition)
            .SingleAsync();
    }
}
