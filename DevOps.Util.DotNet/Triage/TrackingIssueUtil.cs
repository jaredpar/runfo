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

        // TODO: this method is temporary as a transition to the new model. Should be deleted once the web page for adding items
        // is fully functional
        public async Task EnsureStandardTrackingIssues()
        {
            await EnsureTrackingIssueAsync(
                TrackingKind.Timeline,
                searchText: "HTTP request to.*api.nuget.org.*timed out").ConfigureAwait(false);
            await EnsureTrackingIssueAsync(
                TrackingKind.Timeline,
                searchText: "Failed to install dotnet").ConfigureAwait(false);
            await EnsureTrackingIssueAsync(
                TrackingKind.Timeline,
                searchText: "Notification of assignment to an agent was never received").ConfigureAwait(false);
            await EnsureTrackingIssueAsync(
                TrackingKind.Timeline,
                searchText: "Received request to deprovision: The request was cancelled by the remote provider").ConfigureAwait(false);

            async Task EnsureTrackingIssueAsync(
                TrackingKind trackingKind, 
                string searchText,
                int? buildDefinitionId = null,
                GitHubIssueKey? issueKey = null)
            {
                var query = TriageContextUtil.Context
                    .ModelTrackingIssues
                    .Where(x => x.TrackingKind == trackingKind && x.SearchQuery == searchText);
                var modelTrackingIssue = await query.FirstOrDefaultAsync().ConfigureAwait(false);
                if (modelTrackingIssue is object)
                {
                    if (issueKey is { } key && modelTrackingIssue.GetGitHubIssueKey() != key)
                    {
                        modelTrackingIssue.GitHubOrganization = key.Organization;
                        modelTrackingIssue.GitHubRepository = key.Repository;
                        modelTrackingIssue.GitHubIssueNumber = key.Number;
                        await TriageContextUtil.Context.SaveChangesAsync().ConfigureAwait(false);
                    }

                    return;
                }

                ModelBuildDefinition? modelBuildDefinition = null;
                if (buildDefinitionId is { } definitionId)
                {
                    modelBuildDefinition = await Context
                        .ModelBuildDefinitions
                        .Where(x => x.DefinitionId == definitionId)
                        .SingleAsync().ConfigureAwait(false);
                }

                modelTrackingIssue = new ModelTrackingIssue()
                {
                    TrackingKind = trackingKind,
                    SearchQuery = searchText,
                    ModelBuildDefinition = modelBuildDefinition,
                    IsActive = true,
                    GitHubOrganization = issueKey?.Organization,
                    GitHubRepository = issueKey?.Repository,
                    GitHubIssueNumber = issueKey?.Number
                };

                Context.ModelTrackingIssues.Add(modelTrackingIssue);
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }
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
                    isPresent = await TriageHelixAsync(modelBuildAttempt, modelTrackingIssue, HelixLogKind.Console).ConfigureAwait(false);
                    break;
                case TrackingKind.HelixRunClient:
                    isPresent = await TriageHelixAsync(modelBuildAttempt, modelTrackingIssue, HelixLogKind.RunClient).ConfigureAwait(false);
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
                .FilterTestResults(Context.ModelTestResults)
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
            var timelineQuery = request.FilterTimelines(Context.ModelTimelineIssues)
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

        private async Task<bool> TriageHelixAsync(ModelBuildAttempt modelBuildAttempt, ModelTrackingIssue modelTrackingIssue, HelixLogKind helixLogKind)
        {
            Debug.Assert(modelBuildAttempt.ModelBuild is object);
            Debug.Assert(modelBuildAttempt.ModelBuild.ModelBuildDefinition is object);
            Debug.Assert(modelTrackingIssue.IsActive);
            Debug.Assert(modelTrackingIssue.SearchQuery is object);

            var textRegex = DotNetQueryUtil.CreateSearchRegex(modelTrackingIssue.SearchQuery);
            var query = Context
                .ModelTestResults
                .Where(x => x.IsHelixTestResult && x.ModelBuild.Id == modelBuildAttempt.ModelBuild.Id && x.ModelTestRun.Attempt == modelBuildAttempt.Attempt);
            var testResultList = await query.ToListAsync().ConfigureAwait(false);
            var buildInfo = modelBuildAttempt.ModelBuild.GetBuildResultInfo();
            var helixLogInfos = testResultList
                .Select(x => x.GetHelixLogInfo())
                .SelectNotNull()
                .Select(x => (buildInfo, x));
            var request = new SearchHelixLogsRequest()
            {
                Text = modelTrackingIssue.SearchRegexText,
                HelixLogKinds = new List<HelixLogKind>(new[] { helixLogKind }),
                Limit = 100,
            };

            var results = await QueryUtil.SearchHelixLogsAsync(
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
    }
}
