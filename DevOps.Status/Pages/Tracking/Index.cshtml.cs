using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
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

            public string? Title { get; set; }

            public string? Kind { get; set; }

            public int WeekCount { get; set; }

            public int TotalCount { get; set; }
        }

        public TriageContext Context { get; }

        public List<IssueData> Issues { get; set; } = new List<IssueData>();

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageNumber")]
        public int PageNumber { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }
        public string? ErrorMessage { get; set; }

        public TrackingIndexModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync()
        {
            const int PageSize = 25;

            IQueryable<ModelTrackingIssue> query = Context.ModelTrackingIssues;
            if (!string.IsNullOrEmpty(Query))
            {
                try
                {
                    var request = new SearchTrackingIssuesRequest();
                    request.ParseQueryString(Query);
                    query = request.Filter(query);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }
            }

            var week = DateTime.UtcNow - TimeSpan.FromDays(7);
            Issues = await query
                .Where(x => x.IsActive)
                .Select(issue => new IssueData()
                {
                    Id = issue.Id,
                    Title = issue.IssueTitle,
                    Kind = issue.TrackingKind.ToString(),
                    TotalCount = issue.ModelTrackingIssueMatches.Count(),
                    WeekCount = issue.ModelTrackingIssueMatches.Where(x => x.ModelBuildAttempt.ModelBuild.StartTime >= week).Count()
                })
                .OrderByDescending(x => x.WeekCount)
                .Skip(PageSize * PageNumber)
                .Take(PageSize)
                .ToListAsync();

            PaginationDisplay = new PaginationDisplay(
                "/Tracking/Index",
                new Dictionary<string, string>()
                {
                    {nameof(Query), Query ?? "" }
                },
                PageNumber);

        }
    }
}
    