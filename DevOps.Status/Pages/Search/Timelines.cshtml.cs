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
        public class TimelineData
        {
            public int BuildNumber { get; set; }
            public string? BuildUri { get; set; }
            public string? JobName { get; set; }
            public string? Line { get; set; }
            public IssueType IssueType { get; set; }
        }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TimelineQuery { get; set; }

        public List<TimelineData> TimelineDataList { get; set; } = new List<TimelineData>();

        public int? BuildCount { get; set; }
        public bool IncludeIssueTypeColumn { get; set; }
        public string? ErrorMessage { get; set; }

        public TimelinesModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest() { Definition = "runtime" }.GetQueryString();
                return Page();
            }

            try
            {
                var buildSearchOptions = new SearchBuildsRequest()
                {
                    Count = 10,
                };
                buildSearchOptions.ParseQueryString(BuildQuery);
                var timelineSearchOptions = new SearchTimelinesRequest();
                timelineSearchOptions.ParseQueryString(TimelineQuery ?? "");

                var results = await timelineSearchOptions.GetResultsAsync(
                    TriageContextUtil,
                    buildSearchOptions.GetQuery(TriageContextUtil),
                    includeBuild: true);
                TimelineDataList = results
                    .Select(x => new TimelineData()
                    {
                        BuildNumber = x.ModelBuild.BuildNumber,
                        BuildUri = x.ModelBuild.GetBuildResultInfo().BuildUri,
                        JobName = x.JobName,
                        Line = x.Message,
                        IssueType = x.IssueType,
                    })
                    .ToList();
                BuildCount = buildSearchOptions.Count;
                IncludeIssueTypeColumn = timelineSearchOptions.Type is null;
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