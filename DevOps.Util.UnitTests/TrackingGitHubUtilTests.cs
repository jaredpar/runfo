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
    public sealed class TrackingGitHubUtilTests : StandardTestBase
    {
        public TrackingGitHubUtil TrackingGitHubUtil { get; }

        public TrackingGitHubUtilTests()
        {
            TrackingGitHubUtil = new TrackingGitHubUtil(TestableGitHubClientFactory, Context, TestableLogger);
        }

        [Fact]
        public async Task SimpleTimelineSearh()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            var attempt = AddAttempt(1, AddBuild("1|dotnet|roslyn", def));
            var timeline = AddTimelineIssue("windows|dog", attempt);
            var tracking = AddTrackingIssue("Timeline|dog");
            var match = AddTrackingMatch(tracking, attempt, timelineIssue: timeline);
            var result = AddTrackingResult(tracking, attempt);
            await Context.SaveChangesAsync();

            var expected = @"
|Definition|Build|Kind|Job Name|
|---|---|---|---|
|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|Rolling|windows|

";

            var report = await TrackingGitHubUtil.GetReportAsync(tracking, includeMarkers: false);
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
            var tracking = AddTrackingIssue("Test|Util");
            await Context.SaveChangesAsync();
            await TriageAll();

            var expected = @"
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

            var report = await TrackingGitHubUtil.GetReportAsync(tracking, includeMarkers: false, baseTime: new DateTime(year: 2020, month: 08, day: 1));
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());

            void AddTestData(int buildNumber, string dateStr)
            {
                var attempt = AddAttempt(1, AddBuild($"{buildNumber}|dotnet|roslyn|{dateStr}", def));
                var testRun = AddTestRun("windows", attempt.ModelBuild);
                AddTestResult("Util.Test1", testRun);
                AddTestResult("Util.Test2", testRun);
            }
        }

    }
}
