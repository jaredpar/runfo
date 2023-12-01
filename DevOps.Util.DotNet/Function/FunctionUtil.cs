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
            var limitDays = 21;
            var limit = DateTime.UtcNow - TimeSpan.FromDays(limitDays);
            var count = 0;

            var builds = await triageContext
                .ModelBuilds
                .AsNoTracking()
                .Where(x => x.StartTime < limit)
                .OrderBy(x => x.StartTime)
                .Take(deleteMax)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var build in builds)
            {
                if (build.AzureProject is object)
                {
                    Logger.LogInformation($"Deleting {build.GetBuildKey()} ran at {build.StartTime}");
                }

                try
                {
                    await DeleteTrackingIssueMatches(build);
                    await DeleteTimelineIssues(build);
                    await DeleteTestResults(build);
                    triageContext.Remove(build);
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
                    throw;
                }
            }

            Logger.LogInformation($"Deleted {count} builds");
            return count;

            async Task DeleteTimelineIssues(ModelBuild modelBuild)
            {
                var max = 100;
                var totalCount = await triageContext
                    .ModelTimelineIssues
                    .AsNoTracking()
                    .Where(x => x.ModelBuildId == modelBuild.Id)
                    .CountAsync()
                    .ConfigureAwait(false);
                if (totalCount < max)
                {
                    return;
                }

                Logger.LogInformation($"{modelBuild.GetBuildKey()} has too many timeline issues ({totalCount})");
                var deleted = 0;
                do
                {
                    var timelineIssueIds = await triageContext
                        .ModelTimelineIssues
                        .AsNoTracking()
                        .Where(x => x.ModelBuildId == modelBuild.Id)
                        .Take(max)
                        .Select(x => x.Id)
                        .ToListAsync()
                        .ConfigureAwait(false);
                    if (timelineIssueIds.Count == 0)
                    {
                        break;
                    }

                    triageContext.RemoveRange(timelineIssueIds.Select(x => new ModelTimelineIssue() { Id = x }));
                    deleted += timelineIssueIds.Count;
                    Logger.LogInformation($"Deleted {deleted} timeline issues of {totalCount}");
                    await triageContext.SaveChangesAsync().ConfigureAwait(false);
                } while (true);
            }

            async Task DeleteTrackingIssueMatches(ModelBuild modelBuild)
            {
                var buildAttemptIds = await triageContext
                    .ModelBuildAttempts
                    .AsNoTracking()
                    .Where(x => x.ModelBuildId == modelBuild.Id)
                    .Select(x => x.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);
                var count = 0;
                foreach (var buildAttemptId in buildAttemptIds)
                {
                    var trackingIssueIds = await triageContext
                        .ModelTrackingIssueMatches
                        .Where(x => x.ModelBuildAttemptId == buildAttemptId)
                        .Select(x => x.Id)
                        .ToListAsync()
                        .ConfigureAwait(false);
                    if (trackingIssueIds.Count > 0)
                    {
                        count += trackingIssueIds.Count;
                        triageContext.RemoveRange(trackingIssueIds.Select(x => new ModelTrackingIssueMatch() { Id = x }));
                    }
                }

                if (count > 0)
                {
                    Logger.LogInformation($"Deleting {count} tracking issue matches");
                    await triageContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            async Task DeleteTestResults(ModelBuild modelBuild)
            {
                var testCountMax = 100;
                var testCount = await triageContext
                    .ModelTestResults
                    .AsNoTracking()
                    .Where(x => x.ModelBuildId == modelBuild.Id)
                    .CountAsync()
                    .ConfigureAwait(false);
                if (testCount <= testCountMax)
                {
                    return;
                }

                Logger.LogInformation($"{modelBuild.GetBuildKey()} has too many test results ({testCount})");

                var total = 0;

                do
                {
                    var testResultIds = await triageContext
                        .ModelTestResults
                        .AsNoTracking()
                        .Where(x => x.ModelBuildId == modelBuild.Id)
                        .Take(testCountMax)
                        .Select(x => x.Id)
                        .ToListAsync()
                        .ConfigureAwait(false);
                    if (testResultIds.Count == 0)
                    {
                        break;
                    }

                    triageContext.RemoveRange(testResultIds.Select(x => new ModelTestResult() { Id = x }));
                    total += testResultIds.Count;
                    Logger.LogInformation($"Deleted {total} test resuts out of {testCount} from {modelBuild.GetBuildKey()}");

                    await triageContext.SaveChangesAsync().ConfigureAwait(false);
                } while (true);
            }
        }
    }
}
