#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        public DotNetQueryUtilFactory QueryUtilFactory { get; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? QueryString { get; set; }

        public List<(PullRequest PullRequest, Build Build)> MergedPullRequestBuilds { get; set; } = new List<(PullRequest PullRequest, Build Build)>();

        public double PassRate { get; set; }

        public MergedPullRequestsModel(DotNetQueryUtilFactory queryUtilFactory)
        {
            QueryUtilFactory = queryUtilFactory;
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

            var queryUtil = await QueryUtilFactory.CreateForUserAsync();
            var gitHubInfo = new GitHubInfo("dotnet", optionSet.Repository.ToLower());
            var definitions = optionSet.Definition is null
                ? null
                : new[] { DotNetUtil.GetBuildDefinitionKeyFromFriendlyName(optionSet.Definition)!.Value.Id };
            MergedPullRequestBuilds = await (queryUtil.EnumerateMergedPullRequestBuilds(
                gitHubInfo,
                optionSet.Project,
                definitions)
                .Take(optionSet.SearchCount));
            PassRate = MergedPullRequestBuilds.Count(x => x.Build.Result == BuildResult.Succeeded) / (double)MergedPullRequestBuilds.Count;
        }
    }
}
