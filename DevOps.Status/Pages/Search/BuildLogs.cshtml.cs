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

namespace DevOps.Status.Pages.Search
{
    public class BuildLogsModel : PageModel
    {
        public List<SearchBuildLogsResult> BuildLogs { get; } = new List<SearchBuildLogsResult>();

        public DotNetQueryUtilFactory DotNetQueryUtilFactory { get; }

        public BuildLogsModel(DotNetQueryUtilFactory factory)
        {
            DotNetQueryUtilFactory = factory;
        }

        public async Task OnGet()
        {
            var request = new SearchBuildLogsRequest()
            {
                LogName = "Build And Test",
                Text = "The type or namespace name"
            };

            var buildInfos = new BuildInfo[]
            {
                new BuildInfo(new BuildKey("dnceng", "public", 796036), default, (GitHubInfo?)null, null, null, default),
            };

            var queryUtil = await DotNetQueryUtilFactory.CreateDotNetQueryUtilForUserAsync();

            var results = await queryUtil.SearchBuildLogsAsync(buildInfos, request);
            BuildLogs.AddRange(results);
        }
    }
}
