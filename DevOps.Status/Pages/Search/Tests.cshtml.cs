using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

            public string? GitHubRepository { get; set; }
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
        public StatusGitHubClientFactory GitHubClientFactory { get; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TestsQuery { get; set; }

        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TestsModel(TriageContextUtil triageContextUtil, StatusGitHubClientFactory gitHubClientFactory)
        {
            TriageContextUtil = triageContextUtil;
            GitHubClientFactory = gitHubClientFactory;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest() { Definition = "runtime" }.GetQueryString();

                return Page();
            }

            var buildSearchOptions = GetBuildSearchOptions();
            var testSearchOptions = new SearchTestsRequest();
            testSearchOptions.ParseQueryString(TestsQuery ?? "");

            var query = testSearchOptions.GetQuery(
                TriageContextUtil,
                buildSearchOptions.GetQuery(TriageContextUtil))
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
                string? gitHubRepository = null;
                foreach (var item in group)
                {
                    anyHelix = anyHelix || item.IsHelixTestResult;
                    gitHubRepository ??= item.ModelBuild.GitHubRepository;

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
                testInfo.GitHubRepository = gitHubRepository;
                TestInfos.Add(testInfo);
            }

            return Page();
        }

        public async Task<IActionResult> OnPost(string testFullName, string gitHubRepository)
        {
            if (string.IsNullOrEmpty(testFullName) || string.IsNullOrEmpty(BuildQuery))
            {
                throw new Exception("Invalid request");
            }

            var reportText = await GetReportText();
            var gitHubApp = await GitHubClientFactory.CreateForAppAsync(DotNetUtil.GitHubOrganization, gitHubRepository);
            var newIssue = new NewIssue($"Test failures: {testFullName}")
            {
                Body = reportText,
            };
            var issue = await gitHubApp.Issue.Create(DotNetUtil.GitHubOrganization, gitHubRepository, newIssue);
            return Redirect(issue.HtmlUrl);

            async Task<string> GetReportText()
            {
                var buildSearchOptions = GetBuildSearchOptions();
                var testSearchOptions = new SearchTestsRequest()
                {
                    Name = testFullName,
                };
                var query = testSearchOptions.GetQuery(
                    TriageContextUtil,
                    buildSearchOptions.GetQuery(TriageContextUtil))
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition)
                    .Include(x => x.ModelTestRun);

                var results = new List<(BuildInfo BuildInfo, string? TestRunName, HelixLogInfo? LogInfo)>();
                var includeHelix = false;
                foreach (var item in await query.ToListAsync())
                {
                    var buildInfo = item.ModelBuild.GetBuildInfo();
                    var helixLogInfo = item.GetHelixLogInfo();
                    includeHelix = includeHelix || helixLogInfo is object;
                    results.Add((buildInfo, item.ModelTestRun.Name, helixLogInfo));
                }

                var builder = new ReportBuilder();
                return builder.BuildSearchTests(
                    results,
                    includeDefinition: !buildSearchOptions.HasDefinition,
                    includeHelix: includeHelix);
            }
        }

        private SearchBuildsRequest GetBuildSearchOptions()
        {
            Debug.Assert(!string.IsNullOrEmpty(BuildQuery));
            var buildSearchOptions = new SearchBuildsRequest()
            {
                Count = 50,
            };
            buildSearchOptions.ParseQueryString(BuildQuery);
            return buildSearchOptions;
        }
    }
}