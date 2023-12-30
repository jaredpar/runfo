using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;

namespace DevOps.Util.DotNet.Triage
{
    public sealed class TriageContextUtil
    {
        public TriageContext Context { get; }

        public TriageContextUtil(TriageContext context)
        {
            Context = context;
        }

        public static GitHubPullRequestKey? GetGitHubPullRequestKey(ModelBuild build) =>
            build.PullRequestNumber.HasValue
                ? (GitHubPullRequestKey?)new GitHubPullRequestKey(build.GitHubOrganization, build.GitHubRepository, build.PullRequestNumber.Value)
                : null;

        public async Task<ModelBuildDefinition> EnsureBuildDefinitionAsync(DefinitionInfo definitionInfo)
        {
            var buildDefinition = Context.ModelBuildDefinitions
                .Where(x =>
                    x.AzureOrganization == definitionInfo.Organization &&
                    x.AzureProject == definitionInfo.Project &&
                    x.DefinitionNumber == definitionInfo.Id)
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
                DefinitionNumber = definitionInfo.Id,
                DefinitionName = definitionInfo.Name,
            };

            Context.ModelBuildDefinitions.Add(buildDefinition);
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return buildDefinition;
        }

        public async Task<ModelBuild> EnsureBuildAsync(BuildResultInfo buildInfo)
        {
            if (buildInfo.StartTime is not { } startTime)
            {
                throw new InvalidOperationException("Cannot populate a build until it has started");
            }

            var modelBuildNameKey = buildInfo.BuildKey.NameKey;
            var modelBuild = Context.ModelBuilds
                .Where(x => x.NameKey == modelBuildNameKey)
                .FirstOrDefault();
            if (modelBuild is object)
            {
                // This code accounts for the fact that we will see multiple attempts of a build and that will
                // change the result. When those happens we should update all of the following values. It may
                // seem strange to update start and finish time here but that is how the AzDO APIs work and it's
                // best to model them in that way.
                if (modelBuild.BuildResult.ToBuildResult() != buildInfo.BuildResult)
                {
                    modelBuild.StartTime = startTime;
                    modelBuild.FinishTime = buildInfo.FinishTime;
                    modelBuild.BuildResult = buildInfo.BuildResult.ToModelBuildResult();
                    await Context.SaveChangesAsync().ConfigureAwait(false);
                }

                return modelBuild;
            }

            var prKey = buildInfo.PullRequestKey;
            var modelBuildDefinition = await EnsureBuildDefinitionAsync(buildInfo.DefinitionInfo).ConfigureAwait(false);
            modelBuild = new ModelBuild()
            {
                NameKey = modelBuildNameKey,
                ModelBuildDefinitionId = modelBuildDefinition.Id,
                AzureOrganization = modelBuildDefinition.AzureOrganization,
                AzureProject = modelBuildDefinition.AzureProject,
                GitHubOrganization = buildInfo.GitHubBuildInfo?.Organization ?? "",
                GitHubRepository = buildInfo.GitHubBuildInfo?.Repository ?? "",
                GitHubTargetBranch = buildInfo.GitHubBuildInfo?.TargetBranch,
                PullRequestNumber = prKey?.Number,
                StartTime = startTime,
                FinishTime = buildInfo.FinishTime,
                QueueTime = buildInfo.QueueTime,
                BuildNumber = buildInfo.Number,
                BuildResult = buildInfo.BuildResult.ToModelBuildResult(),
                BuildKind = buildInfo.PullRequestKey.HasValue ? ModelBuildKind.PullRequest : ModelBuildKind.Rolling,
                DefinitionName = buildInfo.DefinitionName,
                DefinitionNumber = buildInfo.DefinitionInfo.Id,
            };
            Context.ModelBuilds.Add(modelBuild);
            Context.SaveChanges();
            return modelBuild;
        }

