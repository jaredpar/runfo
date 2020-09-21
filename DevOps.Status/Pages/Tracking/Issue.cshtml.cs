
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
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
        [BindProperty]
        public string? IssueTitle { get; set; }
        public string? SearchQuery { get; set; }
        public string? TrackingKind { get; set; }
        public string? Definition { get; set; }
        public string? GitHubIssueUri { get; set; }
        public string? PopulateBuildsQuery { get; set; }
        public List<Result> Results { get; set; } = new List<Result>();
        public int ModelTrackingIssueId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsActive { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }

        public TrackingIssueModel(TriageContext context, DotNetQueryUtilFactory queryUtilFactory)
        {
            Context = context;
            QueryUtilFactory = queryUtilFactory;
        }

        public async Task OnGetAsync(int id, int pageNumber = 0)
        {
            var issue = await Context.ModelTrackingIssues
                .Where(x => x.Id == id)
                .Include(x => x.ModelBuildDefinition)
                .SingleAsync();
            ModelTrackingIssueId = id;
            IssueTitle = issue.IssueTitle;
            SearchQuery = issue.SearchQuery;
            TrackingKind = issue.TrackingKind.ToString();
            GitHubIssueUri = issue.GetGitHubIssueKey()?.IssueUri;
            IsActive = issue.IsActive;
            Definition = issue.ModelBuildDefinition?.DefinitionName;

            const int pageSize = 20;
            var totalPages = await Context.ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == issue.Id)
                .CountAsync() / pageSize;
            PaginationDisplay = new PaginationDisplay(
                "/Tracking/Issue",
                new Dictionary<string, string>()
                {
                    { nameof(id), id.ToString() }
                },
                pageNumber,
                totalPages);

            Results = await Context.ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == issue.Id)
                .Include(x => x.ModelBuildAttempt)
                .ThenInclude(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
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
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync(int id, string formAction)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == id)
                .SingleAsync()
                .ConfigureAwait(false);
            return formAction switch
            {
                "close" => await CloseAsync(),
                "update" => await UpdateAsync(),
                "populate" => await PopulateAsync(),
                _ => throw new Exception($"Invalid action {formAction}"),
            };

            async Task<IActionResult> CloseAsync()
            {
                modelTrackingIssue.IsActive = false;
                await Context.SaveChangesAsync();
                return RedirectToPage("./Index");
            }

            async Task<IActionResult> PopulateAsync()
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    var queryUtil = await QueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
                    var triageContextUtil = new TriageContextUtil(Context);
                    var trackingIssueUtil = new TrackingIssueUtil(queryUtil, triageContextUtil, NullLogger.Instance);
                    var request = new SearchBuildsRequest();
                    request.ParseQueryString(PopulateBuildsQuery ?? "");

                    await trackingIssueUtil.TriageBuildsAsync(modelTrackingIssue, request, cts.Token);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
                await OnGetAsync(id);
                return Page();
            }

            async Task<IActionResult> UpdateAsync()
            {
                modelTrackingIssue.IssueTitle = IssueTitle;
                await Context.SaveChangesAsync();
                await OnGetAsync(id);
                return Page();
            }
        }
    }
}
    
