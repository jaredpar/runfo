using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DevOps.Status.Pages.View
{
    public class BuildModel : PageModel
    {
        public TriageContextUtil TriageContextUtil { get; }
        public IGitHubClientFactory GitHubClientFactory { get; }
        public ILogger Logger { get; }

        [BindProperty(SupportsGet = true)]
        public int? Number { get; set; }
        public string? BuildUri { get; set; }
        public BuildResult BuildResult { get; set; }
        public int Attempts { get; set; }
        public string? TargetBranch { get; set; }
        public string? Repository { get; set; }
        public string? RepositoryUri { get; set; }
        public string? DefinitionName { get; set; }
        public GitHubPullRequestKey? PullRequestKey { get; set; }
        public TimelineIssuesDisplay TimelineIssuesDisplay { get; set; } = TimelineIssuesDisplay.Empty;
        public TestResultsDisplay TestResultsDisplay { get; set; } = TestResultsDisplay.Empty;
        public List<GitHubIssueKey> GitHubIssues { get; set; } = new List<GitHubIssueKey>();
        public string? GitHubIssueAddErrorMessage { get; set; }

        public BuildModel(TriageContextUtil triageContextUtil, IGitHubClientFactory gitHubClientFactory, ILogger<BuildModel> logger)
        {
            TriageContextUtil = triageContextUtil;
            GitHubClientFactory = gitHubClientFactory;
            Logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            if (!(Number is { } number))
            {
                return Page();
            }

            var buildKey = GetBuildKey(number);
            var project = buildKey.Project;
            var organization = buildKey.Organization;
            var buildId = TriageContextUtil.GetModelBuildNameKey(buildKey);

            var modelBuild = await PopulateBuildInfo();
            if (modelBuild is object)
            {
                await PopulateTimeline();
                await PopulateTests();
            }

            return Page();

            async Task<ModelBuild?> PopulateBuildInfo()
            {
                var modelBuild = await TriageContextUtil
                    .GetModelBuildQuery(buildKey)
                    .Include(x => x.ModelGitHubIssues)
                    .FirstOrDefaultAsync();
                if (modelBuild is null)
                {
                    return null;
                }

                BuildUri = DevOpsUtil.GetBuildUri(organization, project, number);
                BuildResult = modelBuild.BuildResult;
                Repository = $"{modelBuild.GitHubOrganization}/{modelBuild.GitHubRepository}";
                RepositoryUri = $"https://{modelBuild.GitHubOrganization}/{modelBuild.GitHubRepository}";
                DefinitionName = modelBuild.DefinitionName;
                TargetBranch = modelBuild.GitHubTargetBranch;
                GitHubIssues.Clear();
                GitHubIssues.AddRange(modelBuild.ModelGitHubIssues.Select(x => x.GetGitHubIssueKey()));

                if (modelBuild.PullRequestNumber is { } prNumber)
                {
                    Debug.Assert(modelBuild.GitHubOrganization is object);
                    Debug.Assert(modelBuild.GitHubRepository is object);
                    PullRequestKey = new GitHubPullRequestKey(
                        modelBuild.GitHubOrganization,
                        modelBuild.GitHubRepository,
                        prNumber);
                }

                return modelBuild;
            }

            async Task PopulateTimeline()
            {
                var query = TriageContextUtil
                    .Context
                    .ModelTimelineIssues
                    .Where(x => x.ModelBuildId  == modelBuild.Id)
                    .Include(x => x.ModelBuild);
                TimelineIssuesDisplay = await TimelineIssuesDisplay.Create(
                    query,
                    includeBuildColumn: false,
                    includeIssueTypeColumn: true,
                    includeAttemptColumn: true);
                Attempts = TimelineIssuesDisplay.Issues.Count > 0
                    ? TimelineIssuesDisplay.Issues.Max(x => x.Attempt)
                    : 1;
            }

            async Task PopulateTests()
            {
                var query = TriageContextUtil
                    .Context
                    .ModelTestResults
                    .Where(x => x.ModelBuildId == modelBuild.Id)
                    .Include(x => x.ModelTestRun)
                    .Include(x => x.ModelBuild);
                var modelTestResults = await query.ToListAsync();

                TestResultsDisplay = new TestResultsDisplay(modelTestResults)
                {
                    IncludeBuildColumn = false,
                    IncludeBuildKindColumn = false,
                    IncludeTestFullNameColumn = true,
                    IncludeTestFullNameLinks = true,
                    IncludeErrorMessageColumn = true,
                };

                if (modelBuild is object)
                {
                    TestResultsDisplay.BuildsRequest = new SearchBuildsRequest()
                    {
                        Definition = modelBuild.DefinitionName,
                        Started = new DateRequestValue(dayQuery: 7)
                    };
                }
            }
        }

        public async Task<IActionResult> OnPost(string gitHubIssueUri, string formAction)
        {
            if (!GitHubIssueKey.TryCreateFromUri(gitHubIssueUri, out var issueKey))
            {
                return await OnError("Not a valid GitHub Issue Url");
            }

            var buildKey = GetBuildKey(Number!.Value);

            if (formAction == "addIssue")
            {
                try
                {
                    var modelBuild = await TriageContextUtil.GetModelBuildAsync(buildKey);
                    await TriageContextUtil.EnsureGitHubIssueAsync(modelBuild, issueKey, saveChanges: true);
                }
                catch (DbUpdateException ex)
                {
                    if (!ex.IsUniqueKeyViolation())
                    {
                        throw;
                    }
                }
            }
            else
            {
                var query = TriageContextUtil
                    .GetModelBuildQuery(buildKey)
                    .SelectMany(x => x.ModelGitHubIssues)
                    .Where(x =>
                        x.Organization == issueKey.Organization &&
                        x.Repository == issueKey.Repository &&
                        x.Number == issueKey.Number);

                var modelGitHubIssue = await query.FirstOrDefaultAsync();
                if (modelGitHubIssue is object)
                {
                    TriageContextUtil.Context.ModelGitHubIssues.Remove(modelGitHubIssue);
                    await TriageContextUtil.Context.SaveChangesAsync();
                }
            }

            var util = new TrackingGitHubUtil(GitHubClientFactory, TriageContextUtil.Context, SiteLinkUtil.Published, Logger);
            await util.UpdateAssociatedGitHubIssueAsync(issueKey);

            return await OnGet();

            async Task<IActionResult> OnError(string message)
            {
                GitHubIssueAddErrorMessage = message;
                return await OnGet();
            }
        }

        private static BuildKey GetBuildKey(int buildNumber) => new BuildKey(DotNetConstants.AzureOrganization, DotNetConstants.DefaultAzureProject, buildNumber);
    }
}
