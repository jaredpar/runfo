using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Pages.Search
{
    public class TestsModel : PageModel
    {
        public sealed class TestInfo
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
        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; } 
        public int? TotalCount { get; set; }
        public List<TestInfo> TestInfos { get; set; } = new List<TestInfo>();

        public TriageContext TriageContext => TriageContextUtil.Context;

        public TestsModel(TriageContextUtil triageContextUtil, IGitHubClientFactory gitHubClientFactory)
        {
            TriageContextUtil = triageContextUtil;
            GitHubClientFactory = gitHubClientFactory;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(Query))
            {
                Query = new SearchTestsRequest() { Definition = "roslyn-ci" }.GetQueryString();
                return Page();
            }

            if (!SearchTestsRequest.TryCreate(Query ?? "", out var testsRequest, out var errorMessage))
            {
                ErrorMessage = errorMessage;
                return Page();
            }

            try
            {
                var query = testsRequest
                    .Filter(TriageContext.ModelTestResults);
                var totalCount = await query.CountAsync();
                var results = await query
                    .OrderByDescending(x => x.StartTime)
                    .Skip(PageNumber * PageSize)
                    .Take(PageSize + 1)
                    .Include(x => x.ModelBuild)
                    .ToListAsync();

                var count = 0;
                var isBuildKindFiltered =
                    testsRequest.BuildKind is { } bk &&
                    !(bk is { BuildKind: ModelBuildKind.All, Kind: EqualsKind.Equals });

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
                        BuildDefinition = testsRequest.Definition,
                        GitHubOrganization = firstBuild?.GitHubOrganization,
                        GitHubRepository = firstBuild?.GitHubRepository,
                        TestResultsDisplay = new TestResultsDisplay(group)
                        {
                            IncludeBuildColumn = true,
                            IncludeBuildKindColumn = !isBuildKindFiltered,
                            IncludeErrorMessageColumn = true,
                        },
                    };

                    TestInfos.Add(testInfo);
                }

                PaginationDisplay = new PaginationDisplay(
                    "/Search/Tests",
                    new Dictionary<string, string>()
                    {
                        { "q", Query ?? "" }
                    },
                    PageNumber,
                    totalCount / PageSize);
                TotalCount = totalCount;
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