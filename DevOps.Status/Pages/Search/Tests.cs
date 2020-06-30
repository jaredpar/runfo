using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages.Search
{
    public class TestsModel : PageModel
    {
        public class TestInfo
        {
            public string TestName { get; set; }

            public string CollapseName { get; set; }

            public List<TestResultInfo> Results { get; } = new List<TestResultInfo>();
        }

        public class TestResultInfo
        {
            public int BuildNumber { get; set; }

            public string BuildUri { get; set; }

            public string HelixConsoleUri { get; set; }
        }

        public DevOpsServer Server { get; }

        [BindProperty(SupportsGet = true)]
        public string Query { get; set; }

        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TestsModel(DevOpsServer server)
        {
            Server = server;
        }

        public async Task OnGet()
        {
            if (string.IsNullOrEmpty(Query))
            {
                return;
            }

            var queryUtil = new DotNetQueryUtil(Server);
            var testRuns = new List<DotNetTestRun>();
            var builds = await queryUtil.ListBuildsAsync(Query);
            foreach (var build in builds)
            {
                var result = await queryUtil.ListDotNetTestRunsAsync(build, DotNetUtil.FailedTestOutcomes);
                testRuns.AddRange(result);
            }

            var count = 0;
            foreach (var group in testRuns.SelectMany(x => x.TestCaseResults).GroupBy(x => x.TestCaseTitle))
            {
                count++;

                var testInfo = new TestInfo()
                {
                    TestName = group.Key,
                    CollapseName = $"collapse{count}",
                };

                foreach (var item in group)
                {
                    var testResultInfo = new TestResultInfo()
                    {
                        BuildNumber = item.Build.Id,
                        BuildUri = DevOpsUtil.GetBuildUri(item.Build),
                    };
                    testInfo.Results.Add(testResultInfo);
                }

                TestInfos.Add(testInfo);
            }
        }
    }
}