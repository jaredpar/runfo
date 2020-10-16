using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DevOps.Status.Pages.View
{
    public class BuildModel : PageModel
    {
        public TriageContextUtil TriageContextUtil { get; }

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

        public BuildModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGet()
        {
            if (!(Number is { } number))
            {
                return;
            }

            var organization = DotNetUtil.AzureOrganization;
            var project = DotNetUtil.DefaultAzureProject;
            var buildKey = new BuildKey(organization, project, number);
            var buildId = TriageContextUtil.GetModelBuildId(buildKey);

            var modelBuild = await PopulateBuildInfo();
            await PopulateTimeline();
            await PopulateTests();

            async Task<ModelBuild?> PopulateBuildInfo()
            {
                var modelBuild = await TriageContextUtil
                    .GetModelBuildQuery(buildKey)
                    .Include(x => x.ModelBuildDefinition)
                    .FirstOrDefaultAsync();
                if (modelBuild is null)
                {
                    return null;
                }

                BuildUri = DevOpsUtil.GetBuildUri(organization, project, number);
                BuildResult = modelBuild.BuildResult ?? BuildResult.None;
                Repository = $"{modelBuild.GitHubOrganization}/{modelBuild.GitHubRepository}";
                RepositoryUri = $"https://{modelBuild.GitHubOrganization}/{modelBuild.GitHubRepository}";
                DefinitionName = modelBuild.ModelBuildDefinition.DefinitionName;
                TargetBranch = modelBuild.GitHubTargetBranch;

                if (modelBuild.PullRequestNumber is { } prNumber)
                {
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
                    .Where(x => x.ModelBuildId  == buildId)
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
                    .Where(x => x.ModelBuildId == buildId)
                    .Include(x => x.ModelTestRun)
                    .Include(x => x.ModelBuild);
                var modelTestResults = await query.ToListAsync();

                TestResultsDisplay = new TestResultsDisplay(modelTestResults)
                {
                    IncludeBuildColumn = false,
                    IncludeBuildKindColumn = false,
                    IncludeTestFullNameColumn = true,
                    IncludeTestFullNameLinks = true,
                };

                if (modelBuild is object)
                {
                    TestResultsDisplay.BuildsRequest = new SearchBuildsRequest()
                    {
                        Definition = modelBuild.ModelBuildDefinition.DefinitionName,
                        Started = new DateRequestValue(dayQuery: 7)
                    };
                }
            }
        }
    }
}
