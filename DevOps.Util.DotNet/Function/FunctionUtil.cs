using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Function
{
    public sealed class FunctionUtil
    {
        public ILogger Logger { get; }

        public FunctionUtil(ILogger logger)
        {
            Logger = logger;
        }

        public async Task OnPullRequestMergedAsync(
            DevOpsServer server,
            TriageContextUtil triageContextUtil,
            GitHubPullRequestKey prKey,
            string project,
            CancellationToken cancellationToken = default)
        {
            // Pull requests can trigger builds in multiple definitions. Need to calculate the merged PR build
            // for each of them
            var triageContext = triageContextUtil.Context;
            var allBuilds = await server.ListPullRequestBuildsAsync(prKey, project).ConfigureAwait(false);
            foreach (var group in allBuilds.GroupBy(x => x.Definition.Id))
            {
                var mergedBuild = group
                    .Where(x => x.Status == BuildStatus.Completed && x.Result != BuildResult.Canceled)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();
                if (mergedBuild is object)
                {
                    var modelBuild = await triageContextUtil.EnsureBuildAsync(mergedBuild.GetBuildResultInfo()).ConfigureAwait(false);
                    await triageContextUtil.MarkAsMergedPullRequestAsync(modelBuild, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// This will collect and delete builds that are past the data retention deadline
        /// </summary>
        public async Task DeleteOldBuilds(TriageContext triageContext, int deleteMax = 25)
        {
            var limitDays = 90;
            var limit = DateTime.UtcNow - TimeSpan.FromDays(limitDays);

            var modelBuilds = await triageContext
                .ModelBuilds
                .Where(x => x.StartTime < limit)
                .OrderBy(x => x.StartTime)
                .Take(deleteMax)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var modelBuild in modelBuilds)
            {
                if (modelBuild.AzureProject is object)
                {
                    Logger.LogInformation($"Deleting {modelBuild.GetBuildKey()} ran at {modelBuild.StartTime}");
                }

                triageContext.Remove(modelBuild);
                await triageContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
