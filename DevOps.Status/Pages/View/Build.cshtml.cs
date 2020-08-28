using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DevOps.Status.Pages.View
{
    public class BuildModel : PageModel
    {
        public sealed class TimelineData
        {
            public int Attempt { get; set; }
            public string? JobName { get; set;  }
            public string? Line { get; set;  }
        }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true)]
        public int? Number { get; set; }

        public string? BuildUri { get; set; }

        public BuildResult BuildResult { get; set; }

        public int Attempts { get; set; }

        public string? Repository { get; set; }

        public string? RepositoryUri { get; set; }

        public GitHubPullRequestKey? PullRequestKey { get; set; }

        public List<TimelineData> TimelineDataList { get; } = new List<TimelineData>();

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

            await PopulateBuildInfo();
            await PopulateTimeline();
            await PopulateTests();

            async Task PopulateBuildInfo()
            {
                var modelBuild = await TriageContextUtil.FindModelBuildAsync(organization, project, number);
                if (modelBuild is null)
                {
                    return;
                }

                BuildUri = DevOpsUtil.GetBuildUri(organization, project, number);
                BuildResult = modelBuild.BuildResult ?? BuildResult.None;
                Repository = $"{modelBuild.GitHubOrganization}/{modelBuild.GitHubRepository}";
                RepositoryUri = $"https://{modelBuild.GitHubOrganization}/{modelBuild.GitHubRepository}";

                if (modelBuild.PullRequestNumber is { } prNumber)
                {
                    PullRequestKey = new GitHubPullRequestKey(
                        modelBuild.GitHubOrganization,
                        modelBuild.GitHubRepository,
                        prNumber);
                }
            }

            async Task PopulateTimeline()
            {
                var query = TriageContextUtil
                    .Context
                    .ModelTimelineIssues
                    .Where(x =>
                        x.ModelBuild.BuildNumber == number &&
                        x.ModelBuild.ModelBuildDefinition.AzureOrganization == organization &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == project);
                Attempts = 1;
                foreach (var modelTimeline in (await query.ToListAsync()).OrderBy(x => x.Attempt))
                {
                    TimelineDataList.Add(new TimelineData()
                    {
                        Attempt = modelTimeline.Attempt,
                        JobName = modelTimeline.JobName,
                        Line = modelTimeline.Message,
                    });

                    if (modelTimeline.Attempt > Attempts)
                    {
                        Attempts = modelTimeline.Attempt;
                    }
                }
            }

            async Task PopulateTests()
            {
                var query = TriageContextUtil
                    .Context
                    .ModelTestResults
                    .Where(x =>
                        x.ModelBuild.BuildNumber == number &&
                        x.ModelBuild.ModelBuildDefinition.AzureOrganization == organization &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == project)
                    .Include(x => x.ModelTestRun)
                    .Include(x => x.ModelBuild)
                    .ThenInclude(x => x.ModelBuildDefinition);
                var modelTestResults = await query.ToListAsync();
                TestResultsDisplay = new TestResultsDisplay(modelTestResults)
                {
                    IncludeBuildColumn = false,
                    IncludeBuildKindColumn = false,
                    IncludeTestFullNameColumn = true,
                };
            }
        }
    }
}
