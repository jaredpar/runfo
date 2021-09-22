using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
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
    public class HelixLogsModel : PageModel
    {
        public record HelixLogData(BuildInfo BuildInfo, string? Line, string HelixLogKind, string HelixLogUri)
        {
            public int BuildNumber => BuildInfo.Number;
            [MemberNotNullWhen(true, nameof(Line))]
            public bool IsMatch => Line is object;
        }

        // Results
        public bool DidSearch { get; set; } = false;
        public List<HelixLogData> HelixLogs { get; } = new List<HelixLogData>();
        public string? BuildResultText { get; set; }
        public int BuildStart { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }
        [BindProperty(SupportsGet = true, Name = "lq")]
        public string? LogQuery { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }

        public TriageContextUtil TriageContextUtil { get; }

        public HelixLogsModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGet()
        {
            const int pageSize = 25;

            ErrorMessage = null;

            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest()
                {
                    Definition = "runtime",
                    Started = new DateRequestValue(dayQuery: 3),
                }.GetQueryString();
                return;
            }

            if (!SearchBuildsRequest.TryCreate(BuildQuery, out var buildsRequest, out var errorMessage) ||
                !SearchHelixLogsRequest.TryCreate(LogQuery ?? "", out var logsRequest, out errorMessage))
            {
                ErrorMessage = errorMessage;
                return;
            }

            // Helix logs are only kept for failed builds. If the user doesn't specify a specific result type, 
            // like say cancelled, then just search all non succeeded builds.
            if (buildsRequest.BuildResult is null)
            {
                buildsRequest.BuildResult = new BuildResultRequestValue(ModelBuildResult.Succeeded, EqualsKind.NotEquals);
                BuildQuery = buildsRequest.GetQueryString();
            }

            if (logsRequest.HelixLogKinds.Count == 0)
            {
                logsRequest.HelixLogKinds.Add(HelixLogKind.Console);
                LogQuery = logsRequest.GetQueryString();
            }

            if (string.IsNullOrEmpty(logsRequest.Text))
            {
                ErrorMessage = @"Must specify text to search for 'text: ""StackOverflowException""'";
                return;
            }

            try
            {
                IQueryable<ModelBuild> query = TriageContextUtil.Context.ModelBuilds;
                query = buildsRequest.Filter(query);
                var totalBuildCount = await query.CountAsync();

                var modelBuildInfoList = await query
                    .Skip(PageNumber * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        x.Id,
                        x.BuildNumber,
                        x.AzureOrganization,
                        x.AzureProject,
                        x.StartTime,
                        x.GitHubOrganization,
                        x.GitHubRepository,
                        x.GitHubTargetBranch,
                        x.PullRequestNumber,
                    })
                    .ToListAsync();

                var modelBuildIds = modelBuildInfoList.Select(x => x.Id).ToList();

                var modelResultsQuery = TriageContextUtil.Context.ModelTestResults.Where(x => modelBuildIds.Contains(x.ModelBuildId));
                modelResultsQuery = logsRequest.Filter(modelResultsQuery);

                var modelResults = await modelResultsQuery
                    .Select(x => new
                    {
                        x.ModelBuildId,
                        x.HelixConsoleUri,
                        x.HelixCoreDumpUri,
                        x.HelixRunClientUri,
                        x.HelixTestResultsUri,
                    })
                    .ToListAsync();

                var toQuery = modelResults
                    .Select(x =>
                    {
                        var b = modelBuildInfoList.First(b => b.Id == x.ModelBuildId);
                        return
                            (new BuildInfo(b.AzureOrganization, b.AzureProject, b.BuildNumber,
                                new GitHubBuildInfo(b.GitHubOrganization!, b.GitHubRepository!, b.PullRequestNumber, b.GitHubTargetBranch)),
                                new HelixLogInfo(
                                    runClientUri: x.HelixRunClientUri,
                                    consoleUri: x.HelixConsoleUri,
                                    coreDumpUri: x.HelixCoreDumpUri,
                                    testResultsUri: x.HelixTestResultsUri));
                    });

                var helixServer = new HelixServer();
                var errorBuilder = new StringBuilder();
                var results = await helixServer.SearchHelixLogsAsync(
                    toQuery,
                    logsRequest,
                    ex => errorBuilder.AppendLine(ex.Message));
                foreach (var result in results)
                {
                    HelixLogs.Add(new HelixLogData(result.BuildInfo, result.Line, result.HelixLogKind.GetDisplayFileName(), result.HelixLogUri));
                }

                if (errorBuilder.Length > 0)
                {
                    ErrorMessage = errorBuilder.ToString();
                }

                PaginationDisplay = new PaginationDisplay(
                    "/Search/HelixLogs",
                    new Dictionary<string, string>()
                    {
                        { "bq", BuildQuery },
                        { "lq", LogQuery ?? ""},
                    },
                    PageNumber,
                    totalBuildCount / pageSize);
                BuildResultText = $"Results for builds {PageNumber * pageSize}-{(PageNumber * pageSize) + pageSize} of {totalBuildCount}";
                DidSearch = true;
            }
            catch (SqlException ex) when (ex.IsTimeoutViolation())
            {
                ErrorMessage = "Timeout fetching data from the server";
            }
        }
    }
}
