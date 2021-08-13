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
        public TriageContextUtil TriageContextUtil { get; }
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

        public TriageContext Context => DatabaseFixture.TriageContext;

        public StandardTestBase(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
        {
            databaseFixture.AssertEmpty();
            DatabaseFixture = databaseFixture;
            TriageContextUtil = new TriageContextUtil(Context);
            TestableHttpMessageHandler = new TestableHttpMessageHandler();
            TestableLogger = new TestableLogger(testOutputHelper);
            TestableGitHubClientFactory = new TestableGitHubClientFactory();

            var httpClient = new HttpClient(TestableHttpMessageHandler);
            Server = new DevOpsServer("random", httpClient: httpClient);
            HelixServer = new HelixServer(httpClient: httpClient);
            QueryUtil = new DotNetQueryUtil(Server);
        }

        public void Dispose() => DatabaseFixture.TestCompletion();

        /// <summary>
        /// |job name|message|record name|
        /// </summary>
        public ModelTimelineIssue AddTimelineIssue(string data, ModelBuildAttempt attempt)
        {
            var parts = data.Split("|");
            var issue = new ModelTimelineIssue()
            {
                StartTime = attempt.StartTime,
                Attempt = attempt.Attempt,
                JobName = parts[0],
                Message = parts[1],
                RecordName = parts.Length > 2 ? parts[2] : "",
                TaskName = "",
                DefinitionNumber = attempt.ModelBuildDefinition.DefinitionNumber,
                DefinitionName = attempt.ModelBuildDefinition.DefinitionName,
                ModelBuild = attempt.ModelBuild,
                ModelBuildAttempt = attempt,
                ModelBuildDefinition = attempt.ModelBuildDefinition,
                BuildKind = ModelBuildKind.Rolling,
            };
            Context.ModelTimelineIssues.Add(issue);
            Context.SaveChanges();
            return issue;
        }

        public ModelBuildAttempt AddAttempt(int attempt, ModelBuild build)
        {
            var modelAttempt = new ModelBuildAttempt()
            {
                StartTime = build.StartTime,
                Attempt = attempt,
                NameKey = build.NameKey,
                DefinitionName = build.DefinitionName,
                ModelBuildDefinition = build.ModelBuildDefinition,
                ModelBuild = build,
                BuildKind = ModelBuildKind.Rolling, 
            };

            Context.ModelBuildAttempts.Add(modelAttempt);
            Context.SaveChanges();
            return modelAttempt;
        }

        /// <summary>
        /// | number | result | date | github org | github repo |
        /// </summary>
        public ModelBuild AddBuild(string data, ModelBuildDefinition def)
        {
            var parts = data.Split("|");
            ModelBuildResult? br = GetPartOrNull(parts, 1) is { } p ? Enum.Parse<ModelBuildResult>(p) : null;
            return AddBuild(
                def,
                number: GetIntPartOrNull(parts, 0),
                result: br,
                gitHubOrganization: GetPartOrNull(parts, 3),
                gitHubRepository: GetPartOrNull(parts, 4),
                started: GetDatePartOrNull(parts, 2));
        }

        /// <summary>
        /// | number | result | date | github org | github repo |
        /// </summary>
        public ModelBuild AddBuild(ModelBuildDefinition def,
            int? number = null,
            ModelBuildResult? result = null,
            string? gitHubOrganization = null,
            string? gitHubRepository = null,
            DateTime? started = null)

        {
            number ??= BuildCount++;
            started ??= DateTime.UtcNow;

            var build = new ModelBuild()
            {
                NameKey = new BuildKey(def.AzureOrganization, def.AzureProject, number.Value).NameKey,
                BuildNumber = number.Value,
                GitHubOrganization = gitHubOrganization ?? "dotnet",
                GitHubRepository = gitHubRepository ?? "roslyn",
                AzureOrganization = def.AzureOrganization,
                AzureProject = def.AzureProject,
                QueueTime = started.Value,
                StartTime = started.Value,
                BuildResult = result ?? default,
                ModelBuildDefinition = def,
                ModelBuildDefinitionId = def.Id,
                DefinitionName = def.DefinitionName,
                DefinitionNumber = def.DefinitionNumber,
                BuildKind = ModelBuildKind.Rolling,
            };

            Context.ModelBuilds.Add(build);
            Context.SaveChanges();
            return build;
        }

        public ModelGitHubIssue AddGitHubIssue(GitHubIssueKey issueKey, ModelBuild build)
        {
            var issue = new ModelGitHubIssue()
            {
                Organization = issueKey.Organization,
                Repository = issueKey.Repository,
                Number = issueKey.Number,
                ModelBuild = build,
            };

            Context.ModelGitHubIssues.Add(issue);
            return issue;
        }

        public ModelGitHubIssue AddGitHubIssue(string data, ModelBuild build)
        {
            var parts = data.Split("|");

            var issue = new ModelGitHubIssue()
            {
                Organization = GetPartOrNull(parts, 0) ?? DotNetConstants.GitHubOrganization,
                Repository = GetPartOrNull(parts, 1) ?? "roslyn",
                Number = GetPartOrNull(parts, 2) is { } part ? int.Parse(part) : GitHubIssueCount++,
                ModelBuild = build,
            };

            Context.ModelGitHubIssues.Add(issue);
            return issue;
        }

        /// <summary>
        /// |azure org|azure project|name|number|
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public ModelBuildDefinition AddBuildDefinition(string data)
        {
            if (!data.Contains('|'))
            {
                return AddBuildDefinition(data);
            }

            var parts = data.Split("|");
            return AddBuildDefinition(
                parts[2],
                azureOrganization: GetPartOrNull(parts, 0),
                azureProject: GetPartOrNull(parts, 1),
                definitionNumber: GetIntPartOrNull(parts, 3));
        }

        public ModelBuildDefinition AddBuildDefinition(string definitionName, string? azureOrganization, string? azureProject, int? definitionNumber)
        {
            var def = new ModelBuildDefinition()
            {
                AzureOrganization = azureOrganization ?? "dnceng",
                AzureProject = azureProject ?? "public",
                DefinitionName = definitionName,
                DefinitionNumber = definitionNumber ?? DefinitionCount++,
            };

            Context.ModelBuildDefinitions.Add(def);
            Context.SaveChanges();
            return def;
        }

        public ModelTrackingIssue AddTrackingIssue(
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
            Context.SaveChanges();
            return trackingIssue;
        }

        public ModelTestRun AddTestRun(ModelBuildAttempt attempt, string name)
        {
            var testRun = new ModelTestRun()
            {
                Name = name,
                Attempt = attempt.Attempt,
                TestRunId = TestRunCount++,
                ModelBuild = attempt.ModelBuild,
                ModelBuildAttempt = attempt,
            };
            Context.ModelTestRuns.Add(testRun);
            Context.SaveChanges();
            return testRun;
        }

        /// <summary>
        /// |test name|is helix|console uri|runclient uri|error message|
        /// </summary>
        public ModelTestResult AddTestResult(string data, ModelTestRun testRun)
        {
            var parts = data.Split("|");
            var testResult = new ModelTestResult()
            {
                TestRunName = testRun.Name,
                TestFullName = parts[0],
                IsHelixTestResult = GetPartOrNull(parts, 1) is { } s ? bool.Parse(s) : false,
                HelixConsoleUri = GetPartOrNull(parts, 2),
                HelixRunClientUri = GetPartOrNull(parts, 3),
                ErrorMessage = GetPartOrNull(parts, 4) ?? "",
                Outcome = "",
                ModelTestRun = testRun,
                StartTime = testRun.ModelBuild.StartTime,
                ModelBuild = testRun.ModelBuild,
                ModelBuildAttempt = testRun.ModelBuildAttempt,
                Attempt = testRun.Attempt,
                DefinitionNumber = testRun.ModelBuild.DefinitionNumber,
                DefinitionName = testRun.ModelBuild.DefinitionName,
                ModelBuildDefinition = testRun.ModelBuild.ModelBuildDefinition,
                BuildKind = ModelBuildKind.Rolling,
            };
            Context.ModelTestResults.Add(testResult);
            Context.SaveChanges();
            return testResult;
        }

        public void AddHelixLog(ModelTestResult testResult, HelixLogKind kind, string content)
        {
            var uri = $"https://localhost/runfo/{HelixLogCount++}/{kind}";
            testResult.SetHelixLogUri(kind, uri);
            TestableHttpMessageHandler.AddRaw(uri, content);
        }

        public ModelTrackingIssueMatch AddTrackingMatch(
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
            Context.SaveChanges();
            return match;
        }

        public ModelTrackingIssueResult AddTrackingResult(
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
