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

            public string HelixRunClientUri { get; set; }

            public string HelixCoreDumpUri { get; set; }

            public string HelixTestResultsUri { get; set; }
        }

        public DevOpsServer Server { get; }

        [BindProperty(SupportsGet = true)]
        public SearchInfo SearchInfo { get; set; }

        public string InitialSearchText { get; set; }

        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TestsModel(DevOpsServer server)
        {
            Server = server;
        }

        public async Task<IActionResult> OnGet()
        {
            // No query string, this is an initial page load. Just set the default and 
            // let the user specify the search.
            if (!HttpContext.Request.QueryString.HasValue)
            {
                InitialSearchText = SearchInfo.Default.GetSearchText();
                return Page();
            }

            if (!string.IsNullOrEmpty(SearchInfo.QueryString))
            {
                SearchInfo.ParseQueryString();
                var queryString = SearchInfo.CreatePrettyQueryString();
                return Redirect($"~/search/tests{queryString}");
            }

            InitialSearchText = SearchInfo.GetSearchText();
            var queryUtil = new DotNetQueryUtil(Server);
            var testRuns = new List<DotNetTestRun>();
            var builds = await queryUtil.ListBuildsAsync(SearchInfo.CreateBuildSearchOptionSet());

            foreach (var build in builds)
            {
                var result = await queryUtil.ListDotNetTestRunsAsync(build, DotNetUtil.FailedTestOutcomes);
                testRuns.AddRange(result);
            }

            var helixMap = await GetHelixMap();
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
                    if (!item.HelixInfo.HasValue || !helixMap.TryGetValue(item.HelixInfo.Value, out var logInfo))
                    {
                        logInfo = null;
                    }

                    var testResultInfo = new TestResultInfo()
                    {
                        BuildNumber = item.Build.Id,
                        BuildUri = DevOpsUtil.GetBuildUri(item.Build),
                        HelixConsoleUri = logInfo?.ConsoleUri,
                        HelixRunClientUri = logInfo?.RunClientUri,
                        HelixCoreDumpUri = logInfo?.CoreDumpUri,
                        HelixTestResultsUri = logInfo?.TestResultsUri,
                    };
                    testInfo.Results.Add(testResultInfo);
                }

                TestInfos.Add(testInfo);
            }

            return Page();

            async Task<Dictionary<HelixInfo, HelixLogInfo>> GetHelixMap()
            {
                var query = testRuns
                    .SelectMany(x => x.TestCaseResults)
                    .Where(x => x.HelixWorkItem.HasValue)
                    .Select(x => x.HelixWorkItem.Value)
                    .GroupBy(x => x.HelixInfo)
                    .ToList()
                    .AsParallel()
                    .Select(async g => (g.Key, await HelixUtil.GetHelixLogInfoAsync(Server, g.First())));
                await Task.WhenAll(query);
                return query.ToDictionary(x => x.Result.Key, x => x.Result.Item2);
            }
        }
    }
}