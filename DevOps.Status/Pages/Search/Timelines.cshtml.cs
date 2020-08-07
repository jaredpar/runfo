#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using DevOps.Status.Util;
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
        }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TimelineQuery { get; set; }

        public List<TimelineData> TimelineDataList { get; set; } = new List<TimelineData>();

        public DotNetQueryUtilFactory DotNetQueryUtilFactory { get;  }

        public TriageContextUtil TriageContextUtil { get; }

        public TimelinesModel(TriageContextUtil triageContextUtil, DotNetQueryUtilFactory dotnetQueryUtilFactory)
        {
            TriageContextUtil = triageContextUtil;
            DotNetQueryUtilFactory = dotnetQueryUtilFactory;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery) || string.IsNullOrEmpty(TimelineQuery))
            {
                if (string.IsNullOrEmpty(BuildQuery))
                {
                    BuildQuery = new StatusBuildSearchOptions() { Definition = "runtime" }.GetUserQueryString();
                }

                if (string.IsNullOrEmpty(TimelineQuery))
                {
                    TimelineQuery = "error";
                }

                return Page();
            }

            var buildSearchOptions = new StatusBuildSearchOptions()
            {
                Count = 50,
            };
            buildSearchOptions.Parse(BuildQuery);
            var timelineSearchOptions = new StatusTimelineSearchOptions();
            timelineSearchOptions.Parse(TimelineQuery);

            var query = buildSearchOptions.GetModelBuildsQuery(TriageContextUtil);
            var builds = await query.ToListAsync();

            var dotnetQueryUtil = DotNetQueryUtilFactory.DotNetQueryUtil;
            var results = await dotnetQueryUtil.SearchTimelineAsync(
                builds.Select(x => TriageContextUtil.GetBuildInfo(x)),
                text: timelineSearchOptions.Value!);
            TimelineDataList = results
                .Select(x => new TimelineData()
                {
                    BuildNumber = x.BuildInfo.Number,
                    BuildUri = x.BuildInfo.BuildUri,
                    JobName = x.Record.JobName ?? "",
                    Line = x.Line,
                })
                .ToList();
            return Page();
        }
    }
}