using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Status.Pages.Search
{
    public class TimelinesModel : PageModel
    {
        public TriageContextUtil TriageContextUtil { get; }
        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }
        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TimelineQuery { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }
        public TimelineIssuesDisplay TimelineIssuesDisplay { get; set; } = TimelineIssuesDisplay.Empty;
        public int? BuildCount { get; set; }
        public bool IncludeIssueTypeColumn { get; set; }
        public string? ErrorMessage { get; set; }

        public TimelinesModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task<IActionResult> OnGet()
        {
            const int PageSize = 25;
            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest() { Definition = "runtime" }.GetQueryString();
                return Page();
            }

            if (!SearchBuildsRequest.TryCreate(BuildQuery ?? "", out var buildsRequest, out var errorMessage) ||
                !SearchTimelinesRequest.TryCreate(TimelineQuery ?? "", out var timelinesRequest, out errorMessage))
            {
                ErrorMessage = errorMessage;
                return Page();
            }

            IQueryable<ModelTimelineIssue> query = TriageContextUtil.Context.ModelTimelineIssues;
            query = buildsRequest.Filter(query);
            query = timelinesRequest.Filter(query);
            query = query
                .OrderByDescending(x => x.ModelBuild.BuildNumber)
                .Skip(PageNumber * PageSize)
                .Take(PageSize);
            TimelineIssuesDisplay = await TimelineIssuesDisplay.Create(
                query,
                includeBuildColumn: true,
                includeIssueTypeColumn: timelinesRequest.Type is null,
                includeAttemptColumn: true);
            BuildCount = TimelineIssuesDisplay.Issues.GroupBy(x => x.BuildNumber).Count();
            IncludeIssueTypeColumn = timelinesRequest.Type is null;
            PaginationDisplay = new PaginationDisplay(
                "/Search/Timelines",
                new Dictionary<string, string>()
                {
                    { "bq", BuildQuery ?? "" },
                    { "tq", TimelineQuery ?? "" }
                },
                PageNumber);
            return Page();
        }
    }
}