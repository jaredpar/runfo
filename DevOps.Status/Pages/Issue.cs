
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
        public sealed class Result
        {
            public int BuildNumber { get; set; }

            public string BuildUri { get; set; }

            public string JobName { get; set; }
        }

        public TriageContext Context { get; }

        public string SearchText { get; set; }

        public List<Result> Results { get; set; }

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

            Results = await Context.ModelTriageIssueResults
                .Include(x => x.ModelBuild)
                .Include(x => x.ModelBuild.ModelBuildDefinition)
                .Where(x => x.ModelTriageIssueId == issue.Id)
                .OrderByDescending(x => x.BuildNumber)
                .Take(20)
                .Select(x => new Result()
                {
                    BuildNumber = x.BuildNumber,
                    BuildUri = DevOpsUtil.GetBuildUri(x.ModelBuild.ModelBuildDefinition.AzureOrganization, x.ModelBuild.ModelBuildDefinition.AzureProject, x.BuildNumber),
                    JobName = x.JobName
                })
                .ToListAsync();
        }
    }
}
    
