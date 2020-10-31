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

        public ModelTimelineIssue AddTimelineIssue(string data, ModelBuildAttempt attempt)
        {
            var parts = data.Split("|");
            var issue = new ModelTimelineIssue()
            {
                Attempt = attempt.Attempt,
                JobName = parts[0],
                Message = parts[1],
                RecordName = parts.Length > 2 ? parts[2] : null,
                ModelBuildAttempt = attempt,
                ModelBuild = attempt.ModelBuild,
            };
            Context.ModelTimelineIssues.Add(issue);
            return issue;
        }

        public ModelBuildAttempt AddAttempt(int attempt, ModelBuild build)
        {
            var modelAttempt = new ModelBuildAttempt()
            {
                Attempt = attempt,
                ModelBuild = build
            };

            Context.ModelBuildAttempts.Add(modelAttempt);
            return modelAttempt;
        }

        public ModelBuild AddBuild(string data, ModelBuildDefinition def)
        {
            var parts = data.Split("|");
            var number = GetPartOrNull(parts, 0) is { } part ? int.Parse(part) : BuildCount++;
            var dt = GetPartOrNull(parts, 3);
            var br = GetPartOrNull(parts, 4);

            var build = new ModelBuild()
            {
                Id = TriageContextUtil.GetModelBuildId(new BuildKey(def.AzureOrganization, def.AzureProject, number)),
                BuildNumber = number,
                GitHubOrganization = GetPartOrNull(parts, 1),
                GitHubRepository = GetPartOrNull(parts, 2),
                AzureOrganization = def.AzureOrganization,
                AzureProject = def.AzureProject,
                QueueTime = dt is object ? DateTime.ParseExact(dt, "yyyy-MM-dd", null) : (DateTime?)null,
                BuildResult = br is object ? Enum.Parse<BuildResult>(br) : (BuildResult?)null,
                ModelBuildDefinition = def,
                DefinitionId = def.DefinitionId,
                DefinitionName = def.DefinitionName,
            };

            Context.ModelBuilds.Add(build);
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
                Organization = GetPartOrNull(parts, 0) ?? DotNetUtil.GitHubOrganization,
                Repository = GetPartOrNull(parts, 1) ?? "roslyn",
                Number = GetPartOrNull(parts, 2) is { } part ? int.Parse(part) : GitHubIssueCount++,
                ModelBuild = build,
            };

            Context.ModelGitHubIssues.Add(issue);
            return issue;
        }

        public ModelBuildDefinition AddBuildDefinition(string data)
        {
            var parts = data.Split("|");
            var def = new ModelBuildDefinition()
            {
                AzureOrganization = parts[0],
                AzureProject = parts[1],
                DefinitionName = parts[2],
                DefinitionId = int.Parse(parts[3]),
            };
            Context.ModelBuildDefinitions.Add(def);
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
                SearchQuery = query,
                IsActive = true,
                ModelBuildDefinition = definition,
                IssueTitle = title ?? $"Tracking Issue {trackingKind}",
            };

            Context.ModelTrackingIssues.Add(trackingIssue);
            return trackingIssue;
        }

        public ModelTestRun AddTestRun(string data, ModelBuild build)
        {
            var parts = data.Split("|");
            var testRun = new ModelTestRun()
            {
                Name = parts[0],
                Attempt = parts.Length > 1 ? int.Parse(parts[1]) : 1,
                TestRunId = parts.Length > 2 ? int.Parse(parts[2]) : TestRunCount++,
                AzureOrganization = build.ModelBuildDefinition.AzureOrganization,
                AzureProject = build.ModelBuildDefinition.AzureProject,
                ModelBuild = build,
            };
            Context.ModelTestRuns.Add(testRun);
            return testRun;
        }

        public ModelTestResult AddTestResult(string data, ModelTestRun testRun)
        {
            var parts = data.Split("|");
            var testResult = new ModelTestResult()
            {
                TestFullName = parts[0],
                IsHelixTestResult = GetPartOrNull(parts, 1) is { } s ? bool.Parse(s) : false,
                HelixConsoleUri = GetPartOrNull(parts, 2),
                HelixRunClientUri = GetPartOrNull(parts, 3),
                ErrorMessage = GetPartOrNull(parts, 4),
                ModelTestRun = testRun,
                ModelBuild = testRun.ModelBuild,
            };
            Context.ModelTestResults.Add(testResult);
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
            };
            Context.ModelTrackingIssueMatches.Add(match);
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
