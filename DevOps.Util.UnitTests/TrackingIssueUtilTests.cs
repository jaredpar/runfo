using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class TrackingIssueUtilTests : StandardTestBase
    {
        public TrackingIssueUtil TrackingIssueUtil { get; }

        public TrackingIssueUtilTests()
        {
            TrackingIssueUtil = new TrackingIssueUtil(QueryUtil, TriageContextUtil, TestableLogger);
        }

        [Fact]
        public async Task TimelineSearchSimple()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|dotnet|roslyn", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue("Timeline|#dog");
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt);
            var match = await Context.ModelTrackingIssueMatches.SingleAsync();
            Assert.Equal(tracking.Id, match.ModelTrackingIssueId);
            Assert.Equal(timeline.Id, match.ModelTimelineIssueId);

            var result = await Context.ModelTrackingIssueResults.SingleAsync();
            Assert.True(result.IsPresent);
        }

        [Fact]
        public async Task TimelineSearchFilterToDefinition()
        {
            var def1 = AddBuildDefinition("dnceng|public|roslyn|42");
            var def2 = AddBuildDefinition("dnceng|public|runtime|13");
            var attempt1 = AddAttempt(1, AddBuild("1|dotnet|roslyn", def1));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2|dotnet|roslyn", def2));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue("Timeline|#dog", def2);
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1);
            await TrackingIssueUtil.TriageAsync(attempt2);
            var results = await Context.ModelTrackingIssueResults.ToListAsync();
            Assert.Single(results);
            Assert.Equal(attempt2.Id, results.Single().ModelBuildAttemptId);
        }

        [Fact]
        public async Task TimelineSavesJobName()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|dotnet|roslyn", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue("Timeline|#dog");
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt);
            var match = await Context.ModelTrackingIssueMatches.SingleAsync();
            Assert.Equal("windows", match.JobName);
        }

        [Theory]
        [InlineData("Test1", 1)]
        [InlineData("Test2", 1)]
        [InlineData("Test", 2)]
        public async Task SimpleTestSearch(string search, int count)
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|dotnet|roslyn", def));
            var testRun = AddTestRun("windows", attempt.ModelBuild);
            var testResult1 = AddTestResult("Util.Test1", testRun);
            var testResult2 = AddTestResult("Util.Test2", testRun);
            var tracking = AddTrackingIssue($"Test|{search}");
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt);
            var matches = await Context.ModelTrackingIssueMatches.ToListAsync();
            Assert.Equal(count, matches.Count);
            Assert.True(matches.All(x => x.JobName == "windows"));

            var result = await Context.ModelTrackingIssueResults.SingleAsync();
            Assert.True(result.IsPresent);
        }

        [Fact]
        public async Task TriageTrackingIssueWrongDefinition()
        {
            var def1 = AddBuildDefinition("dnceng|public|roslyn|42");
            var def2 = AddBuildDefinition("dnceng|public|runtime|13");
            var attempt1 = AddAttempt(1, AddBuild("1|dotnet|roslyn", def1));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2|dotnet|roslyn", def2));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue("Timeline|#dog", def2);
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1.GetBuildAttemptKey(), tracking.Id);
            await TrackingIssueUtil.TriageAsync(attempt2.GetBuildAttemptKey(), tracking.Id);
            var results = await Context.ModelTrackingIssueResults.ToListAsync();
            Assert.Single(results);
            Assert.Equal(attempt2.Id, results.Single().ModelBuildAttemptId);
        }

        [Fact]
        public async Task TriageTrackingIssueNoDefinition()
        {
            var def1 = AddBuildDefinition("dnceng|public|roslyn|42");
            var def2 = AddBuildDefinition("dnceng|public|runtime|13");
            var attempt1 = AddAttempt(1, AddBuild("1|dotnet|roslyn", def1));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2|dotnet|roslyn", def2));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue("Timeline|#dog");
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1.GetBuildAttemptKey(), tracking.Id);
            await TrackingIssueUtil.TriageAsync(attempt2.GetBuildAttemptKey(), tracking.Id);
            var results = await Context.ModelTrackingIssueResults.ToListAsync();
            Assert.Equal(2, results.Count);
        }
    }
}
