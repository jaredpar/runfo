using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class StandardTestBase : IDisposable
    {
        public DbConnection Connection { get; }
        public TriageContext Context { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public DevOpsServer Server { get; set; }
        public DotNetQueryUtil QueryUtil { get; set; }
        public TestableHttpMessageHandler TestableHttpMessageHandler { get; set; }
        public TestableLogger TestableLogger { get; set; }
        public TestableGitHubClientFactory TestableGitHubClientFactory { get; set; }
        public TestableGitHubClient TestableGitHubClient => TestableGitHubClientFactory.TestableGitHubClient;

        public StandardTestBase()
        {
            Connection = CreateInMemoryDatabase();
            var options = new DbContextOptionsBuilder<TriageContext>()
                .UseSqlite(Connection)
                .Options;
            Context = new TriageContext(options);
            TriageContextUtil = new TriageContextUtil(Context);
            TestableHttpMessageHandler = new TestableHttpMessageHandler();
            TestableLogger = new TestableLogger();
            TestableGitHubClientFactory = new TestableGitHubClientFactory();
            Server = new DevOpsServer("random", httpClient: new HttpClient(TestableHttpMessageHandler));
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
            var number = int.Parse(parts[0]);
            var build = new ModelBuild()
            {
                Id = TriageContextUtil.GetModelBuildId(new BuildKey(def.AzureOrganization, def.AzureProject, number)),
                BuildNumber = number,
                GitHubOrganization = parts.Length > 1 ? parts[1] : null,
                GitHubRepository = parts.Length > 2 ? parts[2] : null,
                ModelBuildDefinition = def,
            };

            Context.ModelBuilds.Add(build);
            return build;
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

        public ModelTrackingIssue AddTrackingIssue(string data, ModelBuildDefinition? definition = null)
        {
            var parts = data.Split("|");
            var trackingIssue = new ModelTrackingIssue()
            {
                TrackingKind = (TrackingKind)Enum.Parse(typeof(TrackingKind), parts[0]),
                SearchQuery = parts[1],
                IsActive = true,
                ModelBuildDefinition = definition,
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
                IsHelixTestResult = parts.Length > 1 ? bool.Parse(parts[1]) : false,
                HelixConsoleUri = parts.Length > 2 ? parts[2] : null,
                HelixRunClientUri = parts.Length > 3 ? parts[3] : null,
                ModelTestRun = testRun,
                ModelBuild = testRun.ModelBuild,
            };
            Context.ModelTestResults.Add(testResult);
            return testResult;
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
    }
}
