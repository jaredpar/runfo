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
            public BuildResult BuildResult { get; set; }
            public string? Result { get; set; }
            public int BuildNumber { get; set; }
            public string? BuildUri { get; set; }
            public string? Kind { get; set; }
            public string? Definition { get; set; }
            public string? DefinitionUri { get; set; }
            public GitHubPullRequestKey? PullRequestKey { get; set; }
        }

        public TriageContext TriageContext { get; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }
        public string? PassRate { get; set; }
        public string? WarningMessage { get; set; }
        public bool IncludeDefinitionColumn { get; set; }
        public List<BuildData> Builds { get; set; } = new List<BuildData>();

        public BuildsModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task OnGet()
        {
            if (string.IsNullOrEmpty(Query))
            {
                Query = new SearchBuildsRequest() { Definition = "roslyn-ci" }.GetQueryString();
                return;
            }

            var options = new SearchBuildsRequest();
            options.ParseQueryString(Query);

            IncludeDefinitionColumn = !options.HasDefinition;

            var results = await options
                .GetQuery(TriageContext)
                .OrderByDescending(x => x.BuildNumber)
                .Include(x => x.ModelBuildDefinition)
                .Take(options.Limit)
                .ToListAsync();

            if (results.Count == options.Limit)
            {
                WarningMessage = $"Builds limited to first {options.Limit}";
            }

            Builds = results
                .Select(x =>
                {
                    var buildInfo = x.GetBuildResultInfo();
                    var buildResult = x.BuildResult ?? BuildResult.None;
                    return new BuildData()
                    {
                        BuildResult = buildResult,
                        Result = buildResult.ToString(),
                        BuildNumber = buildInfo.Number,
                        Kind = buildInfo.PullRequestKey.HasValue ? "Pull Request" : "Rolling",
                        PullRequestKey = buildInfo.PullRequestKey,
                        BuildUri = buildInfo.BuildUri,
                        Definition = x.ModelBuildDefinition.DefinitionName,
                        DefinitionUri = buildInfo.DefinitionInfo.DefinitionUri,
                    };
                })
                .ToList();
            var passRate = (double)Builds.Count(x => x.BuildResult == BuildResult.Succeeded || x.BuildResult == BuildResult.PartiallySucceeded) / Builds.Count;
            passRate *= 100;
            PassRate = $"{passRate:N2}%";
        }
    }
}