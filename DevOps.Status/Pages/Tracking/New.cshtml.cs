using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Function;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DevOps.Status.Pages.Tracking
{
    public class NewTrackingIssueModel : PageModel
    {
        public TriageContext TriageContext { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public IGitHubClientFactory GitHubClientFactory { get; }
        public FunctionQueueUtil FunctionQueueUtil { get; }
        public ILogger Logger { get; }

        [BindProperty(SupportsGet = true)]
        public string? IssueTitle { get; set; }
        [BindProperty(SupportsGet = true)]
        public TrackingKind TrackingKind { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchText { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Definition { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? GitHubOrganization { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? GitHubRepository { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? GitHubIssueUri { get; set; }

        public string? ErrorMessage { get; set; }

        public NewTrackingIssueModel(TriageContext triageContext, FunctionQueueUtil functionQueueUtil, IGitHubClientFactory gitHubClientFactory, ILogger<NewTrackingIssueModel> logger)
        {
            TriageContext = triageContext;
            TriageContextUtil = new TriageContextUtil(triageContext);
            GitHubClientFactory = gitHubClientFactory;
            FunctionQueueUtil = functionQueueUtil;
            Logger = logger;
        }

        public void OnGet()
        {
            if (string.IsNullOrEmpty(Definition))
            {
                Definition = "roslyn-ci";
            }

            if (TrackingKind == TrackingKind.Unknown)
            {
                TrackingKind = TrackingKind.Timeline;
            }

            if (string.IsNullOrEmpty(IssueTitle))
            {
                IssueTitle = $"Tracking issue in {Definition}";
            }

            if (string.IsNullOrEmpty(SearchText))
            {
                SearchText = "Error";
            }
        }

        public async Task<IActionResult> OnPost()
        {
            if (TrackingKind == TrackingKind.Unknown)
            {
                ErrorMessage = "Invalid Tracking Kind";
                return Page();
            }

            if (string.IsNullOrEmpty(IssueTitle))
            {
                ErrorMessage = "Need an issue title";
                return Page();
            }

            if (IssueTitle.Length >= ModelTrackingIssue.IssueTitleLengthLimit)
            {
                ErrorMessage = $"Please limit issue title to {ModelTrackingIssue.IssueTitleLengthLimit} characters";
                return Page();
            }

            if (string.IsNullOrEmpty(SearchText))
            {
                ErrorMessage = "Must provide search text";
                return Page();
            }

            ModelBuildDefinition? modelBuildDefinition = null;
            if (!string.IsNullOrEmpty(Definition))
            {
                modelBuildDefinition = await TriageContextUtil.FindModelBuildDefinitionAsync(Definition);
                if (modelBuildDefinition is null)
                {
                    ErrorMessage = $"Cannot find build definition with name or ID: {Definition}";
                    return Page();
                }
            }

            switch (TrackingKind)
            {
                case TrackingKind.HelixLogs:
                    {
                        if (TryParseQueryString<SearchHelixLogsRequest>(out var request))
                        {
                            if (request.HelixLogKinds.Count == 0)
                            {
                                ErrorMessage = "Need to specify at least one log kind to search";
                                return Page();
                            }
                        }
                        else
                        {
                            return Page();
                        }
                    }
                    break;
                case TrackingKind.Test:
                    if (!TryParseQueryString<SearchTestsRequest>(out _))
                    {
                        return Page();
                    }
                    break;

                case TrackingKind.Timeline:
                    if (!TryParseQueryString<SearchTimelinesRequest>(out _))
                    {
                        return Page();
                    }
                    break;
            }

            GitHubIssueKey? issueKey = null;
            if (!string.IsNullOrEmpty(GitHubIssueUri))
            {
                if (GitHubIssueKey.TryCreateFromUri(GitHubIssueUri, out var key))
                {
                    issueKey = key;
                    GitHubOrganization = key.Organization;
                    GitHubRepository = key.Repository;
                }
                else
                {
                    ErrorMessage = $"Invalid GitHub issue link: {GitHubIssueUri}";
                    return Page();
                }
            }
            else if (string.IsNullOrEmpty(GitHubRepository) || string.IsNullOrEmpty(GitHubOrganization))
            {
                ErrorMessage = "Must provide GitHub organization and repository";
                return Page();
            }

            IGitHubClient? gitHubClient;
            try
            {
                gitHubClient = await GitHubClientFactory.CreateForAppAsync(GitHubOrganization, GitHubRepository);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Cannot create GitHub client for that repository: {ex.Message}";
                return Page();
            }

            var modelTrackingIssue = await CreateTrackingIssue(gitHubClient);
            return RedirectToPage(
                "./Issue",
                new { id = modelTrackingIssue.Id });

            async Task<ModelTrackingIssue> CreateTrackingIssue(IGitHubClient gitHubClient)
            {
                var issueKey = await GetOrCreateGitHubIssueAsync(gitHubClient);
                var modelTrackingIssue = new ModelTrackingIssue()
                {
                    IsActive = true,
                    IssueTitle = IssueTitle,
                    TrackingKind = TrackingKind,
                    SearchQuery = SearchText,
                    ModelBuildDefinition = modelBuildDefinition,
                    GitHubOrganization = issueKey.Organization,
                    GitHubRepository = issueKey.Repository,
                    GitHubIssueNumber = issueKey.Number,
                };

                TriageContext.ModelTrackingIssues.Add(modelTrackingIssue);
                await TriageContext.SaveChangesAsync();
                await InitialTriageAsync(modelTrackingIssue);

                return modelTrackingIssue;
            }

            async Task InitialTriageAsync(ModelTrackingIssue modelTrackingIssue)
            {
                var extraQuery = modelTrackingIssue.ModelBuildDefinitionId.HasValue
                    ? "started:~3"
                    : "started:~1";

                await FunctionQueueUtil.QueueTriageBuildAttempts(TriageContextUtil, modelTrackingIssue, extraQuery);

                // Issues are bulk updated on a 15 minute cycle. This is a new issue though so want to make sure that
                // the user sees progress soon. Schedule two manual updates in the near future on this so the issue 
                // gets rolling then it will fall into the 15 minute bulk cycle.
                await FunctionQueueUtil.QueueUpdateIssueAsync(modelTrackingIssue, TimeSpan.FromSeconds(30));
                await FunctionQueueUtil.QueueUpdateIssueAsync(modelTrackingIssue, TimeSpan.FromMinutes(2));
            }

            async Task<GitHubIssueKey> GetOrCreateGitHubIssueAsync(IGitHubClient gitHubClient)
            {
                if (issueKey is { } key)
                {
                    await TrackingGitHubUtil.EnsureGitHubIssueHasMarkers(gitHubClient, key);
                    return key;
                }

                var newIssue = new NewIssue(IssueTitle)
                {
                    Body = TrackingGitHubUtil.WrapInStartEndMarkers("Runfo Creating Tracking Issue (data being generated)")
                };

                var issue = await gitHubClient.Issue.Create(GitHubOrganization, GitHubRepository, newIssue);
                return issue.GetIssueKey();
            }

            bool TryParseQueryString<T>(out T value)
                where T : ISearchRequest, new()
            {
                value = new T();
                try
                {
                    value.ParseQueryString(SearchText ?? "");
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.ToString();
                    return false;
                }
            }
        }
    }
}
