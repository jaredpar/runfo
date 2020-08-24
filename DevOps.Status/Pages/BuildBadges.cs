using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages
{
    public class BuildBadgesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BuildBadgesModel> _logger;

        public List<Issue> BlockingOfficial { get; set; } = new List<Issue>();
        public List<Issue> BlockingNormal { get; set; } = new List<Issue>();
        public List<Issue> BlockingNormalOptional { get; set; } = new List<Issue>();
        public List<Issue> BlockingOuterloop { get; set; } = new List<Issue>();

        public BuildBadgesModel(IConfiguration configuration, ILogger<BuildBadgesModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task OnGet()
        {
            // so much hack
            var token = _configuration["GitHub:Token"];
            var gitHub = new GitHubClient(new ProductHeaderValue("RuntimeStatusPage"));
            gitHub.Credentials = new Credentials("jaredpar", token);

            await DoSearch(BlockingOfficial, gitHub, "blocking-official-build");
            await DoSearch(BlockingNormal, gitHub, "blocking-clean-ci");
            await DoSearch(BlockingNormalOptional, gitHub, "blocking-clean-ci-optional");
            await DoSearch(BlockingOuterloop, gitHub, "blocking-outerloop");

            static async Task DoSearch(List<Issue> list, GitHubClient gitHub, string label)
            {
                var request = new SearchIssuesRequest()
                {
                    Labels = new [] { label },
                    State = ItemState.Open,
                    Type = IssueTypeQualifier.Issue,
                    Repos = { { "dotnet", "runtime" } }
                };
                var result = await gitHub.Search.SearchIssues(request);
                list.AddRange(result.Items);
            }
        }
    }
}