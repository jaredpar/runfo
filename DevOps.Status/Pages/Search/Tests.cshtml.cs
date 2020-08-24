using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Status.Util;
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
            public string? TestName { get; set; }

            public string? CollapseName { get; set; }

            public List<TestResultInfo> Results { get; } = new List<TestResultInfo>();
        }

        public class TestResultInfo
        {
            public int BuildNumber { get; set; }

            public string? BuildUri { get; set; }

            public string? HelixConsoleUri { get; set; }

            public string? HelixRunClientUri { get; set; }

            public string? HelixCoreDumpUri { get; set; }

            public string? HelixTestResultsUri { get; set; }
        }

        public sealed class TestSearchOptionSet : BuildSearchOptionSet
        {
            public string? TestName { get; set; }

            public TestSearchOptionSet()
            {
                Add("n|name=", "Test name to filter to", t => TestName = t);
            }
        }

        public DotNetQueryUtilFactory QueryUtilFactory { get; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? QueryString { get; set; }

        public string? InitialSearchText { get; set; }

        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TestsModel(DotNetQueryUtilFactory factory)
        {
            QueryUtilFactory = factory;
        }

        public async Task<IActionResult> OnGet()
        {
            // No query string, this is an initial page load. Just set the default and 
            // let the user specify the search.
            if (!HttpContext.Request.QueryString.HasValue)
            {
                return Page();
            }

            var testRuns = new List<DotNetTestRun>();
            var testSearchOptionSet = CreateTestSearchOptionSet();
            var queryUtil = QueryUtilFactory.DotNetQueryUtil;
            var builds = await queryUtil.ListBuildsAsync(testSearchOptionSet);

            foreach (var build in builds)
            {
                var result = await queryUtil.ListDotNetTestRunsAsync(build, DotNetUtil.FailedTestOutcomes);
                testRuns.AddRange(result);
            }

            var testCaseResults = testRuns.SelectMany(x => x.TestCaseResults).ToList();
            FilterTestName();

            var helixMap = await queryUtil.Server.GetHelixMapAsync(testCaseResults);
            var count = 0;
            foreach (var group in testCaseResults.GroupBy(x => x.TestCaseTitle).OrderByDescending(x => x.Count()))
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

            void FilterTestName()
            {
                if (string.IsNullOrEmpty(testSearchOptionSet.TestName))
                {
                    return;
                }

                var regex = new Regex(testSearchOptionSet.TestName, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                testCaseResults.RemoveAll(x => !regex.IsMatch(x.TestCaseTitle));
            }
        }

        private TestSearchOptionSet CreateTestSearchOptionSet()
        {
            var optionSet = new TestSearchOptionSet();
            if (!string.IsNullOrEmpty(QueryString) &&
                optionSet.Parse(DotNetQueryUtil.TokenizeQuery(QueryString)).Count != 0)
            {
                throw OptionSetUtil.CreateBadOptionException();
            }

            return optionSet;
        }
    }
}