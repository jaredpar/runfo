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

            public int BuildCount { get; set; }
        }

        public TriageContext Context { get; }

        public List<IssueData> Issues { get; set; }

        public IssuesModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync()
        {
            Issues = await Context.ModelTriageIssues
                .Select(x => new IssueData()
                {
                    Id = x.Id,
                    SearchText = x.SearchText,
                    SearchKind = x.SearchKind.ToString(),
                    BuildCount = x.ModelTriageIssueResults.Count
                })
                .ToListAsync();
        }
    }
}
    