using DevOps.Util;
using DevOps.Util.DotNet;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    public sealed class DotNetQueryUtilFactory
    {
        public DevOpsServer DevOpsServer { get; }
        public GitHubClientFactory GitHubClientFactory { get; }

        public DotNetQueryUtilFactory(DevOpsServer devOpsServer, GitHubClientFactory gitHubClientFactory)
        {
            DevOpsServer = devOpsServer;
            GitHubClientFactory = gitHubClientFactory;
        }

        public DotNetQueryUtil CreateForAnonymous() => new DotNetQueryUtil(DevOpsServer, GitHubClientFactory.CreateAnonymous());

        public async Task<DotNetQueryUtil> CreateForUserAsync() => new DotNetQueryUtil(DevOpsServer, await GitHubClientFactory.CreateForUserAsync());

        public async Task<DotNetQueryUtil> CreateForUserOrAnonymousAsync() => new DotNetQueryUtil(DevOpsServer, await GitHubClientFactory.CreateForUserOrAnonymousAsync());
    }
}
