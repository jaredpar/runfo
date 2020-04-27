#nullable enable

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

namespace DevOps.Util.Triage
{
    public sealed class AutoTriageUtil
    {
        public DevOpsServer Server { get; }

        public DotNetQueryUtil QueryUtil { get; }

        public TriageContextUtil TriageContextUtil { get; }

        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public AutoTriageUtil(
            DevOpsServer server,
            TriageContext context,
            ILogger logger)
        {
            Server = server;
            QueryUtil = new DotNetQueryUtil(server);
            TriageContextUtil = new TriageContextUtil(context);
            Logger = logger;
        }

        // TODO: don't do this if the issue is closed
        // TODO: limit builds to report on to 100 because after that the tables get too large

        // TODO: eventually this won't be necessary
        public void EnsureTriageIssues()
        {
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "unable to load shared library 'advapi32.dll' or one of its dependencies",
                Create("dotnet", "core-eng", 9635));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "HTTP request to.*api.nuget.org.*timed out",
                Create("dotnet", "core-eng", 9634),
                Create("dotnet", "runtime", 35074));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "Failed to install dotnet",
                Create("dotnet", "runtime", 34015));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "Notification of assignment to an agent was never received",
                Create("dotnet", "runtime", 35223));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Infra,
                SearchKind.SearchTimeline,
                searchText: "Received request to deprovision: The request was cancelled by the remote provider",
                Create("dotnet", "runtime", 34472, includeDefinitons: false),
                Create("dotnet", "core-eng", 9532));

            static ModelTriageGitHubIssue Create(string organization, string repository, int number, string? buildQuery = null, bool includeDefinitons = true) =>
                new ModelTriageGitHubIssue()
                {
                    Organization = organization,
                    Repository = repository, 
                    IssueNumber = number,
                    BuildQuery = buildQuery,
                    IncludeDefinitions = includeDefinitons
                };
        }

        public async Task Triage(string projectName, int buildNumber)
        {
            var build = await Server.GetBuildAsync(projectName, buildNumber).ConfigureAwait(false);
            await Triage(build).ConfigureAwait(false);
        }

        public async Task Triage(string buildQuery)
        {
            foreach (var build in await QueryUtil.ListBuildsAsync(buildQuery))
            {
                await Triage(build).ConfigureAwait(false);
            }
        }

        // TODO: need overload that takes builds and groups up the issue and PR updates
        // or maybe just make that a separate operation from triage
        public async Task Triage(Build build)
        {
            Logger.LogInformation($"Triaging {DevOpsUtil.GetBuildUri(build)}");

            (Timeline? Timeline, bool Fetched) cachedTimeline = (null, false);
            var buildInfo = build.GetBuildInfo();
            var modelBuild = TriageContextUtil.EnsureBuild(buildInfo);

            var query = 
                from issue in Context.ModelTriageIssues
                from complete in Context.ModelTriageIssueResultCompletes
                    .Where(complete =>
                        complete.ModelTriageIssueId == issue.Id &&
                        complete.ModelBuildId == modelBuild.Id)
                    .DefaultIfEmpty()
                select new { issue, complete };

            foreach (var data in query.ToList())
            {
                if (data.complete is object)
                {
                    continue;
                }

                var issue = data.issue;
                switch (issue.SearchKind)
                {
                    case SearchKind.SearchTimeline:
                        { 
                            var timeline = await GetTimelineAsync();
                            if (timeline is object)
                            {
                                DoSearchTimeline(issue, build, modelBuild, timeline);
                            }
                            break;
                        }
                    default:
                        Logger.LogWarning($"Unknown search kind {issue.SearchKind} in {issue.Id}");
                        break;
                }
            }

            async Task<Timeline?> GetTimelineAsync()
            {
                if (cachedTimeline.Fetched)
                {
                    return cachedTimeline.Timeline;
                }

                try
                {
                    cachedTimeline.Timeline = await Server.GetTimelineAsync(build);
                    if (cachedTimeline.Timeline is null)
                    {
                        Logger.LogWarning("No timeline");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error getting timeline: {ex.Message}");
                }

                cachedTimeline.Fetched = true;
                return cachedTimeline.Timeline;
            }
        }

        private void DoSearchTimeline(
            ModelTriageIssue modelTriageIssue,
            Build build,
            ModelBuild modelBuild,
            Timeline timeline)
        {
            var searchText = modelTriageIssue.SearchText;
            Logger.LogInformation($@"Text: ""{searchText}""");
            if (TriageContextUtil.IsProcessed(modelTriageIssue, modelBuild))
            {
                Logger.LogInformation($@"Skipping");
                return;
            }

            var count = 0;
            foreach (var result in QueryUtil.SearchTimeline(build, timeline, text: searchText))
            {
                count++;

                var modelTriageIssueResult = new ModelTriageIssueResult()
                {
                    TimelineRecordName = result.ResultRecord.Name,
                    JobName = result.JobName,
                    Line = result.Line,
                    ModelBuild = modelBuild,
                    ModelTriageIssue = modelTriageIssue,
                    BuildNumber = result.Build.GetBuildKey().Number,
                };
                Context.ModelTriageIssueResults.Add(modelTriageIssueResult);
            }

            var complete = new ModelTriageIssueResultComplete()
            {
                ModelTriageIssue = modelTriageIssue,
                ModelBuild = modelBuild,
            };
            Context.ModelTriageIssueResultCompletes.Add(complete);

            try
            {
                Logger.LogInformation($@"Saving {count} jobs");
                Context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot save timeline complete: {ex.Message}");
            }
        }
    }
}
