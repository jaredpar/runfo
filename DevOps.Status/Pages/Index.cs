
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages
{
    public class IndexModel : PageModel
    {
        public DevOpsServer Server { get; }

        [BindProperty]
        public string BuildSearch { get; set; }

        public List<BuildStatus> Builds { get; set; } = new List<BuildStatus>();

        public IndexModel(DevOpsServer server)
        {
            Server = server;
        }

        public async Task OnPost()
        {
            var queryUtil = new DotNetQueryUtil(Server);
            var builds = await queryUtil.ListBuildsAsync(BuildSearch);
            Builds = builds
                .Select(x =>
                {
                    var buildInfo = x.GetBuildInfo();
                    return new BuildStatus()
                    {
                        Result = x.Result.ToString(),
                        BuildNumber = buildInfo.Number,
                        BuildUri = buildInfo.BuildUri
                    };
                })
                .ToList();
        }
    }
}