using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Status.Pages.Search
{
    public class TimelinesModel : PageModel
    {
        public TriageContextUtil TriageContextUtil { get; }
        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }
        public int? TotalCount { get; set; }
        public TimelineIssuesDisplay TimelineIssuesDisplay { get; set; } = TimelineIssuesDisplay.Empty;
        public bool IncludeIssueTypeColumn { get; set; }
        public string? ErrorMessage { get; set; }

        public TriageContext TriageContext => TriageContextUtil.Context;

        public TimelinesModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task<IActionResult> OnGet()
        {
            const int PageSize = 25;
            if (string.IsNullOrEmpty(Query))
            {
                Query = new SearchTimelinesRequest() { Definition = "roslyn-ci" }.GetQueryString();
                return Page();
            }

            if (!SearchTimelinesRequest.TryCreate(Query ?? "", out var timelinesRequest, out var errorMessage))
            {
                ErrorMessage = errorMessage;
                return Page();
            }

            try
            {
                var query = timelinesRequest.Filter(TriageContext.ModelTimelineIssues);
                var totalCount = await query.CountAsync();

                query = query
                    .OrderByDescending(x => x.StartTime)
                    .Skip(PageNumber * PageSize)
                    .Take(PageSize)
                    .Include(x => x.ModelBuild);
                TimelineIssuesDisplay = await TimelineIssuesDisplay.Create(
                    query,
                    includeBuildColumn: true,
                    includeIssueTypeColumn: timelinesRequest.Type is null,
                    includeAttemptColumn: true);
                IncludeIssueTypeColumn = timelinesRequest.Type is null;
                PaginationDisplay = new PaginationDisplay(
                    "/Search/Timelines",
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
                ErrorMessage = "Timeout fetching data from the server";
                return Page();
            }
        }
    }
}