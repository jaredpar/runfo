using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Util.DotNet.Triage
{
    public sealed class BuildRetryUtil
    {
        public DevOpsServer Server { get; }
        public TriageContextUtil TriageContextUtil { get; }
        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public BuildRetryUtil(
            DevOpsServer server,
            TriageContext context,
            ILogger logger)
        {
            Server = server;
            TriageContextUtil = new TriageContextUtil(context);
            Logger = logger;
        }

        public async Task ProcessBuildAsync(BuildKey buildKey)
        {
            var modelBuildAttempts = await TriageContextUtil
                .GetModelBuildAttemptsQuery(buildKey)
                .Include(x => x.ModelBuild)
                .ToListAsync()
                .ConfigureAwait(false);
            var modelBuild = modelBuildAttempts.FirstOrDefault()?.ModelBuild;
            if (modelBuild is null)
            {
                // This happens when we have no data on the build at all
                Logger.LogWarning($"No model for the build {buildKey}");
                return;
            }

            var failed = modelBuild.BuildResult == ModelBuildResult.Failed || modelBuild.BuildResult == ModelBuildResult.Canceled;
            if (!failed)
            {
                Logger.LogWarning($"Build did not fail so no retry is needed");
                return;
            }

            await RetryOsxDeprovisionAsync(modelBuild, modelBuildAttempts);
        }

        private async Task RetryOsxDeprovisionAsync(ModelBuild modelBuild, List<ModelBuildAttempt> modelBuildAttempts)
        {
            Logger.LogInformation("Considering OSX deprovision retry");
            if (modelBuildAttempts.Count > 1)
            {
                Logger.LogInformation("Build already has multiple attempts");
                return;
            }

            var issues = await Context
                .ModelTimelineIssues
                .Where(x => x.ModelBuildId == modelBuild.Id)
                .Select(x => new
                {
                    x.Message,
                    x.IssueType,
                    x.JobName
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var count = 0;
            foreach (var issue in issues)
            {
                if (issue.Message.Contains("Received request to deprovision: The request was cancelled by the remote provider", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                Logger.LogInformation("No OSX failures");
                return;
            }

            var jobFailedCount = issues
                .Where(x => x.IssueType == ModelIssueType.Error)
                .GroupBy(x => x.JobName)
                .Count();
            if (jobFailedCount - count >= 4)
            {
                Logger.LogInformation("Too many non-OSX failures");
                return;
            }

            Logger.LogInformation("Retrying");
            await Server.RetryBuildAsync(modelBuild.AzureProject, modelBuild.BuildNumber).ConfigureAwait(false);

            var model = new ModelOsxDeprovisionRetry()
            {
                OsxJobFailedCount = count,
                JobFailedCount = jobFailedCount,
                ModelBuild = modelBuild,
            };

            Context.ModelOsxDeprovisionRetry.Add(model);
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
