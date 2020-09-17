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
        [BindProperty(SupportsGet = true, Name = "page")]
        public int PageNumber { get; set; }
        public int? NextPageNumber { get; set; }
        public int? PreviousPageNumber { get; set; }
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
                Query = new SearchBuildsRequest() { Repository = "runtime" }.GetQueryString();
                return Page();
            }

            const int pageSize = 50;
            var options = new SearchBuildsRequest();
            options.ParseQueryString(Query);
            IQueryable<ModelBuild> query = TriageContextUtil.Context.ModelBuilds
                .Include(x => x.ModelBuildDefinition);
            var results = await options.FilterBuilds(query)
                .Where(x => x.PullRequestNumber != null && x.IsMergedPullRequest)
                .OrderByDescending(x => x.BuildNumber)
                .Skip(PageNumber * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var builds = results
                .Select(b =>
                {
                    var prNumber = b.PullRequestNumber!.Value;
                    return new MergedBuildInfo()
                    {
                        Repository = b.GitHubRepository,
                        PullRequestUri = GitHubPullRequestKey.GetPullRequestUri(b.GitHubOrganization, b.GitHubRepository, prNumber),
                        PullRequestNumber = prNumber,
                        BuildUri = b.GetBuildResultInfo().BuildUri,
                        BuildNumber = b.BuildNumber,
                        DefinitionUri = b.ModelBuildDefinition.GetDefinitionKey().DefinitionUri,
                        DefinitionName = b.ModelBuildDefinition.DefinitionName,
                        Result = b.BuildResult!.Value,
                    };
                })
                .ToList();
            MergedPullRequestBuilds = builds;

            var rate = builds.Count(x => x.Result == BuildResult.Succeeded || x.Result == BuildResult.PartiallySucceeded) / (double)builds.Count;
            PassRate = (100 * rate).ToString("F");
            PreviousPageNumber = PageNumber > 0 ? PageNumber - 1 : (int?)null;
            NextPageNumber = PageNumber + 1;
            return Page();
        }
    }
}
