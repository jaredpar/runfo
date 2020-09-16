using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
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
        [BindProperty(SupportsGet = true, Name = "page")]
        public int PageNumber { get; set; }
        public int? NextPageNumber { get; set; }
        public int? PreviousPageNumber { get; set; }
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

            try
            {
                var buildSearchOptions = new SearchBuildsRequest();
                buildSearchOptions.ParseQueryString(BuildQuery);
                var timelineSearchOptions = new SearchTimelinesRequest();
                timelineSearchOptions.ParseQueryString(TimelineQuery ?? "");

                IQueryable<ModelTimelineIssue> query = TriageContextUtil.Context.ModelTimelineIssues;
                query = buildSearchOptions.FilterBuilds(query);
                query = timelineSearchOptions.FilterTimelines(query);
                query = query
                    .Include(x => x.ModelBuild)
                    .OrderByDescending(x => x.ModelBuild.BuildNumber)
                    .Skip(PageNumber * PageSize)
                    .Take(PageSize);
                TimelineIssuesDisplay = await TimelineIssuesDisplay.Create(
                    query,
                    includeBuildColumn: true,
                    includeIssueTypeColumn: timelineSearchOptions.Type is null,
                    includeAttemptColumn: true);
                BuildCount = TimelineIssuesDisplay.Issues.GroupBy(x => x.BuildNumber).Count();
                IncludeIssueTypeColumn = timelineSearchOptions.Type is null;
                PreviousPageNumber = PageNumber > 0 ? PageNumber - 1 : (int?)null;
                NextPageNumber = PageNumber + 1;
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }
    }
}