#nullable enable

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
using Mono.Options;
using Octokit;
using Org.BouncyCastle.Asn1;

namespace DevOps.Status.Pages.View
{
    public class MergedPullRequestsModel : PageModel
    {
        public sealed class SearchOptionSet : OptionSet
        {
            public string Repository { get; set; } = "runtime";

            public string Project { get; set; } = "public";

            public string? Definition { get; set; } = null;

            public int SearchCount { get; set; } = 10;

            public SearchOptionSet()
            {
                Add("d|definition=", "build definition (name|id)(:project)?", d => Definition = d);
                Add("p|project=", "default project to search (public)", p => Project = p);
                Add("r|repository=", "default project to search (public)", r => Repository = r);
                Add("c|count=", "count of builds to show for a definition", (int c) => SearchCount = c);
            }
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

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? QueryString { get; set; }

        public List<MergedBuildInfo> MergedPullRequestBuilds { get; set; } = new List<MergedBuildInfo>();

        public string? PassRate { get; set; }

        public MergedPullRequestsModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task OnGet()
        {
            if (string.IsNullOrEmpty(QueryString))
            {
                return;
            }

            var optionSet = new SearchOptionSet();
            if (optionSet.Parse(DotNetQueryUtil.TokenizeQuery(QueryString)).Count != 0)
            {
                throw OptionSetUtil.CreateBadOptionException();
            }

            IQueryable<ModelBuild> query = TriageContext
                .ModelBuilds
                .Include(x => x.ModelBuildDefinition)
                .Where(x => x.IsMergedPullRequest && x.GitHubOrganization == DotNetUtil.GitHubOrganization && x.GitHubRepository == optionSet.Repository.ToLower());
            if (optionSet.Definition is object)
            {
                var definitionId = DotNetUtil.GetDefinitionIdFromFriendlyName(optionSet.Definition);
                query = query.Where(x => x.ModelBuildDefinition.DefinitionId == definitionId);
            }

            query = query.Take(optionSet.SearchCount);

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
        }
    }
}
