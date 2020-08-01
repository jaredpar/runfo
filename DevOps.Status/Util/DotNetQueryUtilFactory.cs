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
        public BlobStorageUtil BlobStorageUtil { get; }
        public IAzureUtil AzureUtil { get; }
        public StatusGitHubClientFactory GitHubClientFactory { get; }

        public DotNetQueryUtilFactory(DevOpsServer devOpsServer, BlobStorageUtil blobStorageUtil, StatusGitHubClientFactory gitHubClientFactory)
        {
            DevOpsServer = devOpsServer;
            BlobStorageUtil = BlobStorageUtil;
            AzureUtil = new CachingAzureUtil(BlobStorageUtil, devOpsServer);
            GitHubClientFactory = gitHubClientFactory;
        }

        public DotNetQueryUtil CreateForAnonymous() => new DotNetQueryUtil(DevOpsServer, AzureUtil, GitHubClientFactory.CreateAnonymous());

        public async Task<DotNetQueryUtil> CreateForUserAsync() => new DotNetQueryUtil(DevOpsServer, AzureUtil, await GitHubClientFactory.CreateForUserAsync());

        public async Task<DotNetQueryUtil> CreateForUserOrAnonymousAsync() => new DotNetQueryUtil(DevOpsServer, AzureUtil, await GitHubClientFactory.CreateForUserOrAnonymousAsync());
    }
}
