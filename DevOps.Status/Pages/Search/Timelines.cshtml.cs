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
        }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "tq")]
        public string? TimelineQuery { get; set; }

        public List<TimelineData> TimelineDataList { get; set; } = new List<TimelineData>();

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
                    BuildQuery = new SearchBuildsRequest() { Definition = "runtime" }.GetQueryString();
                }

                if (string.IsNullOrEmpty(TimelineQuery))
                {
                    TimelineQuery = "error";
                }

                return Page();
            }

            var buildSearchOptions = new SearchBuildsRequest()
            {
                Count = 50,
            };
            buildSearchOptions.ParseQueryString(BuildQuery);
            var timelineSearchOptions = new SearchTimelinesRequest();
            timelineSearchOptions.ParseQueryString(TimelineQuery);

            var query = timelineSearchOptions.GetQuery(
                TriageContextUtil,
                buildSearchOptions.GetQuery(TriageContextUtil))
                .Include(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition);

            var results = await query.ToListAsync();
            TimelineDataList = results
                .Select(x => new TimelineData()
                {
                    BuildNumber = x.ModelBuild.BuildNumber,
                    BuildUri = x.ModelBuild.GetBuildInfo().BuildUri,
                    JobName = x.JobName,
                    Line = x.Message,
                })
                .ToList();
            return Page();
        }
    }
}