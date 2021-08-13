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
using Xunit.Abstractions;

namespace DevOps.Util.UnitTests
{
    [Collection(DatabaseCollection.Name)]
    public sealed class TrackingGitHubUtilTests : StandardTestBase
    {
        public TrackingGitHubUtil TrackingGitHubUtil { get; }

        public TrackingGitHubUtilTests(DatabaseFixture databaseFixture, ITestOutputHelper testOutputHelper)
            : base(databaseFixture, testOutputHelper)
        {
            TrackingGitHubUtil = new TrackingGitHubUtil(TestableGitHubClientFactory, Context, new SiteLinkUtil("localhost"), TestableLogger);
        }

        [Fact]
        public async Task SimpleTimelineSearh()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|Succeeded|2020-12-01", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue(
                TrackingKind.Timeline,
                title: "Dog Search",
                timelinesRequest: new SearchTimelinesRequest()
                {
                    Message = "dog",
                });
            var match = AddTrackingMatch(tracking, attempt, timelineIssue: timeline);
            var result = AddTrackingResult(tracking, attempt);
            await Context.SaveChangesAsync();

            var expected = $@"
Runfo Tracking Issue: [Dog Search](https://localhost/tracking/issue/{tracking.Id})
|Definition|Build|Kind|Job Name|
|---|---|---|---|
|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|Rolling|windows|



Build Result Summary
|Day Hit Count|Week Hit Count|Month Hit Count|
|---|---|---|
|0|0|0|
";

            var report = await TrackingGitHubUtil.GetTrackingIssueReport(tracking, includeMarkers: false);
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());
        }

        [Fact]
        public async Task TestWithSummary()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            AddTestData(1, "2020-08-01");
            AddTestData(2, "2020-08-01");
            AddTestData(3, "2020-07-29");
            AddTestData(4, "2020-07-29");
            AddTestData(5, "2020-07-29");
            AddTestData(6, "2020-07-29");
            AddTestData(7, "2020-07-05");
            var tracking = AddTrackingIssue(
                TrackingKind.Test,
                title: "Test Search",
                testsRequest: new SearchTestsRequest()
                {
                    Started = new DateRequestValue(DateTime.Parse("2020-7-1"), RelationalKind.GreaterThan),
                    Name = "Util",
                });

            await Context.SaveChangesAsync();
            await TriageAll();

            var expected = $@"
Runfo Tracking Issue: [Test Search](https://localhost/tracking/issue/{tracking.Id})
|Build|Definition|Kind|Run Name|
|---|---|---|---|
|[7](https://dev.azure.com/dnceng/public/_build/results?buildId=7)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[7](https://dev.azure.com/dnceng/public/_build/results?buildId=7)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[6](https://dev.azure.com/dnceng/public/_build/results?buildId=6)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[6](https://dev.azure.com/dnceng/public/_build/results?buildId=6)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[5](https://dev.azure.com/dnceng/public/_build/results?buildId=5)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[5](https://dev.azure.com/dnceng/public/_build/results?buildId=5)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[4](https://dev.azure.com/dnceng/public/_build/results?buildId=4)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[4](https://dev.azure.com/dnceng/public/_build/results?buildId=4)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[3](https://dev.azure.com/dnceng/public/_build/results?buildId=3)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[3](https://dev.azure.com/dnceng/public/_build/results?buildId=3)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[2](https://dev.azure.com/dnceng/public/_build/results?buildId=2)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[2](https://dev.azure.com/dnceng/public/_build/results?buildId=2)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|
|[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|Rolling|windows|



Build Result Summary
|Day Hit Count|Week Hit Count|Month Hit Count|
|---|---|---|
|2|6|7|

";

            var report = await TrackingGitHubUtil.GetTrackingIssueReport(tracking, includeMarkers: false, baseTime: new DateTime(year: 2020, month: 08, day: 1));
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());

            void AddTestData(int buildNumber, string dateStr)
            {
                var attempt = AddAttempt(1, AddBuild($"{buildNumber}||{dateStr}", def));
                var testRun = AddTestRun("windows", attempt);
                AddTestResult("Util.Test1", testRun);
                AddTestResult("Util.Test2", testRun);
            }
        }

        [Fact]
        public async Task AssociatedIssueReport()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var issueKey = new GitHubIssueKey("dotnet", "test", 13);

            for (int i = 0; i < 5; i++)
            {
                AddGitHubIssue(issueKey, AddBuild($"||2020-10-0{i + 1}", def));
                await Context.SaveChangesAsync();
            }

            var expected = @"
<!-- runfo report start -->
|Build|Kind|Start Time|
|---|---|---|
[0](https://dev.azure.com/dnceng/public/_build/results?buildId=0)|Rolling|2020-01-10|
[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|Rolling|2020-02-10|
[2](https://dev.azure.com/dnceng/public/_build/results?buildId=2)|Rolling|2020-03-10|
[3](https://dev.azure.com/dnceng/public/_build/results?buildId=3)|Rolling|2020-04-10|
[4](https://dev.azure.com/dnceng/public/_build/results?buildId=4)|Rolling|2020-05-10|

<!-- runfo report end -->
";

            var report = await TrackingGitHubUtil.GetAssociatedIssueReportAsync(issueKey);
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());
        }

        [Theory]
        [InlineData(HelixLogKind.Console, "Console", "console.log")]
        [InlineData(HelixLogKind.RunClient, "Run Client", "runclient.py")]
        [InlineData(HelixLogKind.TestResults, "Test Results", "test results")]
        public async Task SimpleHelixLogsReport(HelixLogKind kind, string columnText, string fileName)
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            AddTestData(1, "2020-08-01");
            AddTestData(2, "2020-08-01");
            var tracking = AddTrackingIssue(
                TrackingKind.HelixLogs,
                title: "Helix Log",
                helixLogsRequest: new SearchHelixLogsRequest()
                {
                    Started = null,
                    HelixLogKinds = { kind },
                    Text = "data",
                });

            await Context.SaveChangesAsync();
            await TriageAll();

            var expected = @$"
|Build|Kind|{columnText}|
|---|---|---|
|[2](https://dev.azure.com/dnceng/public/_build/results?buildId=2)|Rolling|[{fileName}](https://localhost/runfo/1/{kind})|
|[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|Rolling|[{fileName}](https://localhost/runfo/0/{kind})|
";

            var report = await TrackingGitHubUtil.GetTrackingIssueReport(tracking, includeMarkers: false, baseTime: new DateTime(year: 2020, month: 08, day: 1));
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());

            void AddTestData(int buildNumber, string dateStr)
            {
                var attempt = AddAttempt(1, AddBuild($"{buildNumber}||{dateStr}", def));
                var testRun = AddTestRun("windows", attempt);
                var testResult = AddTestResult("Util.Test1", testRun);
                AddHelixLog(testResult, kind, "The log data");
            }
        }
    }
}
