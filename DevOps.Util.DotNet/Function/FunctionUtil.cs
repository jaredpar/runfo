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
                    modelBuild.IsMergedPullRequest = true;
                    await triageContextUtil.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// This will collect and delete builds that are past the data retention deadline
        /// </summary>
        public async Task DeleteOldBuilds(TriageContext triageContext, int deleteMax = 25)
        {
            var limitDays = 120;
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
                await RemoveRange(triageContext.ModelOsxDeprovisionRetry.Where(x => x.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelBuildAttempts.Where(x => x.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelTimelineIssues.Where(x => x.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelTestRuns.Where(x => x.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelTestResults.Where(x => x.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelGitHubIssues.Where(x => x.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelTrackingIssueMatches.Where(x => x.ModelBuildAttempt.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await RemoveRange(triageContext.ModelTrackingIssueResults.Where(x => x.ModelBuildAttempt.ModelBuildId == modelBuild.Id)).ConfigureAwait(false);
                await triageContext.SaveChangesAsync().ConfigureAwait(false);
            }

            async Task RemoveRange<TEntity>(IQueryable<TEntity> queryable) where TEntity : class
            {
                var list = await queryable.ToListAsync().ConfigureAwait(false);
                if (list.Count > 0)
                {
                    Logger.LogInformation($"Deleting {list.Count} {typeof(TEntity).Name}");
                    triageContext.RemoveRange(list);
                }
            }
        }
    }
}
