
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

namespace DevOps.Status.Pages.Tracking
{
    public class TrackingIssueModel : PageModel
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

        public TrackingIssueModel(TriageContext context)
        {
            Context = context;
        }

        public async Task OnGetAsync(int id)
        {
            var issue = await Context.ModelTrackingIssues
                .Where(x => x.Id == id)
                .SingleAsync();
            SearchText = issue.SearchRegexText;

            Results = await Context.ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == issue.Id)
                .Include(x => x.ModelBuildAttempt)
                .ThenInclude(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Take(20)
                .Select(x => new Result()
                {
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    BuildUri = DevOpsUtil.GetBuildUri(x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization, x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject, x.ModelBuildAttempt.ModelBuild.BuildNumber),
                    BuildKind = x.ModelBuildAttempt.ModelBuild.PullRequestNumber is object ? "Pull Request" : "Rolling",
                    JobName = x.JobName,
                    Attempt = x.ModelBuildAttempt.Attempt,
                    RepositoryName = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    RepositoryUri = x.ModelBuildAttempt.ModelBuild.GitHubRepository is object 
                        ? $"https://github.com/{x.ModelBuildAttempt.ModelBuild.GitHubOrganization}/{x.ModelBuildAttempt.ModelBuild.GitHubRepository}"
                        : ""
                })
                .ToListAsync();
        }
    }
}
    
