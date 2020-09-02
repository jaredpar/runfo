using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        public string? AzureDevOpsEmail { get; set; }

        [BindProperty(SupportsGet = true, Name = "bq")]
        public string? BuildQuery { get; set; }

        [BindProperty(SupportsGet = true, Name = "lq")]
        public string? LogQuery { get; set; }

        public TriageContextUtil TriageContextUtil { get; }
        public DotNetQueryUtilFactory DotNetQueryUtilFactory { get; }

        public HelixLogsModel(TriageContextUtil triageContextUtil, DotNetQueryUtilFactory factory)
        {
            TriageContextUtil = triageContextUtil;
            DotNetQueryUtilFactory = factory;
        }

        public async Task OnGet()
        {
            ErrorMessage = null;
            if (User.GetVsoIdentity() is { } identity)
            {
                AzureDevOpsEmail = identity.FindFirst(ClaimTypes.Email)?.Value;
            }

            if (string.IsNullOrEmpty(BuildQuery))
            {
                BuildQuery = new SearchBuildsRequest() { Definition = "runtime" }.GetQueryString();
                return;
            }

            var searchBuildsRequest = new SearchBuildsRequest() { Count = 10 };
            searchBuildsRequest.ParseQueryString(BuildQuery);

            var searchHelixLogsRequest = new SearchHelixLogsRequest()
            {
                HelixLogKinds = new List<HelixLogKind>(new[] { HelixLogKind.Console }),
            };
            searchHelixLogsRequest.ParseQueryString(LogQuery ?? "");
            if (string.IsNullOrEmpty(searchHelixLogsRequest.Text))
            {
                ErrorMessage = @"Must specify text to search for 'text: ""StackOverflowException""'";
                return;
            }

            IQueryable<ModelTestResult> query = searchBuildsRequest.GetQuery(TriageContextUtil)
                .Join(
                    TriageContextUtil.Context.ModelTestResults.Where(x => x.IsHelixTestResult),
                    b => b.Id,
                    t => t.ModelBuildId,
                    (b, t) => t)
                .Include(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition);

            var modelResults = await query.ToListAsync();
            var toQuery = modelResults
                .Select(x => (x.ModelBuild.GetBuildInfo(), x.GetHelixLogInfo()))
                .Where(x => x.Item2 is object);

            var queryUtil = await DotNetQueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var errorBuilder = new StringBuilder();
            var results = await queryUtil.SearchHelixLogsAsync(
                toQuery!,
                searchHelixLogsRequest,
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
    }
}
