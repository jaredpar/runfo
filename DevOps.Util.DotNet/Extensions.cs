#nullable enable

using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public static class Extensions
    {
        #region DevOpsServer

        public static Task<List<Timeline>> ListTimelineAttemptsAsync(this DevOpsServer server, string project, int buildNumber)
        {
            var azureUtil = new AzureUtil(server);
            return azureUtil.ListTimelineAttemptsAsync(project, buildNumber);
        }

        #endregion
    }
}
