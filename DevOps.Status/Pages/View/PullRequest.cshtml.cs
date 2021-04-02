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
using Octokit;

namespace DevOps.Status.Pages.View
{
    public class PullRequestModel : PageModel
    {
        public sealed class PullRequestBuildInfo
        {
            public string? BuildUri { get; set; }
            public int BuildNumber { get; set; }
            public BuildResult Result { get; set; }
            public string? DefinitionName { get; set; }
            public string? DefinitionUri { get; set; }
        }

        public TriageContext TriageContext { get; set; }
        public IGitHubClientFactory GitHubClientFactory { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Number { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Repository { get; set; }
        public PullRequest? PullRequest { get; set; }
        public List<PullRequestBuildInfo> Builds { get; set; } = new List<PullRequestBuildInfo>();

        public PullRequestModel(TriageContext triageContext, IGitHubClientFactory gitHubClientFactory)
        {
            TriageContext = triageContext;
            GitHubClientFactory = gitHubClientFactory;
        }

        public async Task OnGet()
        {
            if (Number is null || string.IsNullOrEmpty(Repository))
            {
                return;
            }

            var gitHubClient = await GitHubClientFactory.CreateForAppAsync(DotNetConstants.GitHubOrganization, Repository);
            PullRequest = await gitHubClient.PullRequest.Get(DotNetConstants.GitHubOrganization, Repository, Number.Value);

            var builds = await TriageContext
                .ModelBuilds
                .Include(x => x.ModelBuildDefinition)
                .Where(x =>
                    x.GitHubOrganization == DotNetConstants.GitHubOrganization &&
                    x.GitHubRepository == Repository &&
                    x.PullRequestNumber == Number)
                .OrderByDescending(x => x.BuildNumber)
                .ToListAsync();
            Builds = builds
                .Select(b => new PullRequestBuildInfo()
                {
                    BuildUri = b.GetBuildResultInfo().BuildUri,
                    BuildNumber = b.BuildNumber,
                    Result = b.BuildResult,
                    DefinitionUri = b.ModelBuildDefinition.GetDefinitionInfo().DefinitionUri,
                    DefinitionName = b.ModelBuildDefinition.DefinitionName,
                })
                .ToList();
        }
    }
}