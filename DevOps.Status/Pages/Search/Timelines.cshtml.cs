#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevOps.Status.Pages.Search
{
    public class TimelinesModel : PageModel
    {
        public class TimelineData
        {
            public int BuildNumber { get; set; }
            public string? BuildUri { get; set; }
            public string? Message { get; set; }
            public int Attempt { get; set; }
            public string? Kind { get; set; }
        }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TimelineQuery { get; set; }

        public List<TimelineData> TimelineDataList { get; set; } = new List<TimelineData>();

        public TriageContextUtil TriageContextUtil { get; set; }

        public TimelinesModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery) || string.IsNullOrEmpty(TimelineQuery))
            {
                if (string.IsNullOrEmpty(BuildQuery))
                {
                    BuildQuery = new StatusBuildSearchOptions() { Repository = "runtime" }.GetUserQueryString();
                }

                if (string.IsNullOrEmpty(TimelineQuery))
                {
                    TimelineQuery = "error";
                }

                return Page();
            }

            var buildSearchOptions = new StatusBuildSearchOptions();
            buildOptions.Parse(BuildQuery);

            var timelineSearchOptions = new StatusTimelineSearchOptions();
        }
    }
}