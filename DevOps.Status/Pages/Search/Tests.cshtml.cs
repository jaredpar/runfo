using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;
using YamlDotNet.Serialization.NodeTypeResolvers;

namespace DevOps.Status.Pages.Search
{
    public class TestsModel : PageModel
    {
        public class TestInfo
        {
            public string? TestName { get; set; }
            public string? CollapseName { get; set; }
            public TestResultsDisplay? TestResultsDisplay { get; set; }
            public string? TestNameQuery { get; set; }
            public string? BuildDefinition { get; set; }
            public string? GitHubOrganization { get; set; }
            public string? GitHubRepository { get; set; }
        }

        public const int PageSize = 100;
        
        public TriageContextUtil TriageContextUtil { get; }
        public IGitHubClientFactory GitHubClientFactory { get; }
        public string? ErrorMessage { get; set; }
        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }
        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TestsQuery { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public int? NextPageNumber { get; set; }
        public int? PreviousPageNumber { get; set; }
        public int BuildCount { get; set; }
        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TestsModel(TriageContextUtil triageContextUtil, IGitHubClientFactory gitHubClientFactory)
        {
            TriageContextUtil = triageContextUtil;
            GitHubClientFactory = gitHubClientFactory;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest()
                {
                    Definition = "roslyn-ci",
                    Started = new DateRequestValue(dayQuery: 3),
                }.GetQueryString();
                return Page();
            }

            if (!SearchBuildsRequest.TryCreate(BuildQuery ?? "", out var buildsRequest, out var errorMessage) ||
                !SearchTestsRequest.TryCreate(TestsQuery ?? "", out var testsRequest, out errorMessage))
            {
                ErrorMessage = errorMessage;
                return Page();
            }

            try
            {
                IQueryable<ModelTestResult> query = TriageContextUtil.Context.ModelTestResults
                    .Include(x => x.ModelBuild);
                query = buildsRequest.Filter(query);
                query = testsRequest.Filter(query);
                var results = await query
                    .OrderByDescending(x => x.ModelBuild.BuildNumber)
                    .Skip(PageNumber * PageSize)
                    .Take(PageSize + 1)
                    .ToListAsync();

                var count = 0;
                var isBuildKindFiltered =
                    buildsRequest.BuildType is { } bt &&
                    !(bt is { BuildType: BuildKind.All, Kind: EqualsKind.Equals });

                foreach (var group in results.GroupBy(x => x.TestFullName).OrderByDescending(x => x.Count()))
                {
                    count++;

                    testsRequest.Name = group.Key;
                    var firstBuild = group.FirstOrDefault()?.ModelBuild;
                    var testInfo = new TestInfo()
                    {
                        TestName = group.Key,
                        CollapseName = $"collapse{count}",
                        TestNameQuery = testsRequest.GetQueryString(),
                        BuildDefinition = buildsRequest.Definition,
                        GitHubOrganization = firstBuild?.GitHubOrganization,
                        GitHubRepository = firstBuild?.GitHubRepository,
                        TestResultsDisplay = new TestResultsDisplay(group)
                        {
                            IncludeBuildColumn = true,
                            IncludeBuildKindColumn = !isBuildKindFiltered,
                            IncludeErrorMessageColumn = true,
                        }
                    };

                    TestInfos.Add(testInfo);
                }

                BuildCount = results.GroupBy(x => x.ModelBuild.BuildNumber).Count();
                PreviousPageNumber = PageNumber > 0 ? PageNumber - 1 : (int?)null;
                NextPageNumber = results.Count > PageSize ? PageNumber + 1 : (int?)null;
                return Page();
            }
            catch (SqlException ex) when (ex.IsTimeoutViolation())
            {
                ErrorMessage = "Timeout querying data. Please refine the search to be smaller. Consider shrinking build set or using shorter test names";
                return Page();
            }
        }
    }
}