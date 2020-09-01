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
        public DotNetQueryUtil DotNetQueryUtil { get; }

        public DotNetQueryUtilFactory(DevOpsServer devOpsServer, BlobStorageUtil blobStorageUtil, StatusGitHubClientFactory gitHubClientFactory)
        {
            // TODO: this needs to use the Bearer token from the VSO auth
            DevOpsServer = devOpsServer;
            BlobStorageUtil = BlobStorageUtil;
            AzureUtil = new CachingAzureUtil(blobStorageUtil, devOpsServer);
            GitHubClientFactory = gitHubClientFactory;
            DotNetQueryUtil = new DotNetQueryUtil(devOpsServer, AzureUtil);
        }
    }
}
