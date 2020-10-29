using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
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
            public string? TargetBranch { get; set; }
            public string? Queued { get; set; }
        }

        public TriageContext TriageContext { get; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }
        public int TotalBuildCount { get; set; }
        public string? ErrorMessage { get; set; } 
        public bool IncludeDefinitionColumn { get; set; }
        public bool IncludeTargetBranchColumn { get; set; }
        public List<BuildData> Builds { get; set; } = new List<BuildData>();
        public DateTimeUtil DateTimeUtil = new DateTimeUtil();

        public BuildsModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task OnGet()
        {
            const int pageSize = 25;
            if (string.IsNullOrEmpty(Query))
            {
                Query = new SearchBuildsRequest()
                {
                    Definition = "roslyn-ci",
                    Started = new DateRequestValue(dayQuery: 5),
                }.GetQueryString();
                return;
            }

            if (!SearchBuildsRequest.TryCreate(Query, out var request, out var errorMessage))
            {
                ErrorMessage = errorMessage;
                return;
            }

            TotalBuildCount = await request
                .Filter(TriageContext.ModelBuilds)
                .CountAsync();
            PaginationDisplay = new PaginationDisplay(
                "/Search/Builds",
                new Dictionary<string, string>()
                {
                    { "q", Query },
                },
                PageNumber,
                TotalBuildCount / pageSize);

            var skipCount = PageNumber * pageSize;
            List<ModelBuild> results;
            try
            {
                results = await request
                    .Filter(TriageContext.ModelBuilds)
                    .OrderByDescending(x => x.BuildNumber)
                    .Include(x => x.ModelBuildDefinition)
                    .Skip(skipCount)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.IsTimeoutViolation())
            {
                ErrorMessage = "Timeout fetching data from the server";
                return;
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
                        TargetBranch = buildInfo.GitHubBuildInfo?.TargetBranch,
                        Queued = DateTimeUtil.ConvertDateTime(buildInfo.QueueTime)?.ToString("yyyy-MM-dd hh:mm tt"),
                    };
                })
                .ToList();

            IncludeDefinitionColumn = !request.HasDefinition;
            IncludeTargetBranchColumn = !request.TargetBranch.HasValue;
        }
    }
}