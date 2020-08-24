using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mono.Options;
using Octokit;
using Org.BouncyCastle.Asn1;

namespace DevOps.Status.Pages.View
{
    public class MergedPullRequestsModel : PageModel
    {
        public sealed class MergedBuildInfo
        {
            public string? Repository { get; set; }
            public string? PullRequestUri { get; set; }
            public int PullRequestNumber { get; set; }
            public string? BuildUri { get; set; }
            public int BuildNumber { get; set; }
            public string? DefinitionName { get; set; }
            public string? DefinitionUri { get; set; }
            public BuildResult Result { get; set; }
        }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }

        public List<MergedBuildInfo> MergedPullRequestBuilds { get; set; } = new List<MergedBuildInfo>();

        public string? PassRate { get; set; }

        public MergedPullRequestsModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrEmpty(Query))
            {
                Query = new StatusBuildSearchOptions() { Repository = "runtime", Count = 10 }.GetUserQueryString();
                return Page();
            }

            var options = new StatusBuildSearchOptions();
            options.Parse(Query);
            var query = options.GetModelBuildsQuery(
                TriageContextUtil,
                beforeCountFunc: q => q.Where(x => x.PullRequestNumber != null && x.IsMergedPullRequest));
            var builds = (await query.ToListAsync())
                .Select(b =>
                {
                    var prNumber = b.PullRequestNumber!.Value;
                    return new MergedBuildInfo()
                    {
                        Repository = b.GitHubRepository,
                        PullRequestUri = GitHubPullRequestKey.GetPullRequestUri(b.GitHubOrganization, b.GitHubRepository, prNumber),
                        PullRequestNumber = prNumber,
                        BuildUri = TriageContextUtil.GetBuildInfo(b).BuildUri,
                        BuildNumber = b.BuildNumber,
                        DefinitionUri = TriageContextUtil.GetBuildDefinitionInfo(b.ModelBuildDefinition).DefinitionUri,
                        DefinitionName = b.ModelBuildDefinition.DefinitionName,
                        Result = b.BuildResult!.Value,
                    };
                })
                .ToList();
            MergedPullRequestBuilds = builds;

            var rate = builds.Count(x => x.Result == BuildResult.Succeeded || x.Result == BuildResult.PartiallySucceeded) / (double)builds.Count;
            PassRate = (100 * rate).ToString("F");
            return Page();
        }
    }
}
