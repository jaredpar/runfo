using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;

namespace DevOps.Util.DotNet.Triage
{
    public enum ModelBuildKind
    {
        All,
        Rolling,
        PullRequest,
        MergedPullRequest
    }

    public sealed class TriageContextUtil
    {
        public TriageContext Context { get; }

        public TriageContextUtil(TriageContext context)
        {
            Context = context;
        }

        public static string GetModelBuildId(BuildKey buildKey) => 
            $"{buildKey.Organization}-{buildKey.Project}-{buildKey.Number}";

        public static GitHubPullRequestKey? GetGitHubPullRequestKey(ModelBuild build) =>
            build.PullRequestNumber.HasValue
                ? (GitHubPullRequestKey?)new GitHubPullRequestKey(build.GitHubOrganization, build.GitHubRepository, build.PullRequestNumber.Value)
                : null;

        public static ModelBuildKind GetModelBuildKind(bool isMergedPullRequest, int? pullRequestNumber)
        {
            if (isMergedPullRequest)
            {
                return ModelBuildKind.MergedPullRequest;
            }

            if (pullRequestNumber.HasValue)
            {
                return ModelBuildKind.PullRequest;
            }

            return ModelBuildKind.Rolling;
        }

        public async Task<ModelBuildDefinition> EnsureBuildDefinitionAsync(DefinitionInfo definitionInfo)
        {
            var buildDefinition = Context.ModelBuildDefinitions
                .Where(x =>
                    x.AzureOrganization == definitionInfo.Organization &&
                    x.AzureProject == definitionInfo.Project &&
                    x.DefinitionId == definitionInfo.Id)
                .FirstOrDefault();
            if (buildDefinition is object)
            {
                if (buildDefinition.DefinitionName != definitionInfo.Name)
                {
                    buildDefinition.DefinitionName = definitionInfo.Name;
                    await Context.SaveChangesAsync().ConfigureAwait(false);
                }

                return buildDefinition;
            }

            buildDefinition = new ModelBuildDefinition()
            {
                AzureOrganization = definitionInfo.Organization,
                AzureProject = definitionInfo.Project,
                DefinitionId = definitionInfo.Id,
                DefinitionName = definitionInfo.Name,
            };

            Context.ModelBuildDefinitions.Add(buildDefinition);
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return buildDefinition;
        }

        public async Task<ModelBuild> EnsureBuildAsync(BuildResultInfo buildInfo)
        {
            var modelBuildId = GetModelBuildId(buildInfo.BuildKey);
            var modelBuild = Context.ModelBuilds
                .Where(x => x.Id == modelBuildId)
                .FirstOrDefault();
            if (modelBuild is object)
            {
                // This code accounts for the fact that we will see multiple attempts of a build and that will
                // change the result. When those happens we should update all of the following values. It may
                // seem strange to update start and finish time here but that is how the AzDO APIs work and it's
                // best to model them in that way.
                if (modelBuild.BuildResult != buildInfo.BuildResult)
                {
                    modelBuild.StartTime = buildInfo.StartTime;
                    modelBuild.FinishTime = buildInfo.FinishTime;
                    modelBuild.BuildResult = buildInfo.BuildResult;
                    await Context.SaveChangesAsync().ConfigureAwait(false);
                }

                return modelBuild;
            }

            var prKey = buildInfo.PullRequestKey;
            var modelBuildDefinition = await EnsureBuildDefinitionAsync(buildInfo.DefinitionInfo).ConfigureAwait(false);
            modelBuild = new ModelBuild()
            {
                Id = modelBuildId,
                ModelBuildDefinitionId = modelBuildDefinition.Id,
                AzureOrganization = modelBuildDefinition.AzureOrganization,
                AzureProject = modelBuildDefinition.AzureProject,
                GitHubOrganization = buildInfo.GitHubBuildInfo?.Organization,
                GitHubRepository = buildInfo.GitHubBuildInfo?.Repository,
                GitHubTargetBranch = buildInfo.GitHubBuildInfo?.TargetBranch,
                PullRequestNumber = prKey?.Number,
                StartTime = buildInfo.StartTime,
                FinishTime = buildInfo.FinishTime,
                QueueTime = buildInfo.QueueTime,
                BuildNumber = buildInfo.Number,
                BuildResult = buildInfo.BuildResult,
                DefinitionName = buildInfo.DefinitionName,
                DefinitionId = buildInfo.DefinitionInfo.Id,
            };
            Context.ModelBuilds.Add(modelBuild);
            Context.SaveChanges();
            return modelBuild;
        }

