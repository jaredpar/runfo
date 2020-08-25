using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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

            public bool IncludeHelixColumns { get; set; }

            public bool IncludeKindColumn { get; set; }
        }

        public class TestResultInfo
        {
            public int BuildNumber { get; set; }

            public string? TestRun { get; set; }

            public string? Kind { get; set; }

            public string? BuildUri { get; set; }

            public string? HelixConsoleUri { get; set; }

            public string? HelixRunClientUri { get; set; }

            public string? HelixCoreDumpUri { get; set; }

            public string? HelixTestResultsUri { get; set; }
        }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TestsQuery { get; set; }

        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TestsModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery))
            {
                if (string.IsNullOrEmpty(BuildQuery))
                {
                    BuildQuery = new StatusBuildSearchOptions() { Definition = "runtime" }.GetUserQueryString();
                }

                return Page();
            }

            var buildSearchOptions = new StatusBuildSearchOptions()
            {
                Count = 50,
            };
            buildSearchOptions.Parse(BuildQuery);
            var testSearchOptions = new StatusTestSearchOptions();
            testSearchOptions.Parse(TestsQuery ?? "");

            var query = testSearchOptions.GetModelTestResultsQuery(
                TriageContextUtil,
                buildSearchOptions.GetModelBuildsQuery(TriageContextUtil))
                .Include(x => x.ModelTestRun)
                .Include(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition);

            var results = await query.ToListAsync();
            var count = 0;
            foreach (var group in results.GroupBy(x => x.TestFullName).OrderByDescending(x => x.Count()))
            {
                count++;

                var testInfo = new TestInfo()
                {
                    TestName = group.Key,
                    CollapseName = $"collapse{count}",
                    IncludeKindColumn = buildSearchOptions.Kind == ModelBuildKind.All,
                };

                var anyHelix = false;
                foreach (var item in group)
                {
                    if (item.IsHelixTestResult)
                    {
                        anyHelix = true;
                    }

                    var testResultInfo = new TestResultInfo()
                    {
                        BuildNumber = item.ModelBuild.BuildNumber,
                        BuildUri = DevOpsUtil.GetBuildUri(item.ModelBuild.ModelBuildDefinition.AzureOrganization, item.ModelBuild.ModelBuildDefinition.AzureProject, item.ModelBuild.BuildNumber),
                        Kind = item.ModelBuild.GetModelBuildKind().GetDisplayString(),
                        TestRun = item.ModelTestRun.Name,
                        HelixConsoleUri = item.HelixConsoleUri,
                        HelixRunClientUri = item.HelixRunClientUri,
                        HelixCoreDumpUri = item.HelixCoreDumpUri,
                        HelixTestResultsUri = item.HelixTestResultsUri,
                    };
                    testInfo.Results.Add(testResultInfo);
                }

                testInfo.IncludeHelixColumns = anyHelix;
                TestInfos.Add(testInfo);
            }

            return Page();
        }
    }
}