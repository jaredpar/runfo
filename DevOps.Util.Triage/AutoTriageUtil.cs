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

        // TODO: need to remove this. Not all functions require this and it's hard to feed through from
        // functions
        public IGitHubClient GitHubClient { get; }

        public TriageContextUtil TriageContextUtil { get; }

        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public AutoTriageUtil(
            DevOpsServer server,
            TriageContext context,
            IGitHubClient gitHubClient,
            ILogger logger)
        {
            Server = server;
            GitHubClient = gitHubClient;
            QueryUtil = new DotNetQueryUtil(server, new AzureUtil(server), gitHubClient);
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
                Create("dotnet", "core-eng", 9634, "-p public"),
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
                Create("dotnet", "runtime", 34472, includeDefinitions: false),
                Create("dotnet", "core-eng", 9532));
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Test,
                SearchKind.SearchHelixRunClient,
                searchText: "ERROR.*Job running for too long. Killing...");
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Test,
                SearchKind.SearchTest,
                searchText: "System.Net.Sockets.Tests.DisposedSocket.NonDisposedSocket_SafeHandlesCollected");
            TriageContextUtil.EnsureTriageIssue(
                TriageIssueKind.Test,
                SearchKind.SearchTimeline,
                searchText: "OutOfMemoryException",
                Create("dotnet", "aspnetcore", 21802, "-d aspnet"));

            static ModelTriageGitHubIssue Create(string organization, string repository, int number, string? buildQuery = null, bool includeDefinitions = true) =>
                new ModelTriageGitHubIssue()
                {
                    Organization = organization,
                    Repository = repository, 
                    IssueNumber = number,
                    BuildQuery = buildQuery,
                    IncludeDefinitions = includeDefinitions
                };
        }

        public async Task TriageBuildAsync(string projectName, int buildNumber)
        {
            var build = await Server.GetBuildAsync(projectName, buildNumber).ConfigureAwait(false);
            await TriageBuildAsync(build).ConfigureAwait(false);
        }

        public async Task TriageQueryAsync(string buildQuery)
        {
            foreach (var build in await QueryUtil.ListBuildsAsync(buildQuery))
            {
                await TriageBuildAsync(build).ConfigureAwait(false);
            }
        }

        // TODO: need overload that takes builds and groups up the issue and PR updates
        // or maybe just make that a separate operation from triage
        public async Task TriageBuildAsync(Build build)
        {
            var buildInfo = build.GetBuildInfo();
            var modelBuild = await TriageContextUtil.EnsureBuildAsync(buildInfo).ConfigureAwait(false);
            var buildTriageUtil = new BuildTriageUtil(
                build,
                buildInfo,
                modelBuild,
                Server,
                TriageContextUtil,
                GitHubClient,
                Logger);
            await buildTriageUtil.TriageAsync().ConfigureAwait(false);
        }

        public async Task RetryOsxDeprovisionAsync(string projectName, int buildNumber)
        {
            var build = await Server.GetBuildAsync(projectName, buildNumber).ConfigureAwait(false);
            if (!(build.Result == BuildResult.Failed || build.Result == BuildResult.Canceled))
            {
                Logger.LogInformation("Not a failed build");
                return;
            }

            var timeline = await Server.GetTimelineAsync(build).ConfigureAwait(false);
            if (timeline is null)
            {
                Logger.LogInformation("Timeline is null");
                return;
            }

            if (timeline.Records.Any(x => x.PreviousAttempts?.Length > 0))
            {
                Logger.LogInformation("Project already has multiple attempts");
                return;
            }

            var osxCount = QueryUtil.SearchTimeline(
                build,
                timeline,
                text: "Received request to deprovision: The request was cancelled by the remote provider")
                .Select(x => x.Record.JobRecord)
                .Count();
            if (osxCount == 0)
            {
                Logger.LogInformation("No OSX failures");
                return;
            }

            var timelineTree = TimelineTree.Create(timeline);
            var totalFailed = timelineTree.Jobs.Where(x => !x.IsAnySuccess()).Count();
            if (totalFailed - osxCount >= 4)
            {
                Logger.LogInformation("Too many non-OSX failures");
                return;
            }

            Logger.LogInformation("Retrying");
            await Server.RetryBuildAsync(projectName, buildNumber).ConfigureAwait(false);

            var modelBuild = await TriageContextUtil.EnsureBuildAsync(build.GetBuildInfo()).ConfigureAwait(false);
            var model = new ModelOsxDeprovisionRetry()
            {
                OsxJobFailedCount = osxCount,
                JobFailedCount = totalFailed,
                ModelBuild = modelBuild,
            };

            Context.ModelOsxDeprovisionRetry.Add(model);
            await Context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
