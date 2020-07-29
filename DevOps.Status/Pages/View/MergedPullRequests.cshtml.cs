#nullable enable

using System;
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
        public sealed class SearchOptions
        {
            public string Repository { get; set; } = "runtime";

            public string Definition { get; set; } = "runtime";

            public int Count { get; set; } = 50;

            public string UserQuery
            {
                get
                {
                    var builder = new StringBuilder();
                    if (!string.IsNullOrEmpty(Repository))
                    {
                        builder.Append($"repository:{Repository} ");
                    }

                    if (!string.IsNullOrEmpty(Definition))
                    {
                        builder.Append($"definition:{Definition} ");
                    }

                    builder.Append($"count:{Count} ");
                    return builder.ToString();
                }
            }

            public SearchOptions()
            {
            }

            public void Parse(string query)
            {
                foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(query))
                {
                    switch (tuple.Name.ToLower())
                    {
                        case "repository":
                            Repository = tuple.Value;
                            break;
                        case "definition":
                            Definition = tuple.Value;
                            break;
                        case "count":
                            Count = int.Parse(tuple.Value);
                            break;
                        default:
                            throw new Exception($"Invalid option {tuple.Name}");
                    }
                }
            }

            public Dictionary<string, string> GetQueryParams() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "repository", Repository },
                { "definition", Definition },
                { "count", Count.ToString() }
            };
        }

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

        public TriageContext TriageContext { get; }

        [BindProperty(SupportsGet = true)]
        public SearchOptions Options { get; set; } = new SearchOptions();

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? QueryString { get; set; }

        public List<MergedBuildInfo> MergedPullRequestBuilds { get; set; } = new List<MergedBuildInfo>();

        public string? PassRate { get; set; }

        public MergedPullRequestsModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task<IActionResult> OnGet()
        {
            if (!string.IsNullOrEmpty(QueryString))
            {
                Options.Parse(QueryString);
                return RedirectToPage("/View/MergedPullRequests", Options.GetQueryParams());
            }

            IQueryable<ModelBuild> query = TriageContext
                .ModelBuilds
                .Include(x => x.ModelBuildDefinition)
                .Where(x => x.IsMergedPullRequest && x.GitHubOrganization == DotNetUtil.GitHubOrganization && x.GitHubRepository == Options.Repository.ToLower());
            if (Options.Definition is object)
            {
                var definitionId = DotNetUtil.GetDefinitionIdFromFriendlyName(Options.Definition);
                query = query.Where(x => x.ModelBuildDefinition.DefinitionId == definitionId);
            }

            query = query.Take(Options.Count);

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