        public async Task EnsureResultAsync(ModelBuild modelBuild, Build build)
        {
            if (modelBuild.BuildResult.ToBuildResult() != build.Result)
            {
                var buildInfo = build.GetBuildResultInfo();
                if (buildInfo.StartTime is not { } startTime)
                {
                    throw new InvalidOperationException("Cannot populate a build until it has started");
                }

                modelBuild.BuildResult = build.Result.ToModelBuildResult();
                modelBuild.StartTime = startTime;
                modelBuild.FinishTime = buildInfo.FinishTime;
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task<ModelBuildAttempt> EnsureBuildAttemptAsync(BuildResultInfo buildInfo, Timeline timeline)
        {
            var modelBuild = await EnsureBuildAsync(buildInfo).ConfigureAwait(false);
            return await EnsureBuildAttemptAsync(modelBuild, buildInfo.BuildResult.ToModelBuildResult(), timeline).ConfigureAwait(false);
        }

        public async Task<ModelBuildAttempt> EnsureBuildAttemptAsync(ModelBuild modelBuild, ModelBuildResult buildResult, Timeline timeline)
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
                ? startTimeQuery.Min()!.Value
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
                    NameKey = modelBuild.NameKey,
                    IsTimelineMissing = false,
                    GitHubTargetBranch = modelBuild.GitHubTargetBranch,
                    BuildKind = modelBuild.BuildKind,
                    DefinitionNumber = modelBuild.DefinitionNumber,
                    DefinitionName = modelBuild.DefinitionName,
                    ModelBuildDefinitionId = modelBuild.ModelBuildDefinitionId,
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
                        IssueType = issue.Type.ToModelIssueType(),
                        StartTime = startTime,
                        GitHubTargetBranch = modelBuild.GitHubTargetBranch,
                        BuildKind = modelBuild.BuildKind,
                        BuildResult = buildResult,
                        DefinitionNumber = modelBuild.DefinitionNumber,
                        DefinitionName = modelBuild.DefinitionName,
                        ModelBuild = modelBuild,
                        ModelBuildAttempt = modelBuildAttempt,
                        ModelBuildDefinitionId = modelBuild.ModelBuildDefinitionId,
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
                BuildResult = build.Result.ToModelBuildResult(),
                NameKey = modelBuild.NameKey,
                StartTime = modelBuild.StartTime,
                FinishTime = modelBuild.FinishTime,
                ModelBuild = modelBuild,
                ModelBuildDefinitionId = modelBuild.ModelBuildDefinitionId,
                DefinitionNumber = modelBuild.DefinitionNumber,
                DefinitionName = modelBuild.DefinitionName,
                IsTimelineMissing = false,
            };
            Context.ModelBuildAttempts.Add(modelBuildAttempt);
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return modelBuildAttempt;
        }

        public Task<ModelGitHubIssue> EnsureGitHubIssueAsync(ModelBuild modelBuild, GitHubIssueKey issueKey, bool saveChanges)
            => EnsureGitHubIssueAsync(modelBuild.GetBuildKey(), modelBuild.Id, issueKey, saveChanges);

        public async Task<ModelGitHubIssue> EnsureGitHubIssueAsync(BuildKey buildKey, int modelBuildId, GitHubIssueKey issueKey, bool saveChanges)
        {
            var query = GetModelBuildQuery(buildKey)
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
                ModelBuildId = modelBuildId,
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
            var nameKey = buildKey.NameKey;
            return Context.ModelBuilds.Where(x => x.NameKey == nameKey);
        }

        public Task<ModelBuild?> FindModelBuildAsync(BuildKey buildKey) =>
            GetModelBuildQuery(buildKey).FirstOrDefaultAsync()!;

        public Task<ModelBuild> GetModelBuildAsync(BuildKey buildKey) =>
            GetModelBuildQuery(buildKey).SingleAsync();

        public IQueryable<ModelBuildAttempt> GetModelBuildAttemptQuery(BuildAttemptKey buildAttemptKey)
        {
            var nameKey = buildAttemptKey.BuildKey.NameKey;
            return Context
                .ModelBuildAttempts
                .Where(x => x.NameKey == nameKey && x.Attempt == buildAttemptKey.Attempt);
        }

        public Task<ModelBuildAttempt?> FindModelBuildAttemptAsync(BuildAttemptKey buildAttemptKey) =>
            GetModelBuildAttemptQuery(buildAttemptKey).FirstOrDefaultAsync()!;

        public Task<ModelBuildAttempt> GetModelBuildAttemptAsync(BuildAttemptKey buildAttemptKey) =>
            GetModelBuildAttemptQuery(buildAttemptKey).SingleAsync();

