using DevOps.Util.DotNet;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.Xml;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class ReportBuilderTests
    {
        private static BuildAndDefinitionInfo CreateBuildInfo(
            int buildNumber = 42,
            int definitionId = 13,
            string definitionName = "def",
            string organization = "devopsutil",
            string project = "test") =>
            new BuildAndDefinitionInfo(
                organization,
                project,
                buildNumber,
                definitionId,
                definitionName,
                gitHubBuildInfo: null);

        private static HelixLogInfo CreateHelixLogInfo(string? consoleUri = null, string? runClientUri = null) =>
            new HelixLogInfo(consoleUri: consoleUri, runClientUri: runClientUri, coreDumpUri: null, testResultsUri: null);

        [Fact]
        public void SimpleBuild()
        {
            var expected = @"
|Build|Definition|Kind|Run Name|
|---|---|---|---|
|[1](https://dev.azure.com/devopsutil/test/_build/results?buildId=1)|[def](https://devopsutil.visualstudio.com/test/_build?definitionId=13)|Rolling|job1|
|[2](https://dev.azure.com/devopsutil/test/_build/results?buildId=2)|[def](https://devopsutil.visualstudio.com/test/_build?definitionId=13)|Rolling|job2|
";

            var results = new (BuildAndDefinitionInfo, string?, HelixLogInfo?)[]
            {
                (CreateBuildInfo(buildNumber: 1), "job1", null),
                (CreateBuildInfo(buildNumber: 2), "job2", null)
            };

            var builder = new ReportBuilder();
            var report = builder.BuildSearchTests(results, includeDefinition: true, includeHelix: false);
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());
        }

        [Fact]
        public void SimpleHelix()
        {
            var expected = @"
|Build|Definition|Kind|Run Name|Console|Core Dump|Test Results|Run Client|
|---|---|---|---|---|---|---|---|
|[1](https://dev.azure.com/devopsutil/test/_build/results?buildId=1)|[def](https://devopsutil.visualstudio.com/test/_build?definitionId=13)|Rolling|job1|[console.log](https://build/job1)||||
|[2](https://dev.azure.com/devopsutil/test/_build/results?buildId=2)|[def](https://devopsutil.visualstudio.com/test/_build?definitionId=13)|Rolling|job2|[console.log](https://build/job2)||||
";

            var results = new (BuildAndDefinitionInfo, string?, HelixLogInfo?)[]
            {
                (CreateBuildInfo(buildNumber: 1), "job1", CreateHelixLogInfo("https://build/job1")),
                (CreateBuildInfo(buildNumber: 2), "job2", CreateHelixLogInfo("https://build/job2"))
            };

            var builder = new ReportBuilder();
            var report = builder.BuildSearchTests(results, includeDefinition: true, includeHelix: true);
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());
        }
    }
}
