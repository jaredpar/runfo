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
<!-- runfo report start -->
|Definition|Build|Kind|Job Name|
|---|---|---|---|
|[roslyn](https://dnceng.visualstudio.com/public/_build?definitionId=42)|[1](https://dev.azure.com/dnceng/public/_build/results?buildId=1)|Rolling|windows|

<!-- runfo report end -->
";

            var report = await TrackingGitHubUtil.GetReportAsync(tracking);
            Assert.Equal(expected, report.TrimNewlines());
        }
    }
}
