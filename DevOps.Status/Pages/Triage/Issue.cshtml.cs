
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
    public class TriageIssueModel : PageModel
    {
        public sealed class Result
        {
            public int BuildNumber { get; set; }

            public string? BuildUri { get; set; }

            public string? BuildKind { get; set; }

            public string? JobName { get; set; }

            public int Attempt { get; set; }

            public string? RepositoryName { get; set; }

            public string? RepositoryUri { get; set; }
        }

        public TriageContext Context { get; }

        public string SearchText { get; set; } = "";

        public List<Result> Results { get; set; } = new List<Result>();

        public TriageIssueModel(TriageContext context)
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
                    BuildKind = x.ModelBuild.PullRequestNumber is object ? "Pull Request" : "Rolling",
                    JobName = x.JobName,
                    Attempt = x.Attempt,
                    RepositoryName = x.ModelBuild.GitHubRepository,
                    RepositoryUri = x.ModelBuild.GitHubRepository is object 
                        ? $"https://github.com/{x.ModelBuild.GitHubOrganization}/{x.ModelBuild.GitHubRepository}"
                        : ""
                })
                .ToListAsync();
        }
    }
}
    
