using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
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
            public string? BuildLogUri { get; set; }
        }

        public List<BuildLogData> BuildLogs { get; } = new List<BuildLogData>();
        public int? BuildCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AzureDevOpsEmail { get; set; }

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
            if (User.GetVsoIdentity() is { } identity)
            {
                AzureDevOpsEmail = identity.FindFirst(ClaimTypes.Email)?.Value;
            }

            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest()
                {
                    Definition = "roslyn-ci",
                    Started = new DateRequestValue(dayQuery: 3),
                }.GetQueryString();
                return;
            }

            if (!SearchBuildsRequest.TryCreate(BuildQuery, out var buildsRequest, out var errorMessage) ||
                !SearchBuildLogsRequest.TryCreate(LogQuery ?? "", out var logsRequest, out errorMessage))
            {
                ErrorMessage = errorMessage;
                return;
            }

            if (string.IsNullOrEmpty(logsRequest.Text))
            {
                ErrorMessage = @"Must specify text to search for 'text: ""StackOverflowException""'";
                return;
            }

            ErrorMessage = null;

            List<BuildResultInfo> buildInfos;
            try
            {
                buildInfos = (await buildsRequest
                    .Filter(TriageContextUtil.Context.ModelBuilds)
                    .OrderByDescending(x => x.BuildNumber)
                    .ToListAsync()).Select(x => x.GetBuildResultInfo()).ToList();
            }
            catch (SqlException ex) when (ex.IsTimeoutViolation())
            {
                ErrorMessage = "Timeout fetching data from server";
                return;
            }

            BuildCount = buildInfos.Count;

            var queryUtil = await DotNetQueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var errorBuilder = new StringBuilder();
            var results = await queryUtil.SearchBuildLogsAsync(buildInfos, logsRequest, ex => errorBuilder.AppendLine(ex.Message));
            foreach (var result in results)
            {
                BuildLogs.Add(new BuildLogData()
                {
                    BuildNumber = result.BuildInfo.Number,
                    Line = result.Line,
                    JobName = result.JobName,
                    BuildLogUri = result.BuildLogReference.Url,
                });
            }

            if (errorBuilder.Length > 0)
            {
                ErrorMessage = errorBuilder.ToString();
            }
        }
    }
}
