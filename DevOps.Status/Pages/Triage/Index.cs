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

namespace DevOps.Status.Pages.Triage
{
    public class TriageIndexModel : PageModel
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

        public TriageIndexModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync()
        {
            var week = DateTime.UtcNow - TimeSpan.FromDays(7);
            Issues = await Context.ModelTriageIssues
                .Select(issue => new IssueData()
                {
                    Id = issue.Id,
                    SearchText = issue.SearchText,
                    SearchKind = issue.SearchKind.ToString(),
                    TotalCount = issue.ModelTriageIssueResults.Count(),
                    WeekCount = issue.ModelTriageIssueResults.Where(x => x.ModelBuild.StartTime >= week).Count()
                })
                .OrderByDescending(x => x.WeekCount)
                .ToListAsync();
        }
    }
}
    