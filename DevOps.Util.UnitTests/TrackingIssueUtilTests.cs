using DevOps.Util.DotNet;
using DevOps.Util.Triage;
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
    public sealed class TrackingIssueUtilTests : IDisposable
    {
        private readonly DbConnection _connection;
        private readonly TriageContext _context;
        private readonly TrackingIssueUtil _trackingIssueUtil;

        public TrackingIssueUtilTests()
        {
            _connection = CreateInMemoryDatabase();
            var options = new DbContextOptionsBuilder<TriageContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new TriageContext(options);
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
            _trackingIssueUtil = new TrackingIssueUtil(
                new DotNetQueryUtil(new DevOpsServer("random", httpClient: new HttpClient(new TestableHttpMessageHandler()))),
                new TriageContextUtil(_context),
                new TestableLogger());
        }

        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            return connection;
        }

        public void Dispose() => _connection.Dispose();

        private ModelTimelineIssue AddTimelineIssue(string data, ModelBuildAttempt attempt)
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
            _context.ModelTimelineIssues.Add(issue);
            return issue;
        }

        private ModelBuildAttempt AddAttempt(int attempt, ModelBuild build)
        {
            var modelAttempt = new ModelBuildAttempt()
            {
                Attempt = attempt,
                ModelBuild = build
            };

            _context.ModelBuildAttempts.Add(modelAttempt);
            return modelAttempt;
        }

        private ModelBuild AddBuild(string data, ModelBuildDefinition def)
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

            _context.ModelBuilds.Add(build);
            return build;
        }

        private ModelBuildDefinition AddBuildDefinition(string data)
        {
            var parts = data.Split("|");
            var def = new ModelBuildDefinition()
            {
                AzureOrganization = parts[0],
                AzureProject = parts[1],
                DefinitionName = parts[2],
                DefinitionId = int.Parse(parts[3]),
            };
            _context.ModelBuildDefinitions.Add(def);
            return def;
        }

        private ModelTrackingIssue AddTrackingIssue(string data)
        {
            var parts = data.Split("|");
            var trackingIssue = new ModelTrackingIssue()
            {
                TrackingKind = (TrackingKind)Enum.Parse(typeof(TrackingKind), parts[0]),
                SearchRegexText = parts[1],
                IsActive = true,
            };
            _context.ModelTrackingIssues.Add(trackingIssue);
            return trackingIssue;
        }

        [Fact]
        public async Task SimpleTimelineSearh()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|dotnet|roslyn", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue("Timeline|dog");
            await _context.SaveChangesAsync();

            await _trackingIssueUtil.TriageAsync(attempt);
            var match = await _context.ModelTrackingIssueMatches.SingleAsync();
            Assert.Equal(tracking.Id, match.ModelTrackingIssueId);
            Assert.Equal(timeline.Id, match.ModelTimelineIssueId);

            var result = await _context.ModelTrackingIssueResults.SingleAsync();
            Assert.True(result.IsPresent);
        }

    }
}
