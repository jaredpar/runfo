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
        public async Task<int> DeleteOldBuilds(TriageContext triageContext, int deleteMax = 25)
        {
            var limitDays = 30;
            var limit = DateTime.UtcNow - TimeSpan.FromDays(limitDays);
            var count = 0;

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

                try
                {
                    await ShrinkBuild(modelBuild);
                    triageContext.Remove(modelBuild);
                    await triageContext.SaveChangesAsync().ConfigureAwait(false);
                    count++;
                }
                catch (Exception ex)
                {
                    var message = $"Error deleting build {ex.Message}";
                    if (ex.InnerException is object)
                    {
                        message += $" (Inner {ex.InnerException.Message})";
                    }
                    Logger.LogError(message);
                }
            }

            Logger.LogInformation($"Deleted {count} builds");
            return count;

            async Task ShrinkBuild(ModelBuild modelBuild)
            {
                var testCountMax = 100;
                var testCount = await triageContext
                    .ModelTestResults
                    .Where(x => x.ModelBuildId == modelBuild.Id)
                    .CountAsync()
                    .ConfigureAwait(false);
                if (testCount <= testCountMax)
                {
                    return;
                }

                Logger.LogInformation($"{modelBuild.GetBuildKey()} has too many test resultts ({testCount})");

                do
                {
                    var testResults = await triageContext
                        .ModelTestResults
                        .Where(x => x.ModelBuildId == modelBuild.Id)
                        .Take(100)
                        .ToListAsync()
                        .ConfigureAwait(false);
                    if (testResults.Count == 0)
                    {
                        break;
                    }

                    Logger.LogInformation($"Deleting {testResults.Count} test results from {modelBuild.GetBuildKey()}");
                    foreach (var testResult in testResults)
                    {
                        triageContext.Remove(testResult);
                    }

                    await triageContext.SaveChangesAsync().ConfigureAwait(false);
                } while (true);
            }
        }
    }
}
