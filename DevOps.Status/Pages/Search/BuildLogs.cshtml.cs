using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
    public class BuildLogsModel : PageModel
    {
        public sealed class BuildLogData
        {
            public int BuildNumber { get; set; }
            public string? Line { get; set; }
            public string? JobName { get; set; }
            public string? BuildLogUrl { get; set; }
        }

        public List<BuildLogData> BuildLogs { get; } = new List<BuildLogData>();
        public int? BuildCount { get; set; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "lq")]
        public string? LogQuery { get; set; }

        public TriageContextUtil TriageContextUtil { get; }
        public DotNetQueryUtilFactory DotNetQueryUtilFactory { get; }

        public BuildLogsModel(TriageContextUtil triageContextUtil, DotNetQueryUtilFactory factory)
        {
            TriageContextUtil = triageContextUtil;
            DotNetQueryUtilFactory = factory;
        }

        public async Task OnGet()
        {
            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest() { Definition = "runtime" }.GetQueryString();
                return;
            }

            var searchBuildsRequest = new SearchBuildsRequest() { Count = 10 };
            searchBuildsRequest.ParseQueryString(BuildQuery);
            var buildInfos = (await searchBuildsRequest
                .GetQuery(TriageContextUtil)
                .Include(x => x.ModelBuildDefinition)
                .ToListAsync()).Select(x => x.GetBuildInfo()).ToList();
            BuildCount = buildInfos.Count;

            var searchBuildLogsRequest = new SearchBuildLogsRequest();
            searchBuildLogsRequest.ParseQueryString(LogQuery ?? "");

            var queryUtil = await DotNetQueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var results = await queryUtil.SearchBuildLogsAsync(buildInfos, searchBuildLogsRequest);
            foreach (var result in results)
            {
                BuildLogs.Add(new BuildLogData()
                {
                    BuildNumber = result.BuildInfo.Number,
                    Line = result.Line,
                    JobName = result.JobName,
                    BuildLogUrl = result.BuildLogReference.Url
                });
            }
        }
    }
}
