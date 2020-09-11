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
    public sealed class TrackingIssueUtilTests : StandardTestBase
    {
        public TrackingIssueUtil TrackingIssueUtil { get; }

        public TrackingIssueUtilTests()
        {
            TrackingIssueUtil = new TrackingIssueUtil(QueryUtil, TriageContextUtil, TestableLogger);
        }

        [Fact]
        public async Task SimpleTimelineSearh()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|dotnet|roslyn", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue("Timeline|dog");
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt);
            var match = await Context.ModelTrackingIssueMatches.SingleAsync();
            Assert.Equal(tracking.Id, match.ModelTrackingIssueId);
            Assert.Equal(timeline.Id, match.ModelTimelineIssueId);

            var result = await Context.ModelTrackingIssueResults.SingleAsync();
            Assert.True(result.IsPresent);
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

            var result = await Context.ModelTrackingIssueResults.SingleAsync();
            Assert.True(result.IsPresent);
        }
    }
}
