#nullable enable

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

            /*
            var query = buildSearchOptions
                .GetModelBuildsQuery(TriageContextUtil)
                .Join(
                    TriageContextUtil.Context.ModelTimelineIssues,
                    b => b.Id,
                    t => t.ModelBuildId,
                    (b, t) => new { b, t })
                .Where(t => EF.Functions.Like(t.t.Message, timelineSearchOptions.Value!));
            */

            var value = timelineSearchOptions.Value!.Replace('*', '%');
            value = '%' + value.Trim('%') + '%';
            var query = TriageContextUtil.Context
                .ModelBuilds
                .Include(x => x.ModelBuildDefinition)
                .Join(
                    TriageContextUtil.Context.ModelTimelineIssues,
                    b => b.Id,
                    t => t.ModelBuildId,
                    (b, t) => new { b, t })
                .Where(t => EF.Functions.Like(t.t.Message, value));
            /*
            var query = TriageContextUtil.Context
                .ModelBuilds
                .Include(x => x.ModelBuildDefinition)
                .Join(
                    TriageContextUtil.Context.ModelTimelineIssues,
                    b => b.Id,
                    t => t.ModelBuildId,
                    (b, t) => new { b, t })
                .Take(10);
            */

            var results = await query.ToListAsync();
            TimelineDataList = results
                .Select(x => new TimelineData()
                {
                    BuildNumber = x.b.BuildNumber,
                    BuildUri = TriageContextUtil.GetBuildInfo(x.b).BuildUri,
                    JobName = x.t.JobName,
                    Line = x.t.Message,
                })
                .ToList();
            return Page();
        }
    }
}