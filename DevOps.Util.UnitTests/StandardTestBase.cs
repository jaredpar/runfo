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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    public class StandardTestBase : IDisposable
    {
        public DbConnection Connection { get; }
        public TriageContext Context { get; }
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

        public StandardTestBase(ITestOutputHelper testOutputHelper)
        {
            Connection = CreateInMemoryDatabase();
            var options = new DbContextOptionsBuilder<TriageContext>()
                .UseSqlite(Connection)
                .Options;
            Context = new TriageContext(options);
            TriageContextUtil = new TriageContextUtil(Context);
            TestableHttpMessageHandler = new TestableHttpMessageHandler();
            TestableLogger = new TestableLogger(testOutputHelper);
            TestableGitHubClientFactory = new TestableGitHubClientFactory();

            var httpClient = new HttpClient(TestableHttpMessageHandler);
            Server = new DevOpsServer("random", httpClient: httpClient);
            HelixServer = new HelixServer(httpClient: httpClient);
            QueryUtil = new DotNetQueryUtil(Server);

            Context.Database.EnsureDeleted();
            Context.Database.EnsureCreated();
        }

        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            return connection;
        }

        public void Dispose() => Connection.Dispose();

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
            var number = GetPartOrNull(parts, 0) is { } part ? int.Parse(part) : BuildCount++;
            var br = GetPartOrNull(parts, 1);
            var dt = GetPartOrNull(parts, 2);
            var startTime = dt is object ? DateTime.ParseExact(dt, "yyyy-MM-dd", null) : DateTime.UtcNow;

            var build = new ModelBuild()
            {
                NameKey = TriageContextUtil.GetModelBuildNameKey(new BuildKey(def.AzureOrganization, def.AzureProject, number)),
                BuildNumber = number,
                GitHubOrganization = GetPartOrNull(parts, 1) ?? "dotnet",
                GitHubRepository = GetPartOrNull(parts, 2) ?? "roslyn",
                AzureOrganization = def.AzureOrganization,
                AzureProject = def.AzureProject,
                QueueTime = startTime,
                StartTime = startTime,
                BuildResult = br is object ? Enum.Parse<ModelBuildResult>(br) : default,
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
            var parts = data.Split("|");
            var def = new ModelBuildDefinition()
            {
                AzureOrganization = GetPartOrNull(parts, 0) ?? "dnceng",
                AzureProject = GetPartOrNull(parts, 1) ?? "public",
                DefinitionName = parts[2],
                DefinitionNumber = GetPartOrNull(parts, 3) is { } part ? int.Parse(part) : DefinitionCount++,
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

        /// <summary>
        /// |name|test run id| 
        /// </summary>
        public ModelTestRun AddTestRun(string data, ModelBuildAttempt attempt)
        {
            var parts = data.Split("|");
            var testRun = new ModelTestRun()
            {
                Name = parts[0],
                Attempt = attempt.Attempt,
                TestRunId = GetPartOrNull(parts, 1) is { }  part ? int.Parse(part) : TestRunCount++,
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
