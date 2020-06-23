using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages
{
    public class IssuesModel : PageModel
    {
        public class IssueData
        {
            public int Id { get; set; }

            public string SearchText { get; set; }

            public string SearchKind { get; set; }

            public int WeekCount { get; set; }

            public int TotalCount { get; set; }
        }

        public TriageContext Context { get; }

        public List<IssueData> Issues { get; set; }

        public IssuesModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync()
        {
            Issues = new List<IssueData>();
            foreach (var issue in await Context.ModelTriageIssues.ToListAsync())
            {
                var issueData = new IssueData()
                {
                    Id = issue.Id,
                    SearchText = issue.SearchText,
                    SearchKind = issue.SearchKind.ToString(),
                    WeekCount = await GetWeekCount(issue),
                    TotalCount = await GetTotalCount(issue)
                };
                Issues.Add(issueData);
            }

            Issues = Issues.OrderByDescending(x => x.WeekCount).ToList();

            async Task<int> GetTotalCount(ModelTriageIssue issue) =>
                await Context.ModelTriageIssueResults
                    .Where(x => x.ModelTriageIssueId == issue.Id)
                    .CountAsync();

            async Task<int> GetWeekCount(ModelTriageIssue issue)
            {
                var week = DateTime.UtcNow - TimeSpan.FromDays(7);
                return await Context.ModelTriageIssueResults
                    .Where(x => x.ModelTriageIssueId == issue.Id && x.ModelBuild.StartTime >= week)
                    .CountAsync();

            }
        }
    }
}
    