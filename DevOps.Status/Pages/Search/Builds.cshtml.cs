#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages.Search
{
    public class BuildsModel : PageModel
    {
        public class BuildData
        {
            public string? Result { get; set; }
            public int BuildNumber { get; set; }
            public string? BuildUri { get; set; }
            public string? Kind { get; set; }
        }

        public TriageContext TriageContext { get; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }

        public List<BuildData> Builds { get; set; } = new List<BuildData>();

        public BuildsModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task OnGet()
        {
            if (string.IsNullOrEmpty(Query))
            {
                Query = new StatusBuildSearchOptions() { Repository = "runtime", Count = 10 }.GetUserQueryString();
                return;
            }

            var options = new StatusBuildSearchOptions();
            options.Parse(Query);

            Builds = (await options.GetModelBuildsQuery(TriageContext).ToListAsync())
                .Select(x =>
                {
                    var buildInfo = TriageContextUtil.GetBuildInfo(x);
                    return new BuildData()
                    {
                        Result = x.BuildResult.ToString(),
                        BuildNumber = buildInfo.Number,
                        Kind = buildInfo.PullRequestNumber.HasValue ? "Pull Request" : "Rolling",
                        BuildUri = buildInfo.BuildUri,
                    };
                })
                .ToList();
        }
    }
}