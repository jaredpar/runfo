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
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    public sealed class TrackingIssueUtilTests : StandardTestBase
    {
        public TrackingIssueUtil TrackingIssueUtil { get; }

        public TrackingIssueUtilTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            TrackingIssueUtil = new TrackingIssueUtil(HelixServer, QueryUtil, TriageContextUtil, TestableLogger);
        }

        public async Task<int> GetMatchCountAsync(ModelTrackingIssue trackingIssue) =>
            await Context
            .ModelTrackingIssueMatches
            .Where(x => x.ModelTrackingIssueId == trackingIssue.Id)
            .CountAsync();

        public async Task<int> GetMatchCountAsync(ModelTrackingIssue trackingIssue, ModelBuildAttempt modelBuildAttempt) =>
            await Context
            .ModelTrackingIssueMatches
            .Where(x => x.ModelTrackingIssueId == trackingIssue.Id && x.ModelBuildAttemptId == modelBuildAttempt.Id)
            .CountAsync();

        [Fact]
        public async Task TimelineSearchSimple()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" });
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
            var attempt1 = AddAttempt(1, AddBuild("1", def1));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2", def2));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" },
                definition: def2);
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
            var attempt = AddAttempt(1, AddBuild("1", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" });
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
            var attempt = AddAttempt(1, AddBuild("1", def));
            var testRun = AddTestRun("windows", attempt);
            var testResult1 = AddTestResult("Util.Test1", testRun);
            var testResult2 = AddTestResult("Util.Test2", testRun);
            var tracking = AddTrackingIssue(
                TrackingKind.Test,
                testsRequest: new SearchTestsRequest() { Name = search });
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
            var attempt1 = AddAttempt(1, AddBuild("1", def1));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2", def2));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" },
                definition: def2);
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
            var attempt1 = AddAttempt(1, AddBuild("1", def1));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2", def2));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" });
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1.GetBuildAttemptKey(), tracking.Id);
            await TrackingIssueUtil.TriageAsync(attempt2.GetBuildAttemptKey(), tracking.Id);
            var results = await Context.ModelTrackingIssueResults.ToListAsync();
            Assert.Equal(2, results.Count);
        }

        /// <summary>
        /// Make sure the triage is working for only the specific <see cref="ModelBuildAttempt"/>
        /// that is passed in. Helps validate that our search is as limited as it should be within
        /// the util
        /// </summary>
        [Fact]
        public async Task TriageTimelineIssueAttemptOnly()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt1 = AddAttempt(1, AddBuild("1", def));
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var attempt2 = AddAttempt(1, AddBuild("2", def));
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" });
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1.GetBuildAttemptKey(), tracking.Id);
            Assert.Equal(1, await Context.ModelTrackingIssueResults.CountAsync());
            await TrackingIssueUtil.TriageAsync(attempt2.GetBuildAttemptKey(), tracking.Id);
            Assert.Equal(2, await Context.ModelTrackingIssueResults.CountAsync());
        }

        [Fact]
        public async Task TriageTimelineIssueAttemptOnlyWithinBuild()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt1 = AddAttempt(1, AddBuild("1", def));
            var attempt2 = AddAttempt(2, attempt1.ModelBuild);
            var attempt3 = AddAttempt(3, attempt1.ModelBuild);
            var timeline1 = AddTimelineIssue("windows|dog", attempt1);
            var timeline2 = AddTimelineIssue("windows|dog", attempt2);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                timelinesRequest: new SearchTimelinesRequest() { Message = "#dog" });
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1.GetBuildAttemptKey(), tracking.Id);
            Assert.Equal(1, await Context.ModelTrackingIssueResults.CountAsync());
            await TrackingIssueUtil.TriageAsync(attempt2.GetBuildAttemptKey(), tracking.Id);
            Assert.Equal(2, await Context.ModelTrackingIssueResults.CountAsync());
            await TrackingIssueUtil.TriageAsync(attempt2.GetBuildAttemptKey(), tracking.Id);
            Assert.Equal(2, await Context.ModelTrackingIssueResults.CountAsync());
        }

        [Theory]
        [InlineData(HelixLogKind.Console)]
        [InlineData(HelixLogKind.RunClient)]
        [InlineData(HelixLogKind.TestResults)]
        public async Task SimpleHelixLogTrackingIssue(HelixLogKind kind)
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1", def));
            var testRun = AddTestRun("windows", attempt);
            var testResult1 = AddTestResult("Util.Test1", testRun);
            AddHelixLog(testResult1, kind, "the dog fetched the ball");
            var testResult2 = AddTestResult("Util.Test2", testRun);
            AddHelixLog(testResult2, kind, "the tree grew");

            var tracking1 = AddTrackingIssue(
                TrackingKind.HelixLogs,
                helixLogsRequest: new SearchHelixLogsRequest()
                {
                    HelixLogKinds = { kind },
                    Text = "the",
                });
            await TestSearch(tracking1, matchCount: 2, isPresent: true);

            var tracking2 = AddTrackingIssue(
                TrackingKind.HelixLogs,
                helixLogsRequest: new SearchHelixLogsRequest()
                {
                    HelixLogKinds = { kind },
                    Text = "dog",
                });
            await TestSearch(tracking2, matchCount: 1, isPresent: true);

            var tracking3 = AddTrackingIssue(
                TrackingKind.HelixLogs,
                helixLogsRequest: new SearchHelixLogsRequest()
                {
                    HelixLogKinds = { kind },
                    Text = "fish",
                });
            await TestSearch(tracking3, matchCount: 0, isPresent: false);

            async Task TestSearch(ModelTrackingIssue issue, int matchCount, bool isPresent)
            {
                await Context.SaveChangesAsync();
                await TrackingIssueUtil.TriageAsync(attempt.GetBuildAttemptKey(), issue);
                var matches = await Context.ModelTrackingIssueMatches.Where(x => x.ModelTrackingIssueId == issue.Id).ToListAsync();
                Assert.Equal(matchCount, matches.Count);
                var result = await Context.ModelTrackingIssueResults.Where(x => x.ModelTrackingIssueId == issue.Id).SingleAsync();
                Assert.Equal(isPresent, result.IsPresent);
            }

        }

        [Fact]
        public async Task TestsSearchRespectsDefinition()
        {
            var def1 = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt1 = AddAttempt(1, AddBuild("1", def1));
            var testRun1 = AddTestRun("windows", attempt1);
            AddTestResult("test1|||failed dog", testRun1);
            AddTestResult("test2|||failed cat", testRun1);

            var attempt2 = AddAttempt(1, AddBuild("2", def1));
            var testRun2 = AddTestRun("windows", attempt2);
            AddTestResult("test2|||failed dog", testRun2);
            AddTestResult("test2|||failed dog", testRun2);

            var def2 = AddBuildDefinition("dnceng|public|roslyn|13");
            var attempt3 = AddAttempt(1, AddBuild("3", def2));
            var testRun3 = AddTestRun("windows", attempt3);
            AddTestResult("test1|||failed dog", testRun3);
            AddTestResult("test2|||failed dog", testRun3);

            var tracking = AddTrackingIssue(
                TrackingKind.Test,
                testsRequest: new SearchTestsRequest() { Name = "test2" },
                definition: def1);
            await Context.SaveChangesAsync();

            await TrackingIssueUtil.TriageAsync(attempt1);
            await TrackingIssueUtil.TriageAsync(attempt2);
            await TrackingIssueUtil.TriageAsync(attempt3);
            Assert.Equal(1, await GetMatchCountAsync(tracking, attempt1));
            Assert.Equal(2, await GetMatchCountAsync(tracking, attempt2));
            Assert.Equal(0, await GetMatchCountAsync(tracking, attempt3));
        }
    }
}
