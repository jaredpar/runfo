
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Microsoft.Extensions.Logging.Abstractions;
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
        public DotNetQueryUtilFactory QueryUtilFactory { get; }
        public string? SearchText { get; set; }
        public string? TrackingKind { get; set; }
        public string? Definition { get; set; }
        public List<Result> Results { get; set; } = new List<Result>();
        [BindProperty]
        public int PopulateCount { get; set; }
        public int ModelTrackingIssueId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsActive { get; set; }

        public TrackingIssueModel(TriageContext context, DotNetQueryUtilFactory queryUtilFactory)
        {
            Context = context;
            QueryUtilFactory = queryUtilFactory;
        }

        public async Task OnGetAsync(int id)
        {
            var issue = await Context.ModelTrackingIssues
                .Where(x => x.Id == id)
                .Include(x => x.ModelBuildDefinition)
                .SingleAsync();
            ModelTrackingIssueId = id;
            SearchText = issue.SearchRegexText;
            TrackingKind = issue.TrackingKind.ToString();
            IsActive = issue.IsActive;
            Definition = issue.ModelBuildDefinition?.DefinitionName;

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

        public async Task<IActionResult> OnPostAsync(int id, bool close)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == id)
                .SingleAsync()
                .ConfigureAwait(false);
            if (close)
            {
                modelTrackingIssue.IsActive = false;
                await Context.SaveChangesAsync();
                return RedirectToPage("./Index");
            }
            else
            {
                try
                {
                    var queryUtil = await QueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
                    var triageContextUtil = new TriageContextUtil(Context);
                    var trackingIssueUtil = new TrackingIssueUtil(queryUtil, triageContextUtil, NullLogger.Instance);
                    var query = modelTrackingIssue.ModelBuildDefinition is object
                        ? Context.ModelBuildAttempts.Where(x => x.ModelBuild.ModelBuildDefinitionId == modelTrackingIssue.ModelBuildDefinitionId)
                        : Context.ModelBuildAttempts;
                    query = query
                        .OrderByDescending(x => x.ModelBuild.BuildNumber)
                        .Take(PopulateCount)
                        .Include(x => x.ModelBuild)
                        .ThenInclude(x => x.ModelBuildDefinition);
                    var attempts = await query.ToListAsync();
                    foreach (var attempt in attempts)
                    {
                        await trackingIssueUtil.TriageAsync(attempt, modelTrackingIssue);
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
                await OnGetAsync(id);
                return Page();
            }
        }
    }
}
    
