using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages.Tracking
{
    public class TrackingIndexModel : PageModel
    {
        public class IssueData
        {
            public int Id { get; set; }

            public string? SearchText { get; set; }

            public string? Kind { get; set; }

            public int WeekCount { get; set; }

            public int TotalCount { get; set; }
        }

        public TriageContext Context { get; }

        public List<IssueData> Issues { get; set; } = new List<IssueData>();

        public TrackingIndexModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync()
        {
            var week = DateTime.UtcNow - TimeSpan.FromDays(7);
            Issues = await Context.ModelTrackingIssues
                .Where(x => x.IsActive)
                .Select(issue => new IssueData()
                {
                    Id = issue.Id,
                    SearchText = issue.SearchRegexText,
                    Kind = issue.TrackingKind.ToString(),
                    TotalCount = issue.ModelTrackingIssueMatches.Count(),
                    WeekCount = issue.ModelTrackingIssueMatches.Where(x => x.ModelBuildAttempt.ModelBuild.StartTime >= week).Count()
                })
                .OrderByDescending(x => x.WeekCount)
                .ToListAsync();
        }
    }
}
    