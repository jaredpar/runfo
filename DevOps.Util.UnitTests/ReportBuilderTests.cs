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
        private static BuildInfo CreateBuildInfo(
            int buildNumber = 42,
            int definitionId = 13,
            string definitionName = "def",
            string organization = "devopsutil",
            string project = "test",
            BuildResult result = BuildResult.Succeeded) =>
            new BuildInfo(
                new BuildKey(organization, project, buildNumber),
                new BuildDefinitionInfo(organization, project, definitionId, definitionName),
                gitHubInfo: null,
                startTime: null,
                finishTime: null,
                result);

        [Fact]
        public void SimpleBuild()
        {
            var expected = @"
<!-- runfo report start -->
|Build|Definition|Kind|Run Name|
|---|---|---|---|
|[1](https://dev.azure.com/devopsutil/test/_build/results?buildId=1)|[def](https://devopsutil.visualstudio.com/test/_build?definitionId=13)|Rolling|job1|
|[2](https://dev.azure.com/devopsutil/test/_build/results?buildId=2)|[def](https://devopsutil.visualstudio.com/test/_build?definitionId=13)|Rolling|job2|

<!-- runfo report end -->
";

            var results = new (BuildInfo, string?, HelixLogInfo?)[]
            {
                (CreateBuildInfo(buildNumber: 1), "job1", null),
                (CreateBuildInfo(buildNumber: 2), "job2", null)
            };

            var builder = new ReportBuilder();
            var report = builder.BuildSearchTests(results, includeDefinition: true, includeHelix: false);
            Assert.Equal(expected.TrimNewlines(), report.TrimNewlines());
        }
    }
}
