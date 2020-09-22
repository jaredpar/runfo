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

            if (string.IsNullOrEmpty(GitHubRepository) || string.IsNullOrEmpty(GitHubOrganization))
            {
                ErrorMessage = "Must provide GitHub organization and repository";
                return Page();
            }

            IGitHubClient? gitHubClient = null;
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
                var issueKey = await CreateGitHubIssueAsync(gitHubClient);
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
                // Picking how many days to triage here. If there is no definition then there will be 
                // a _lot_ more builds in the first day alone so just triage that far.
                var days = modelBuildDefinition is object ? 3 : 1;
                var request = new SearchBuildsRequest() { Queued = new DateRequestValue(days) };
                await FunctionQueueUtil.QueueTriageBuildQuery(request, TriageContext, limit: 20);
            }

            async Task<GitHubIssueKey> CreateGitHubIssueAsync(IGitHubClient gitHubClient)
            {
                var newIssue = new NewIssue("Temporary title")
                {
                    Body = "Runfo Creating Tracking Issue"
                };

                var issue = await gitHubClient.Issue.Create(GitHubOrganization, GitHubRepository, newIssue);

                return issue.GetIssueKey();
            }
        }
    }
}
