
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
using DevOps.Util.DotNet.Function;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;
using YamlDotNet.Serialization.NodeTypeResolvers;

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
            public DateTime? Queued { get; set; }
        }

        public struct HitCountInfo
        {
            public int Today { get; set; }
            public int Week { get; set; }
            public int Month { get; set; }
        }

        public TriageContextUtil TriageContextUtil { get; }
        public IGitHubClientFactory GitHubClientFactory { get;  }
        public FunctionQueueUtil FunctionQueueUtil { get;  }

        public TriageContext Context => TriageContextUtil.Context;
        [BindProperty]
        public string? IssueTitle { get; set; }
        public string? SearchQuery { get; set; }
        public string? TrackingKind { get; set; }
        public string? Definition { get; set; }
        public string? GitHubIssueUri { get; set; }
        [BindProperty]
        public string? PopulateBuildsQuery { get; set; }
        public HitCountInfo HitCount { get; set; }
        public List<Result> Results { get; set; } = new List<Result>();
        public int ModelTrackingIssueId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsActive { get; set; }
        public PaginationDisplay? PaginationDisplay { get; set; }

        public TrackingIssueModel(TriageContextUtil triageContextUtil, FunctionQueueUtil functionQueueUtil, IGitHubClientFactory gitHubClientFactory)
        {
            TriageContextUtil = triageContextUtil;
            FunctionQueueUtil = functionQueueUtil;
            GitHubClientFactory = gitHubClientFactory;
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

            var dateTimeUtil = new DateTimeUtil();
            Results = await Context.ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == issue.Id)
                .Include(x => x.ModelBuildAttempt)
                .ThenInclude(x => x.ModelBuild)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
                .Select(x => new Result()
                {
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    BuildUri = DevOpsUtil.GetBuildUri(x.ModelBuildAttempt.ModelBuild.AzureOrganization, x.ModelBuildAttempt.ModelBuild.AzureProject, x.ModelBuildAttempt.ModelBuild.BuildNumber),
                    BuildKind = x.ModelBuildAttempt.ModelBuild.PullRequestNumber is object ? "Pull Request" : "Rolling",
                    JobName = x.JobName,
                    Attempt = x.ModelBuildAttempt.Attempt,
                    RepositoryName = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    RepositoryUri = x.ModelBuildAttempt.ModelBuild.GitHubRepository is object
                        ? $"https://github.com/{x.ModelBuildAttempt.ModelBuild.GitHubOrganization}/{x.ModelBuildAttempt.ModelBuild.GitHubRepository}"
                        : "",
                    Queued = dateTimeUtil.ConvertDateTime(x.ModelBuildAttempt.ModelBuild.QueueTime),
                })
                .AsNoTracking()
                .ToListAsync();

            var now = dateTimeUtil.Now;
            HitCount = new HitCountInfo()
            {
                Today =  await GetHitCount(now - TimeSpan.FromDays(1)),
                Week =  await GetHitCount(now - TimeSpan.FromDays(7)),
                Month =  await GetHitCount(now - TimeSpan.FromDays(30)),
            };

            async Task<int> GetHitCount(DateTime before) => await Context
                .ModelTrackingIssueResults
                .Where(x => x.ModelTrackingIssueId == ModelTrackingIssueId && x.IsPresent && x.ModelBuildAttempt.ModelBuild.QueueTime > before)
                .CountAsync();
        }

        public async Task<IActionResult> OnPostAsync(int id, string formAction)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == id)
                .Include(x => x.ModelBuildDefinition)
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
                if (modelTrackingIssue.GetGitHubIssueKey() is { } issueKey)
                {
                    try
                    {
                        var gitHubClient = await GitHubClientFactory.CreateForAppAsync(issueKey.Organization, issueKey.Repository);
                        var issueUpdate = new IssueUpdate() { State = ItemState.Closed };
                        var issue = await gitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, issueUpdate);
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = $"Cannot close GitHub issue {ex.Message}";
                        return Page();
                    }
                }

                modelTrackingIssue.IsActive = false;
                await Context.SaveChangesAsync();
                return RedirectToPage("./Index");
            }

            async Task<IActionResult> PopulateAsync()
            {
                var request = new SearchBuildsRequest()
                {
                    Definition = modelTrackingIssue.ModelBuildDefinition?.DefinitionName,
                };

                request.ParseQueryString(string.IsNullOrEmpty(PopulateBuildsQuery) ? "started:~7" : PopulateBuildsQuery);
                if (!request.HasDefinition)
                {
                    ErrorMessage = "Need to filter build results to a definition";
                    return Page();
                }

                await FunctionQueueUtil.QueueTriageBuildQuery(TriageContextUtil, modelTrackingIssue, request);
                await FunctionQueueUtil.QueueUpdateIssueAsync(modelTrackingIssue, delay: TimeSpan.FromMinutes(1));
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
    