        public IQueryable<ModelTestRun> GetModelTestRunQuery(int modelBuildId, int testRunId)
        {
            return Context
                .ModelTestRuns
                .Where(x => x.ModelBuildId == modelBuildId && x.TestRunId == testRunId);
        }

        public Task<ModelTestRun?> FindModelTestRunAsync(int modelBuildId, int testRunId) =>
            GetModelTestRunQuery(modelBuildId, testRunId).FirstOrDefaultAsync()!;

        public Task<ModelTestRun> GetModelTestRunAsync(int modelBuildId, int testRunId) =>
            GetModelTestRunQuery(modelBuildId, testRunId).SingleAsync()!;

        public IQueryable<ModelBuildDefinition> GetModelBuildDefinitionQueryAsync(int id) => Context
            .ModelBuildDefinitions
            .Where(x => x.DefinitionNumber == id);

        public Task<ModelBuildDefinition?> FindModelBuildDefinitionAsync(int id) =>
            GetModelBuildDefinitionQueryAsync(id).FirstOrDefaultAsync()!;

        public Task<ModelBuildDefinition?> GetModelBuildDefinitionAsync(int id) =>
            GetModelBuildDefinitionQueryAsync(id).SingleOrDefaultAsync();

        public async Task<ModelBuildDefinition?> FindModelBuildDefinitionAsync(string azureOrganization, string nameOrId)
        {
            if (int.TryParse(nameOrId, out var id))
            {
                return await Context
                    .ModelBuildDefinitions
                    .Where(x => x.DefinitionNumber == id && x.AzureOrganization == azureOrganization)
                    .SingleOrDefaultAsync()
                    .ConfigureAwait(false);
            }

            return await Context
                .ModelBuildDefinitions
                .Where(x => x.DefinitionName == nameOrId && x.AzureOrganization == azureOrganization)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public IQueryable<ModelGitHubIssue> GetModelGitHubIssuesQuery(GitHubIssueKey issueKey) => Context
            .ModelGitHubIssues
            .Where(x =>
                x.Number == issueKey.Number &&
                x.Organization == issueKey.Organization &&
                x.Repository == issueKey.Repository);

        public async Task<ModelTestRun> EnsureTestRunAsync(ModelBuildAttempt modelBuildAttempt, DotNetTestRun testRun, Dictionary<HelixInfoWorkItem, HelixLogInfo> helixMap)
        {
            var modelTestRun = await FindModelTestRunAsync(modelBuildAttempt.ModelBuildId, testRun.TestRunId).ConfigureAwait(false);
            if (modelTestRun is object)
            {
                return modelTestRun;
            }

            modelTestRun = new ModelTestRun()
            {
                ModelBuildId = modelBuildAttempt.ModelBuildId,
                ModelBuildAttempt = modelBuildAttempt,
                TestRunId = testRun.TestRunId,
                Name = testRun.TestRunName,
                Attempt = modelBuildAttempt.Attempt,
            };
            Context.ModelTestRuns.Add(modelTestRun);

            const int maxTestCaseCount = 200;
            int count = 0;
            foreach (var dotnetTestCaseResult in testRun.TestCaseResults)
            {
                count++;
                if (count >= maxTestCaseCount)
                {
                    break;
                }

                var testCaseResult = dotnetTestCaseResult.TestCaseResult;
                var testResult = new ModelTestResult()
                {
                    TestFullName = NormalizeString(testCaseResult.TestCaseTitle),
                    Outcome = testCaseResult.Outcome,
                    ModelTestRun = modelTestRun,
                    TestRunName = modelTestRun.Name,
                    ErrorMessage = NormalizeString(testCaseResult.ErrorMessage),
                    IsSubResultContainer = testCaseResult.SubResults?.Length > 0,
                    IsSubResult = false,
                    StartTime = modelBuildAttempt.StartTime,
                };

                AddQueryData(testResult);
                AddHelixInfo(testResult);
                Context.ModelTestResults.Add(testResult);

                if (testCaseResult.SubResults is { } subResults)
                {
                    foreach (var subResult in subResults)
                    {
                        var iterationTestResult = new ModelTestResult()
                        {
                            TestRunName = modelTestRun.Name,
                            TestFullName = testCaseResult.TestCaseTitle,
                            Outcome = subResult.Outcome,
                            ModelTestRun = modelTestRun,
                            ErrorMessage = subResult.ErrorMessage ?? "",
                            IsSubResultContainer = false,
                            IsSubResult = true,
                        };

                        AddQueryData(iterationTestResult);
                        AddHelixInfo(iterationTestResult);
                        Context.ModelTestResults.Add(iterationTestResult);
                    }
                }

                void AddHelixInfo(ModelTestResult testResult)
                {
                    if (dotnetTestCaseResult.HelixWorkItem is { } helixInfo &&
                        helixMap.TryGetValue(helixInfo, out var helixLogInfo))
                    {
                        testResult.IsHelixTestResult = true;
                        testResult.HelixConsoleUri = helixLogInfo.ConsoleUri;
                        testResult.HelixCoreDumpUri = helixLogInfo.CoreDumpUri;
                        testResult.HelixRunClientUri = helixLogInfo.RunClientUri;
                        testResult.HelixTestResultsUri = helixLogInfo.TestResultsUri;
                        testResult.HelixWorkItemName = helixInfo.WorkItemName;
                    }
                }

                void AddQueryData(ModelTestResult testResult)
                {
                    testResult.Attempt = modelBuildAttempt.Attempt;
                    testResult.StartTime = modelBuildAttempt.StartTime;
                    testResult.GitHubTargetBranch = modelBuildAttempt.GitHubTargetBranch;
                    testResult.BuildKind = modelBuildAttempt.BuildKind;
                    testResult.BuildResult = modelBuildAttempt.BuildResult;
                    testResult.DefinitionNumber = modelBuildAttempt.DefinitionNumber;
                    testResult.DefinitionName = modelBuildAttempt.DefinitionName;
                    testResult.ModelBuildId = modelBuildAttempt.ModelBuildId;
                    testResult.ModelBuildAttemptId = modelBuildAttempt.Id;
                    testResult.ModelBuildDefinitionId = modelBuildAttempt.ModelBuildDefinitionId;
                }
            }

            string NormalizeString(string? value)
            {
                if (value is null)
                {
                    return "";
                }

                var maxLen = 4096;
                if (value.Length > maxLen)
                {
                    value = $"{value.AsSpan().Slice(0, maxLen)}...";
                }

                return value;
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
                query = query.Where(x => x.DefinitionNumber == definitionId);
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
                default:
                    query = query.Where(x => x.BuildKind == kind);
                    break;
            }

            if (count is { } c)
            {
                query = query.Take(c);
            }

            return query;
        }

        public IQueryable<ModelBuildAttempt> GetModelBuildAttemptsQuery(BuildKey buildKey)
        {
            var nameKey = buildKey.NameKey;
            return Context
                .ModelBuildAttempts
                .Where(x => x.NameKey == nameKey);
        }

        public IQueryable<ModelTrackingIssue> GetModelTrackingIssuesQuery(GitHubIssueKey issueKey) => Context
            .ModelTrackingIssues
            .Where(x =>
                x.IsActive &&
                x.GitHubOrganization == issueKey.Organization &&
                x.GitHubRepository == issueKey.Repository &&
                x.GitHubIssueNumber == issueKey.Number);

        /// <summary>
        /// Update the model to reflect that this build was the build for a merged pull request. This will
        /// update the build and all of the related entities that track <see cref="ModelBuildKind"/>
        /// </summary>
        public async Task MarkAsMergedPullRequestAsync(ModelBuild modelBuild, CancellationToken cancellationToken = default)
        {
            modelBuild.BuildKind = ModelBuildKind.MergedPullRequest;
            foreach (var attempt in Context.ModelBuildAttempts.Where(x => x.ModelBuildId == modelBuild.Id))
            {
                attempt.BuildKind = ModelBuildKind.MergedPullRequest;
            }

            foreach (var issue in Context.ModelTimelineIssues.Where(x => x.ModelBuildId == modelBuild.Id))
            {
                issue.BuildKind = ModelBuildKind.MergedPullRequest;
            }

            foreach (var testResult in Context.ModelTestResults.Where(x => x.ModelBuildId == modelBuild.Id))
            {
                testResult.BuildKind = ModelBuildKind.MergedPullRequest;
            }

            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}