        public async Task EnsureResultAsync(ModelBuild modelBuild, Build build)
        {
            if (modelBuild.BuildResult != build.Result)
            {
                var buildInfo = build.GetBuildResultInfo();
                modelBuild.BuildResult = build.Result;
                modelBuild.StartTime = buildInfo.StartTime;
                modelBuild.FinishTime = buildInfo.FinishTime;
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<ModelBuildAttempt> EnsureBuildAttemptAsync(BuildResultInfo buildInfo, Timeline timeline)
        {
            var modelBuild = await EnsureBuildAsync(buildInfo).ConfigureAwait(false);
            return await EnsureBuildAttemptAsync(modelBuild, buildInfo.BuildResult, timeline).ConfigureAwait(false);
        }

        public async Task<ModelBuildAttempt> EnsureBuildAttemptAsync(ModelBuild modelBuild, BuildResult buildResult, Timeline timeline)
        {
            var attempt = timeline.GetAttempt();
            var modelBuildAttempt = await Context.ModelBuildAttempts
                .Where(x => x.ModelBuildId == modelBuild.Id && x.Attempt == attempt)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (modelBuildAttempt is object && !modelBuildAttempt.IsTimelineMissing)
            {
                return modelBuildAttempt;
            }

            var startTimeQuery = timeline
                .Records
                .Where(x => x.Attempt == attempt)
                .Select(x => DevOpsUtil.ConvertFromRestTime(x.StartTime))
                .SelectNullableValue()
                .Select(x => (DateTime?)x.DateTime);
            var startTime = startTimeQuery.Any()
                ? startTimeQuery.Min()
                : modelBuild.StartTime;
            
            var finishTimeQuery = timeline
                .Records
                .Where(x => x.Attempt == attempt)
                .Select(x => DevOpsUtil.ConvertFromRestTime(x.FinishTime))
                .SelectNullableValue()
                .Select(x => (DateTime?)x.DateTime);
            var finishTime = finishTimeQuery.Any()
                ? finishTimeQuery.Max()
                : modelBuild.FinishTime;

            if (modelBuildAttempt is object)
            {
                modelBuildAttempt.BuildResult = buildResult;
                modelBuildAttempt.StartTime = startTime;
                modelBuildAttempt.FinishTime = finishTime;
                modelBuildAttempt.IsTimelineMissing = false;
            }
            else
            {
                modelBuildAttempt = new ModelBuildAttempt()
                {
                    Attempt = attempt,
                    BuildResult = buildResult,
                    StartTime = startTime,
                    FinishTime = finishTime,
                    ModelBuild = modelBuild,
                    IsTimelineMissing = false,
                };
                Context.ModelBuildAttempts.Add(modelBuildAttempt);
            }

            var timelineTree = TimelineTree.Create(timeline);
            foreach (var record in timeline.Records)
            {
                if (record.Issues is null ||
                    !timelineTree.TryGetJob(record, out var job))
                {
                    continue;
                }

                foreach (var issue in record.Issues)
                {
                    var timelineIssue = new ModelTimelineIssue()
                    {
                        Attempt = attempt,
                        JobName = job.Name,
                        RecordName = record.Name,
                        TaskName = record.Task?.Name ?? "",
                        RecordId = record.Id,
                        Message = issue.Message,
                        ModelBuild = modelBuild,
                        IssueType = issue.Type,
                        ModelBuildAttempt = modelBuildAttempt,
                    };
                    Context.ModelTimelineIssues.Add(timelineIssue);
                }
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);
            return modelBuildAttempt;
        }

        /// <summary>
        /// There are times when the AzDO API will not provide a Timeline for a Bulid. In those cases we still need to create 
        /// a <see cref="ModelBuildAttempt"/> entry. The assumption is this is for the first attempt and all of the values will 
        /// be
        /// </summary>
        /// <param name="modelBuild"></param>
        /// <param name="build"></param>
        /// <returns></returns>
        public async Task<ModelBuildAttempt> EnsureBuildAttemptWithoutTimelineAsync(ModelBuild modelBuild, Build build)
        {
            const int attempt = 1;
            var modelBuildAttempt = await Context.ModelBuildAttempts
                .Where(x => x.ModelBuildId == modelBuild.Id && x.Attempt == attempt)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (modelBuildAttempt is object)
            {
                return modelBuildAttempt;
            }

            modelBuildAttempt = new ModelBuildAttempt()
            {
                Attempt = attempt,
                BuildResult = build.Result,
                StartTime = modelBuild.StartTime,
                FinishTime = modelBuild.FinishTime,
                ModelBuild = modelBuild,
                IsTimelineMissing = false,
            };
            Context.ModelBuildAttempts.Add(modelBuildAttempt);
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return modelBuildAttempt;
        }

        public async Task<ModelGitHubIssue> EnsureGitHubIssueAsync(ModelBuild modelBuild, GitHubIssueKey issueKey, bool saveChanges)
        {
            var query = GetModelBuildQuery(modelBuild.GetBuildKey())
                .SelectMany(x => x.ModelGitHubIssues)
                .Where(x =>
                    x.Number == issueKey.Number &&
                    x.Organization == issueKey.Organization &&
                    x.Repository == issueKey.Repository);
            var modelGitHubIssue = await query.SingleOrDefaultAsync().ConfigureAwait(false);
            if (modelGitHubIssue is object)
            {
                return modelGitHubIssue;
            }

            modelGitHubIssue = new ModelGitHubIssue()
            {
                Organization = issueKey.Organization,
                Repository = issueKey.Repository,
                Number = issueKey.Number,
                ModelBuild = modelBuild,
            };

            Context.ModelGitHubIssues.Add(modelGitHubIssue);

            if (saveChanges)
            {
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }

            return modelGitHubIssue;
        }

        public IQueryable<ModelBuild> GetModelBuildQuery(BuildKey buildKey)
        {
            var id = GetModelBuildId(buildKey);
            return Context.ModelBuilds.Where(x => x.Id == id);
        }

        public Task<ModelBuild?> FindModelBuildAsync(BuildKey buildKey) =>
            GetModelBuildQuery(buildKey).FirstOrDefaultAsync()!;

        public Task<ModelBuild> GetModelBuildAsync(BuildKey buildKey) =>
            GetModelBuildQuery(buildKey).SingleAsync();

        public IQueryable<ModelBuildAttempt> GetModelBuildAttemptQuery(BuildAttemptKey buildAttemptKey)
        {
            var buildId = GetModelBuildId(buildAttemptKey.BuildKey);
            return Context
                .ModelBuildAttempts
                .Where(x => x.ModelBuildId == buildId && x.Attempt == buildAttemptKey.Attempt);
        }

        public Task<ModelBuildAttempt?> FindModelBuildAttemptAsync(BuildAttemptKey buildAttemptKey) =>
            GetModelBuildAttemptQuery(buildAttemptKey).FirstOrDefaultAsync()!;

        public Task<ModelBuildAttempt> GetModelBuildAttemptAsync(BuildAttemptKey buildAttemptKey) =>
            GetModelBuildAttemptQuery(buildAttemptKey).SingleAsync();

        public IQueryable<ModelTestRun> GetModelTestRunQuery(BuildKey buildKey, int testRunId)
        {
            var buildId = GetModelBuildId(buildKey);
            return Context
                .ModelTestRuns
                .Where(x => x.ModelBuildId == buildId && x.TestRunId == testRunId);
        }

        public Task<ModelTestRun?> FindModelTestRunAsync(BuildKey buildKey, int testRunId) =>
            GetModelTestRunQuery(buildKey, testRunId).FirstOrDefaultAsync()!;

        public Task<ModelTestRun> GetModelTestRunAsync(BuildKey buildKey, int testRunId) =>
            GetModelTestRunQuery(buildKey, testRunId).SingleAsync()!;

        public IQueryable<ModelBuildDefinition> GetModelBuildDefinitionQueryAsync(int id) => Context
            .ModelBuildDefinitions
            .Where(x => x.DefinitionId == id);

        public Task<ModelBuildDefinition?> FindModelBuildDefinitionAsync(int id) =>
            GetModelBuildDefinitionQueryAsync(id).FirstOrDefaultAsync()!;

        public Task<ModelBuildDefinition> GetModelBuildDefinitionAsync(int id) =>
            GetModelBuildDefinitionQueryAsync(id).SingleOrDefaultAsync();

        public async Task<ModelBuildDefinition?> FindModelBuildDefinitionAsync(string nameOrId)
        {
            if (int.TryParse(nameOrId, out var id))
            {
                return await Context
                    .ModelBuildDefinitions
                    .Where(x => x.DefinitionId == id)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
            }

            return await Context
                .ModelBuildDefinitions
                .Where(x => x.DefinitionName == nameOrId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public IQueryable<ModelGitHubIssue> GetModelGitHubIssuesQuery(GitHubIssueKey issueKey) => Context
            .ModelGitHubIssues
            .Where(x =>
                x.Number == issueKey.Number &&
                x.Organization == issueKey.Organization &&
                x.Repository == issueKey.Repository);

        public async Task<ModelTestRun> EnsureTestRunAsync(ModelBuild modelBuild, int attempt, DotNetTestRun testRun, Dictionary<HelixInfo, HelixLogInfo> helixMap)
        {
            var modelTestRun = await FindModelTestRunAsync(modelBuild.GetBuildKey(), testRun.TestRun.Id).ConfigureAwait(false);
            if (modelTestRun is object)
            {
                return modelTestRun;
            }

            var buildInfo = testRun.Build.GetBuildResultInfo();
            modelTestRun = new ModelTestRun()
            {
                AzureOrganization = buildInfo.Organization,
                AzureProject = buildInfo.Project,
                ModelBuild = modelBuild,
                TestRunId = testRun.TestRun.Id,
                Name = testRun.TestRun.Name,
                Attempt = attempt,
            };
            Context.ModelTestRuns.Add(modelTestRun);

            foreach (var dotnetTestCaseResult in testRun.TestCaseResults)
            {
                var testCaseResult = dotnetTestCaseResult.TestCaseResult;
                var testResult = new ModelTestResult()
                {
                    TestFullName = testCaseResult.TestCaseTitle,
                    Outcome = testCaseResult.Outcome,
                    ModelTestRun = modelTestRun,
                    ModelBuild = modelBuild,
                    JobName = modelTestRun.Name,
                    ErrorMessage = testCaseResult.ErrorMessage,
                    IsSubResultContainer = testCaseResult.SubResults?.Length > 0,
                    IsSubResult = false,
                };

                AddHelixInfo(testResult);
                Context.ModelTestResults.Add(testResult);

                if (testCaseResult.SubResults is { } subResults)
                {
                    foreach (var subResult in subResults)
                    {
                        var iterationTestResult = new ModelTestResult()
                        {
                            TestFullName = testCaseResult.TestCaseTitle,
                            Outcome = subResult.Outcome,
                            ModelTestRun = modelTestRun,
                            ModelBuild = modelBuild,
                            ErrorMessage = subResult.ErrorMessage,
                            IsSubResultContainer = false,
                            IsSubResult = true
                        };

                        AddHelixInfo(iterationTestResult);
                        Context.ModelTestResults.Add(iterationTestResult);
                    }
                }

                void AddHelixInfo(ModelTestResult testResult)
                {
                    if (dotnetTestCaseResult.HelixInfo is { } helixInfo &&
                        helixMap.TryGetValue(helixInfo, out var helixLogInfo))
                    {
                        testResult.IsHelixTestResult = true;
                        testResult.HelixConsoleUri = helixLogInfo.ConsoleUri;
                        testResult.HelixCoreDumpUri = helixLogInfo.CoreDumpUri;
                        testResult.HelixRunClientUri = helixLogInfo.RunClientUri;
                        testResult.HelixTestResultsUri = helixLogInfo.TestResultsUri;
                    }
                }
            }

            await Context.SaveChangesAsync().ConfigureAwait(false);
            return modelTestRun;
        }

        public IQueryable<ModelBuild> GetModelBuildsQuery(
            bool descendingOrder = true,
            int? definitionId = null,
            string? definitionName = null,
            ModelBuildKind kind = ModelBuildKind.All,
            string? gitHubRepository = null,
            string? gitHubOrganization = null,
            int? count = null)
        {
            if (definitionId is object && definitionName is object)
            {
                throw new Exception($"Cannot specify {nameof(definitionId)} and {nameof(definitionName)}");
            }

            // Need to always include ModelBuildDefinition at this point because the GetBuildKey function
            // depends on that being there.
            IQueryable<ModelBuild> query = Context.ModelBuilds;

            query = descendingOrder
                ? query.OrderByDescending(x => x.BuildNumber)
                : query.OrderBy(x => x.BuildNumber);

            if (definitionId is { } d)
            {
                query = query.Where(x => x.DefinitionId == definitionId);
            }
            else if (definitionName is object)
            {
                query = query.Where(x => x.DefinitionName == definitionName);
            }

            if (gitHubOrganization is object)
            {
                gitHubOrganization = gitHubOrganization.ToLower();
                query = query.Where(x => x.GitHubOrganization == gitHubOrganization);
            }

            if (gitHubRepository is object)
            {
                gitHubRepository = gitHubRepository.ToLower();
                query = query.Where(x => x.GitHubRepository == gitHubRepository);
            }

            switch (kind)
            {
                case ModelBuildKind.All:
                    // Nothing to filter
                    break;
                case ModelBuildKind.MergedPullRequest:
                    query = query.Where(x => x.IsMergedPullRequest);
                    break;
                case ModelBuildKind.PullRequest:
                    query = query.Where(x => x.PullRequestNumber.HasValue);
                    break;
                case ModelBuildKind.Rolling:
                    query = query.Where(x => x.PullRequestNumber == null);
                    break;
                default:
                    throw new InvalidOperationException($"Invalid kind {kind}");
            }

            if (count is { } c)
            {
                query = query.Take(c);
            }

            return query;
        }

        public IQueryable<ModelBuildAttempt> GetModelBuildAttemptsQuery(BuildKey buildKey)
        {
            var modelBuildId = GetModelBuildId(buildKey);
            return Context
                .ModelBuildAttempts
                .Where(x => x.ModelBuildId == modelBuildId);
        }

        public IQueryable<ModelTrackingIssue> GetModelTrackingIssuesQuery(GitHubIssueKey issueKey) => Context
            .ModelTrackingIssues
            .Where(x =>
                x.IsActive &&
                x.GitHubOrganization == issueKey.Organization &&
                x.GitHubRepository == issueKey.Repository &&
                x.GitHubIssueNumber == issueKey.Number);

        public IQueryable<ModelBuild> GetModelBuildsQuery(ModelTrackingIssue modelTrackingIssue, SearchBuildsRequest buildsRequest)
        {
            switch (modelTrackingIssue.TrackingKind)
            {
                case TrackingKind.Timeline:
                    {
                        var query = buildsRequest.Filter(Context.ModelTimelineIssues);
                        var request = new SearchTimelinesRequest();
                        request.ParseQueryString(modelTrackingIssue.SearchQuery);
                        return request
                            .Filter(query)
                            .Select(x => x.ModelBuild);
                    }
                case TrackingKind.Test:
                    {
                        var query = buildsRequest.Filter(Context.ModelTestResults);
                        var request = new SearchTestsRequest();
                        request.ParseQueryString(modelTrackingIssue.SearchQuery);
                        return request
                            .Filter(query)
                            .Select(x => x.ModelBuild);
                    }
                case TrackingKind.HelixLogs:
                    {
                        var query = buildsRequest.Filter(Context.ModelTestResults);
                        var request = new SearchHelixLogsRequest();
                        request.ParseQueryString(modelTrackingIssue.SearchQuery);
                        return request
                            .Filter(query)
                            .Select(x => x.ModelBuild);
                    }
#pragma warning disable 618
                    // TODO: delete once these types are removed from the DB
                case TrackingKind.HelixConsole:
                case TrackingKind.HelixRunClient:
                    throw null!;
#pragma warning restore 618
                default:
                    throw new InvalidOperationException($"Invalid kind {modelTrackingIssue.TrackingKind}");
            }
        }
    }
}