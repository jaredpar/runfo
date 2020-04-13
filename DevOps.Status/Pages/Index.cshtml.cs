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
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;

        public List<Issue> BlockingOfficial { get; set; }

        public List<Issue> BlockingNormal { get; set; }
        public List<Issue> BlockingNormalOptional { get; set; }
        public List<Issue> BlockingOuterloop { get; set; }

        public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
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

            BlockingOfficial = await DoSearch(gitHub, "blocking-official-build");
            BlockingNormal = await DoSearch(gitHub, "blocking-clean-ci");
            BlockingNormalOptional = await DoSearch(gitHub, "blocking-clean-ci-optional");
            BlockingOuterloop = await DoSearch(gitHub, "blocking-outerloop");

            static async Task<List<Issue>> DoSearch(GitHubClient gitHub, string label)
            {
                var request = new SearchIssuesRequest()
                {
                    Labels = new [] { label },
                    State = ItemState.Open,
                    Type = IssueTypeQualifier.Issue,
                    Repos = { { "dotnet", "runtime" } }
                };
                var result = await gitHub.Search.SearchIssues(request);
                return result.Items.ToList();
            }
        }
    }
}