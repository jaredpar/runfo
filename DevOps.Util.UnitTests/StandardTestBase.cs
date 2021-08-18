using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    public abstract class StandardTestBase : IDisposable
    {
        public DatabaseFixture DatabaseFixture { get; }
        public ITestOutputHelper TestOutputHelper { get; }
        public TriageContext Context { get; private set; }
        public TriageContextUtil TriageContextUtil { get; private set; }
        public DevOpsServer Server { get; set; }
        public DotNetQueryUtil QueryUtil { get; set; }
        public HelixServer HelixServer { get; set; }
        public TestableHttpMessageHandler TestableHttpMessageHandler { get; set; }
        public TestableLogger TestableLogger { get; set; }
        public TestableGitHubClientFactory TestableGitHubClientFactory { get; set; }
        public TestableGitHubClient TestableGitHubClient => TestableGitHubClientFactory.TestableGitHubClient;
        private int BuildCount { get; set; }
        private int DefinitionCount { get; set; }
        private int TestRunCount { get; set; }
        private int GitHubIssueCount { get; set; }
        private int HelixLogCount { get; set; }

        public StandardTestBase(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
        {
            DatabaseFixture = databaseFixture;
            DatabaseFixture.RegisterLoggerAction(testOutputHelper.WriteLine);
            TestOutputHelper = testOutputHelper;
            ResetContext();
            TestableHttpMessageHandler = new TestableHttpMessageHandler();
            TestableLogger = new TestableLogger(testOutputHelper);
            TestableGitHubClientFactory = new TestableGitHubClientFactory();

            var httpClient = new HttpClient(TestableHttpMessageHandler);
            Server = new DevOpsServer("random", httpClient: httpClient);
            HelixServer = new HelixServer(httpClient: httpClient);
            QueryUtil = new DotNetQueryUtil(Server);
        }

        public void Dispose()
        {
            Context.Dispose();
            DatabaseFixture.UnregisterLoggerAction(TestOutputHelper.WriteLine);
            DatabaseFixture.TestCompletion();
        }

        [MemberNotNull(nameof(Context))]
        [MemberNotNull(nameof(TriageContextUtil))]
        protected virtual void ResetContext()
        {
            Context?.Dispose();
            Context = new TriageContext(DatabaseFixture.Options);
            TriageContextUtil = new TriageContextUtil(Context);
        }

        public async Task<ModelBuildAttempt> AddAttemptAsync(ModelBuild build, int attempt) => 
            await AddAttemptAsync(build, attempt, timelineIssues: new (string?, string?, string?)[] { });

        public async Task<ModelBuildAttempt> AddAttemptAsync(
            ModelBuild build,
            int attempt,
            params (string? JobName, string? Message, string? RecordName)[]? timelineIssues) =>
            await AddAttemptAsync(build, attempt, startTime: null, finishTime: null, timelineIssues: timelineIssues);

        public async Task<ModelBuildAttempt> AddAttemptAsync(
            ModelBuild build,
            int attempt = 1,
            DateTime? startTime = null,
            DateTime? finishTime = null,
            (string? JobName, string? Message, string? RecordName)[]? timelineIssues = null)
        {
            var records = new List<TimelineRecord>();
            records.Add(new TimelineRecord()
            {
                Attempt = attempt,
                Id = Guid.NewGuid().ToString(),
                StartTime = DevOpsUtil.ConvertToRestTime(startTime ?? build.StartTime),
                FinishTime = DevOpsUtil.ConvertToRestTime(finishTime ?? build.FinishTime ?? build.StartTime)
            });

            if (timelineIssues is object)
            {
                foreach (var timelineIssue in timelineIssues)
                {
                    var jobRecord = new TimelineRecord()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = timelineIssue.JobName ?? "",
                        Type = "Job",
                    };
                    records.Add(jobRecord);

                    var issueRecord = new TimelineRecord()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = timelineIssue.JobName ?? "",
                        ParentId = jobRecord.Id,
                    };
                    records.Add(issueRecord);

                    issueRecord.Issues = new[]
                    {
                        new Issue()
                        {
                            Message = timelineIssue.Message ?? "",
                            Type = IssueType.Error,
                        }
                    };
                }
            }

            var timeline = new Timeline()
            {
                Records = records.ToArray(),
            };

            return await TriageContextUtil.EnsureBuildAttemptAsync(
                build,
                ModelBuildResult.Succeeded,
                timeline);
        }

        /// <summary>
        /// | number | result | date | github org | github repo |
        /// </summary>
        public async Task<ModelBuild> AddBuildAsync(string data, ModelBuildDefinition def)
        {
            var parts = data.Split("|");
            BuildResult? br = GetPartOrNull(parts, 1) is { } p ? Enum.Parse<BuildResult>(p) : null;
            return await AddBuildAsync(
                def,
                number: GetIntPartOrNull(parts, 0),
                result: br,
                gitHubOrganization: GetPartOrNull(parts, 3),
                gitHubRepository: GetPartOrNull(parts, 4),
                queued: GetDatePartOrNull(parts, 2));
        }

        public async Task<ModelBuild> AddBuildAsync(ModelBuildDefinition def,
            int? number = null,
            BuildResult? result = null,
            string? gitHubOrganization = null,
            string? gitHubRepository = null,
            int? prNumber = null,
            string? targetBranch = null,
            DateTime? queued = null)
        {
            GitHubBuildInfo? gitHubBuildInfo = null;
            if (gitHubOrganization is object && gitHubRepository is object)
            {
                gitHubBuildInfo = new(gitHubOrganization, gitHubRepository, prNumber, targetBranch);
            }

            var buildAndDefinitionInfo = new BuildAndDefinitionInfo(
                def.AzureOrganization,
                def.AzureProject,
                number ?? BuildCount++,
                def.DefinitionNumber,
                def.DefinitionName,
                gitHubBuildInfo);

            var time = queued ?? DateTime.UtcNow;
            var buildResultInfo = new BuildResultInfo(
                buildAndDefinitionInfo,
                time,
                time,
                time,
                result ?? BuildResult.Succeeded);

            return await TriageContextUtil.EnsureBuildAsync(buildResultInfo);
        }

        public async Task<ModelGitHubIssue> AddGitHubIssueAsync(ModelBuild build, GitHubIssueKey? issueKey = null)
        {
            var key = issueKey ?? new GitHubIssueKey(DotNetConstants.GitHubOrganization, "roslyn", GitHubIssueCount++);
            return await TriageContextUtil.EnsureGitHubIssueAsync(build, key, saveChanges: true);
        }

        public async Task<ModelBuildDefinition> AddBuildDefinitionAsync(string definitionName, string? azureOrganization = null, string? azureProject = null, int? definitionNumber = null)
        {
            var info = new DefinitionInfo(
                azureOrganization ?? "dnceng",
                azureProject ?? "public",
                definitionNumber ?? DefinitionCount++,
                definitionName);

            return await TriageContextUtil.EnsureBuildDefinitionAsync(info);
        }

        public async Task<ModelTrackingIssue> AddTrackingIssueAsync(
            TrackingKind trackingKind,
            string? title = null,
            SearchTestsRequest? testsRequest = null,
            SearchTimelinesRequest? timelinesRequest = null,
            SearchBuildLogsRequest? buildLogsRequest = null,
            SearchHelixLogsRequest? helixLogsRequest = null,
            ModelBuildDefinition? definition = null)
        {
            var query = testsRequest?.GetQueryString();
            query ??= timelinesRequest?.GetQueryString();
            query ??= buildLogsRequest?.GetQueryString();
            query ??= helixLogsRequest?.GetQueryString();

            var trackingIssue = new ModelTrackingIssue()
            {
                TrackingKind = trackingKind,
                SearchQuery = query ?? "",
                IsActive = true,
                ModelBuildDefinition = definition,
                IssueTitle = title ?? $"Tracking Issue {trackingKind}",
                GitHubOrganization = "",
                GitHubRepository = "",
            };

            Context.ModelTrackingIssues.Add(trackingIssue);
            await Context.SaveChangesAsync();
            return trackingIssue;
        }

        public async Task<ModelTestRun> AddTestRunAsync(
            ModelBuildAttempt attempt,
            string name) =>
            await AddTestRunAsync(
                attempt,
                name,
                new (string, string?, HelixLogKind?, string?)[] { });

        public async Task<ModelTestRun> AddTestRunAsync(
            ModelBuildAttempt attempt,
            string name,
            params (string TestCaseName, string? ErrorMessage)[] testCaseInfos) =>
            await AddTestRunAsync(
                attempt,
                name,
                testCaseInfos.Select(x => (x.TestCaseName, x.ErrorMessage, (HelixLogKind?)null, (string?)null)).ToArray());

        public async Task<ModelTestRun> AddTestRunAsync(
            ModelBuildAttempt attempt,
            string name,
            params (string TestCaseName, string? ErrorMessage, HelixLogKind? Kind, string? HelixContent)[] testCaseInfos)
        {
            Debug.Assert(attempt.ModelBuildDefinition?.AzureProject is object);

            var map = new Dictionary<HelixInfo, HelixLogInfo>();
            var testCaseResults = testCaseInfos
                .Select(x =>
                {
                    HelixInfo? info = null;
                    if (x.Kind is { } kind)
                    {
                        var uri = $"https://localhost/runfo/{HelixLogCount++}/{kind}";
                        info = new HelixInfo(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                        var logInfo = new HelixLogInfo(kind, uri);
                        map[info.Value] = logInfo;
                        Debug.Assert(x.HelixContent is object);
                        TestableHttpMessageHandler.AddRaw(uri, x.HelixContent);
                    }

                    var testCaseResult = new TestCaseResult()
                    {
                        TestCaseTitle = x.TestCaseName,
                        ErrorMessage = x.ErrorMessage,
                        Outcome = "",
                    };

                    return new DotNetTestCaseResult(testCaseResult, info);
                })
                .ToReadOnlyCollection();

            var dotNetTestRun = new DotNetTestRun(
                attempt.ModelBuildDefinition!.AzureProject,
                TestRunCount++,
                name,
                testCaseResults);

            return await TriageContextUtil.EnsureTestRunAsync(
                attempt,
                dotNetTestRun,
                map);
        }

        public async Task<ModelTrackingIssueMatch> AddTrackingMatchAsync(
            ModelTrackingIssue trackingIssue,
            ModelBuildAttempt attempt,
            ModelTimelineIssue? timelineIssue = null,
            ModelTestResult? testResult = null,
            string? helixLogUri = null)
        {
            var match = new ModelTrackingIssueMatch()
            {
                ModelTrackingIssue = trackingIssue,
                ModelBuildAttempt = attempt,
                ModelTimelineIssue = timelineIssue,
                ModelTestResult = testResult,
                HelixLogUri = helixLogUri,
                JobName = "",
            };
            Context.ModelTrackingIssueMatches.Add(match);
            await Context.SaveChangesAsync();
            return match;
        }

        public async Task<ModelTrackingIssueResult> AddTrackingResultAsync(
            ModelTrackingIssue trackingIssue,
            ModelBuildAttempt attempt,
            bool isPresent = true)
        {
            var result = new ModelTrackingIssueResult()
            {
                ModelTrackingIssue = trackingIssue,
                ModelBuildAttempt = attempt,
                IsPresent = isPresent,
            };
            Context.ModelTrackingIssueResults.Add(result);
            await Context.SaveChangesAsync();
            return result;
        }

        private static string? GetPartOrNull(string[] parts, int index) => parts.Length > index && !string.IsNullOrEmpty(parts[index]) ? parts[index] : null;
        private static int? GetIntPartOrNull(string[] parts, int index) => GetPartOrNull(parts, index) is { } p ? int.Parse(p) : null;
        private static DateTime? GetDatePartOrNull(string[] parts, int index) => GetPartOrNull(parts, index) is { } dt ? DateTime.ParseExact(dt, "yyyy-MM-dd", null) : null;

        public async Task TriageAll()
        {
            var util = new TrackingIssueUtil(HelixServer, QueryUtil, TriageContextUtil, TestableLogger);
            foreach (var modelBuildAttempt in await Context.ModelBuildAttempts.Include(x => x.ModelBuild).ToListAsync())
            {
                await util.TriageAsync(modelBuildAttempt.GetBuildAttemptKey());
            }
        }
    }
}
