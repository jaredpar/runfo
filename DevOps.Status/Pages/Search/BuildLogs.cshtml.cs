using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        public record BuildLogData(BuildResultInfo BuildResultInfo, string? Line, string? JobName, string RecordName, BuildLogReference BuildLogReference)
        {
            public int BuildNumber => BuildResultInfo.Number;
            [MemberNotNullWhen(true, nameof(Line))]
            public bool IsMatch => Line is object;
        }

        public string? ErrorMessage { get; set; }
        public string? AzureDevOpsEmail { get; set; }
        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }
        [BindProperty(SupportsGet = true, Name = "lq")]
        public string? LogQuery { get; set; }

        // Build Search Information
        public List<BuildLogData> BuildLogDatas { get; } = new List<BuildLogData>();
        public string? SearchStatus { get; set; }
        public bool DidSearch { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }

        public TriageContextUtil TriageContextUtil { get; }
        public DotNetQueryUtilFactory DotNetQueryUtilFactory { get; }

        public BuildLogsModel(TriageContextUtil triageContextUtil, DotNetQueryUtilFactory factory)
        {
            TriageContextUtil = triageContextUtil;
            DotNetQueryUtilFactory = factory;
        }

        public async Task OnGet()
        {
            const int pageSize = 25;

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
            int totalBuildCount;
            try
            {
                var query = buildsRequest.Filter(TriageContextUtil.Context.ModelBuilds);
                totalBuildCount = await query.CountAsync();

                buildInfos = await query
                    .OrderByDescending(x => x.BuildNumber)
                    .Skip(pageSize * PageNumber)
                    .Take(pageSize)
                    .ToBuildResultInfoListAsync();
            }
            catch (SqlException ex) when (ex.IsTimeoutViolation())
            {
                ErrorMessage = "Timeout fetching data from server";
                return;
            }

            var queryUtil = await DotNetQueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var errorBuilder = new StringBuilder();
            var results = await queryUtil.SearchBuildLogsAsync(buildInfos, logsRequest, ex => errorBuilder.AppendLine(ex.Message));
            foreach (var result in results.OrderByDescending(x => x.BuildInfo.Number))
            {
                BuildLogDatas.Add(new BuildLogData(result.BuildInfo, result.Line, result.JobName, result.Record.Name, result.BuildLogReference));
            }

            PaginationDisplay = new PaginationDisplay(
                "/Search/BuildLogs",
                new Dictionary<string, string>()
                {
                    { "bq", BuildQuery },
                    { "lq", LogQuery ?? ""},
                },
                PageNumber,
                totalBuildCount / pageSize);
            SearchStatus = $"Results for builds {PageNumber * pageSize}-{(PageNumber * pageSize) + pageSize} of {totalBuildCount}";
            DidSearch = true;
            if (errorBuilder.Length > 0)
            {
                ErrorMessage = errorBuilder.ToString();
            }
        }
    }
}
