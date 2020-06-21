
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
    public class IssueModel : PageModel
    {
        public TriageContext Context { get; }

        public string SearchText { get; set; }

        public IssueModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync(int id)
        {
            var issue = await Context.ModelTriageIssues
                .Where(x => x.Id == id)
                .SingleAsync();
            SearchText = issue.SearchText;
        }
    }
}
    
