using System;
using System.Collections.Generic;
using System.Linq;
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
    public class HelixLogsModel : PageModel
    {
        public sealed class HelixLogData
        {
            public int BuildNumber { get; set; }
            public string? Line { get; set; }
            public string? HelixLogKind { get; set; }
            public string? HelixLogUri { get; set; }
        }

        public List<HelixLogData> HelixLogs { get; } = new List<HelixLogData>();
        public int? BuildCount { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "lq")]
        public string? LogQuery { get; set; }

        public TriageContextUtil TriageContextUtil { get; }

        public HelixLogsModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGet()
        {
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

            if (logsRequest.HelixLogKinds.Count == 0)
            {
                logsRequest.HelixLogKinds.Add(HelixLogKind.Console);
            }

            if (string.IsNullOrEmpty(logsRequest.Text))
            {
                ErrorMessage = @"Must specify text to search for 'text: ""StackOverflowException""'";
                return;
            }

            try
            {
                IQueryable<ModelTestResult> query = TriageContextUtil.Context.ModelTestResults.Where(x => x.IsHelixTestResult);
                query = buildsRequest.Filter(query);
                query = query
                    .Take(100)
                    .Include(x => x.ModelBuild);

                var modelResults = await query.ToListAsync();
                var toQuery = modelResults
                    .Select(x => (x.ModelBuild.GetBuildInfo(), x.GetHelixLogInfo()))
                    .Where(x => x.Item2 is object);

                var helixServer = new HelixServer();
                var errorBuilder = new StringBuilder();
                var results = await helixServer.SearchHelixLogsAsync(
                    toQuery!,
                    logsRequest,
                    ex => errorBuilder.AppendLine(ex.Message));
                foreach (var result in results)
                {
                    HelixLogs.Add(new HelixLogData()
                    {
                        BuildNumber = result.BuildInfo.Number,
                        Line = result.Line,
                        HelixLogKind = result.HelixLogKind.GetDisplayFileName(),
                        HelixLogUri = result.HelixLogUri,
                    });
                }

                if (errorBuilder.Length > 0)
                {
                    ErrorMessage = errorBuilder.ToString();
                }
            }
            catch (SqlException ex) when (ex.IsTimeoutViolation())
            {
                ErrorMessage = "Timeout fetching data from the server";
            }
        }
    }
}
