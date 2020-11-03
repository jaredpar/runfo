using DevOps.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    /// <summary>
    /// This class is responsible for making the updates to GitHub based on the stored state 
    /// of the model
    /// TODO: The name of this type is no longer accurate as it updates associated and tracking issues
    /// </summary>
    public sealed class TrackingGitHubUtil
    {
        public const int DefaultReportLimit = 100;
        public const string MarkdownReportStart = "<!-- runfo report start -->";
        public const string MarkdownReportEnd = "<!-- runfo report end -->";
        public static readonly Regex MarkdownReportStartRegex = new Regex(@"<!--\s*runfo report start\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex MarkdownReportEndRegex = new Regex(@"<!--\s*runfo report end\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public IGitHubClientFactory GitHubClientFactory { get; }
        public TriageContextUtil TriageContextUtil { get; }
        public SiteLinkUtil SiteLinkUtil { get; }
        public ReportBuilder ReportBuilder { get; } = new ReportBuilder();

        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public TrackingGitHubUtil(
            IGitHubClientFactory gitHubClientFactory,
            TriageContext context,
            SiteLinkUtil siteLinkUtil,
            ILogger logger)
        {
            GitHubClientFactory = gitHubClientFactory;
            TriageContextUtil = new TriageContextUtil(context);
            SiteLinkUtil = siteLinkUtil;
            Logger = logger;
        }

        public async Task UpdateTrackingGitHubIssuesAsync()
        {
            foreach (var modelTrackingIssue in Context.ModelTrackingIssues.Where(x => x.IsActive))
            {
                try
                {
                    await UpdateTrackingGitHubIssueAsync(modelTrackingIssue).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error updating {modelTrackingIssue.Id}: {ex.Message}");
                }
            }
        }

        public async Task UpdateTrackingGitHubIssueAsync(int modelTrackingIssueId)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == modelTrackingIssueId)
                .SingleAsync().ConfigureAwait(false);
            await UpdateTrackingGitHubIssueAsync(modelTrackingIssue).ConfigureAwait(false);
        }

        public async Task UpdateTrackingGitHubIssueAsync(ModelTrackingIssue modelTrackingIssue)
        {
            if (modelTrackingIssue.GetGitHubIssueKey() is { } issueKey)
            {
                await UpdateTrackingGitHubIssueAsync(modelTrackingIssue, issueKey).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Update the GitHub issue with the associated builds
        /// </summary>
        public async Task<bool> UpdateAssociatedGitHubIssueAsync(GitHubIssueKey issueKey)
        {
            IGitHubClient? gitHubClient = await TryCreateForIssue(issueKey).ConfigureAwait(false);
            if (gitHubClient is null)
            {
                return false;
            }

            await EnsureGitHubIssueHasMarkers(gitHubClient, issueKey).ConfigureAwait(false);
            var report = await GetAssociatedIssueReportAsync(issueKey).ConfigureAwait(false);
            var succeeded = await UpdateGitHubIssueReport(gitHubClient, issueKey, report);
            Logger.LogInformation($"Updated {issueKey.IssueUri}");
            return true;
        }

        private async Task<bool> UpdateTrackingGitHubIssueAsync(ModelTrackingIssue modelTrackingIssue, GitHubIssueKey issueKey)
        {
            IGitHubClient? gitHubClient = await TryCreateForIssue(issueKey).ConfigureAwait(false);
            if (gitHubClient is null)
            {
                return false;
            }

            var report = await GetTrackingIssueReport(modelTrackingIssue).ConfigureAwait(false);
            var succeeded = await UpdateGitHubIssueReport(gitHubClient, issueKey, report).ConfigureAwait(false);
            Logger.LogInformation($"Updated {issueKey.IssueUri}");
            return succeeded;
        }

        private async Task<bool> UpdateGitHubIssueReport(IGitHubClient gitHubClient, GitHubIssueKey issueKey, string reportBody)
        {
            try
            {
                var issueClient = gitHubClient.Issue;
                var issue = await issueClient.Get(issueKey.Organization, issueKey.Repository, issueKey.Number).ConfigureAwait(false);
                if (TryUpdateIssueText(reportBody, issue.Body, out var newIssueBody))
                {
                    var issueUpdate = issue.ToUpdate();
                    issueUpdate.Body = newIssueBody;
                    await issueClient.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, issueUpdate).ConfigureAwait(false);
                    return true;
                }
                else
                {
                    Logger.LogInformation("Cannot find the replacement section in the issue");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating issue {issueKey}: {ex.Message}");
            }

            return false;
        }

        public async Task<IGitHubClient?> TryCreateForIssue(GitHubIssueKey issueKey)
        {
            try
            {
                var gitHubClient = await GitHubClientFactory.CreateForAppAsync(
                    issueKey.Organization,
                    issueKey.Repository).ConfigureAwait(false);
                return gitHubClient;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot create GitHubClient for {issueKey.Organization} {issueKey.Repository}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ensure the issue has the appropriate start / end markers in the body so it can be updated later 
        /// by automation
        /// </summary>
        public async Task EnsureGitHubIssueHasMarkers(GitHubIssueKey issueKey)
        {
            IGitHubClient? gitHubClient = await TryCreateForIssue(issueKey).ConfigureAwait(false);
            if (gitHubClient is null)
            {
                return;
            }

            await EnsureGitHubIssueHasMarkers(gitHubClient, issueKey).ConfigureAwait(false);
        }

        /// <summary>
        /// Ensure the issue has the appropriate start / end markers in the body so it can be updated later 
        /// by automation
        /// </summary>
        public static async Task EnsureGitHubIssueHasMarkers(IGitHubClient gitHubClient, GitHubIssueKey issueKey)
        {
            var issue = await gitHubClient.Issue.Get(issueKey.Organization, issueKey.Repository, issueKey.Number).ConfigureAwait(false);
            if (HasMarkers())
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine(issue.Body);
            builder.AppendLine(MarkdownReportStart);
            builder.AppendLine(MarkdownReportEnd);

            var issueUpdate = issue.ToUpdate();
            issueUpdate.Body = builder.ToString();

            await gitHubClient.Issue.Update(issueKey.Organization, issueKey.Repository, issueKey.Number, issueUpdate).ConfigureAwait(false);

            bool HasMarkers()
            {
                using var reader = new StringReader(issue.Body);
                bool foundStart = false;
                while (reader.ReadLine() is string line)
                {
                    if (MarkdownReportStartRegex.IsMatch(line))
                    {
                        foundStart = true;
                    }

                    if (MarkdownReportEndRegex.IsMatch(line) && foundStart)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static bool TryUpdateIssueText(string reportBody, string oldIssueText, [NotNullWhen(true)] out string? newIssueText)
        {
            var builder = new StringBuilder();
            var inReportBody = false;
            var foundEnd = false;
            using var reader = new StringReader(oldIssueText);
            do
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (inReportBody)
                {
                    // Skip until we hit the end of the existing report
                    if (MarkdownReportEndRegex.IsMatch(line))
                    {
                        inReportBody = false;
                        foundEnd = true;
                    }
                }
                else if (MarkdownReportStartRegex.IsMatch(line))
                {
                    builder.AppendLine(reportBody);
                    inReportBody = true;
                }
                else
                {
                    builder.AppendLine(line);
                }
            } while (true);

            if (foundEnd)
            {
                newIssueText = builder.ToString();
                return true;
            }
            else
            {
                newIssueText = null;
                return false;
            }
        }

        public static string WrapInStartEndMarkers(string text)
        {
            var builder = new StringBuilder();
            builder.AppendLine(MarkdownReportStart);
            builder.AppendLine(text);
            builder.AppendLine(MarkdownReportEnd);
            return builder.ToString();
        }

        public async Task<string> GetTrackingIssueReport(
            ModelTrackingIssue modelTrackingIssue,
            int limit = DefaultReportLimit,
            bool includeMarkers = true,
            DateTime? baseTime = null)

        {
            var time = baseTime ?? DateTime.UtcNow;
            var reportTextTask = modelTrackingIssue.TrackingKind switch
            {
                TrackingKind.Test => GetReportForTestAsync(modelTrackingIssue, limit, time),
                TrackingKind.Timeline => GetReportForTimelineAsync(modelTrackingIssue, limit, time),
                TrackingKind.HelixLogs => GetReportForHelixAsync(modelTrackingIssue, limit),

#pragma warning disable 618
                // TODO: delete once these types are removed from the DB
                TrackingKind.HelixConsole => throw null!,
                TrackingKind.HelixRunClient => throw null!,
#pragma warning restore 618
                _ => throw new Exception($"Invalid value {modelTrackingIssue.TrackingKind}"),
            };

            var reportText = await reportTextTask.ConfigureAwait(false);
            return includeMarkers
                ? WrapInStartEndMarkers(reportText)
                : reportText;
        }

        private async Task<string> GetReportForTestAsync(ModelTrackingIssue modelTrackingIssue, int limit, DateTime baseTime)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    GitHubTargetBranch = x.ModelBuildAttempt.ModelBuild.GitHubTargetBranch,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    QueueTime = x.ModelBuildAttempt.ModelBuild.QueueTime,
                    TestRunName = x.ModelTestResult.ModelTestRun.Name,
                    TestResult = x.ModelTestResult,
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            var reportBody = reportBuilder.BuildSearchTests(matches
                .OrderByDescending(x => x.BuildNumber)
                .Take(limit)
                .Select(x => (
                    new BuildAndDefinitionInfo(
                        x.AzureOrganization,
                        x.AzureProject,
                        x.BuildNumber,
                        x.DefinitionId,
                        x.DefinitionName,
                        new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.GitHubPullRequestNumber, x.GitHubTargetBranch)),
                    (string?)x.TestRunName,
                    x.TestResult.GetHelixLogInfo())),
                includeDefinition: true,
                includeHelix: matches.Any(x => x.TestResult.IsHelixTestResult));

            var builder = new StringBuilder();
            AppendHeader(builder, modelTrackingIssue);
            builder.AppendLine(reportBody);
            if (matches.Count > limit)
            {
                builder.AppendLine($"Displaying {limit} of {matches.Count} results");
            }
            AppendFooter(builder, matches.Select(x => (x.BuildNumber, x.QueueTime)), baseTime);
            return builder.ToString();
        }

        private async Task<string> GetReportForTimelineAsync(ModelTrackingIssue modelTrackingIssue, int limit, DateTime baseTime)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Take(limit)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    GitHubTargetBranch = x.ModelBuildAttempt.ModelBuild.GitHubTargetBranch,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    QueueTime = x.ModelBuildAttempt.ModelBuild.QueueTime,
                    TimelineIssue = x.ModelTimelineIssue
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            var reportBody = reportBuilder.BuildSearchTimeline(
                matches.Select(x => (
                    new BuildAndDefinitionInfo(
                        x.AzureOrganization,
                        x.AzureProject,
                        x.BuildNumber,
                        x.DefinitionId,
                        x.DefinitionName,
                        new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.GitHubPullRequestNumber, x.GitHubTargetBranch)),
                    x.TimelineIssue?.JobName)),
                markdown: true,
                includeDefinition: true);

            var builder = new StringBuilder();
            AppendHeader(builder, modelTrackingIssue);
            builder.AppendLine(reportBody);
            AppendFooter(builder, matches.Select(x => (x.BuildNumber, x.QueueTime)), baseTime);

            return builder.ToString();
        }

        private async Task<string> GetReportForHelixAsync(ModelTrackingIssue modelTrackingIssue, int limit)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.HelixLogs);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Take(limit)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    GitHubTargetBranch = x.ModelBuildAttempt.ModelBuild.GitHubTargetBranch,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    HelixLogKind = x.HelixLogKind,
                    HelixLogUri = x.HelixLogUri,
                })
                .ToListAsync().ConfigureAwait(false);

            var map = new Dictionary<BuildKey, (BuildInfo BuildInfo, HelixLogInfo? HelixLogInfo)>();
            var set = new HashSet<HelixLogKind>();
            foreach (var item in matches)
            {
                var key = new BuildKey(item.AzureOrganization, item.AzureProject, item.BuildNumber);
                if (!map.TryGetValue(key, out var tuple))
                {
                    var buildInfo = new BuildInfo(
                        key.Organization,
                        key.Project,
                        key.Number,
                        new GitHubBuildInfo(item.GitHubOrganization, item.GitHubRepository, item.GitHubPullRequestNumber, item.GitHubTargetBranch));
                    tuple = (buildInfo, null);
                }

                set.Add(item.HelixLogKind);
                tuple.HelixLogInfo = tuple.HelixLogInfo is { } log
                    ? log.SetUri(item.HelixLogKind, item.HelixLogUri)
                    : new HelixLogInfo(item.HelixLogKind, item.HelixLogUri);
                map[key] = tuple;
            }

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchHelix(
                map.Values.OrderByDescending(x => x.BuildInfo.Number),
                set.ToArray(),
                markdown: true);
        }

        /// <summary>
        /// Get the report for builds that are associated with the given GitHub Issue Key
        /// </summary>
        public async Task<string> GetAssociatedIssueReportAsync(GitHubIssueKey issueKey)
        {
            var query = TriageContextUtil
                .GetModelGitHubIssuesQuery(issueKey)
                .Select(x => new
                {
                    x.ModelBuild.AzureOrganization,
                    x.ModelBuild.AzureProject,
                    x.ModelBuild.BuildNumber,
                    x.ModelBuild.QueueTime,
                    x.ModelBuild.PullRequestNumber,
                    x.ModelBuild.GitHubOrganization,
                    x.ModelBuild.GitHubRepository,
                    x.ModelBuild.GitHubTargetBranch,
                });
            var builds = await query.ToListAsync().ConfigureAwait(false);
            var results = builds
                .Select(x => (new BuildInfo(
                    x.AzureOrganization,
                    x.AzureProject,
                    x.BuildNumber,
                    new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.PullRequestNumber, x.GitHubTargetBranch)),
                    x.QueueTime));
            var report = ReportBuilder.BuildManual(results);
            return WrapInStartEndMarkers(report);
        }

        private void AppendHeader(StringBuilder builder, ModelTrackingIssue modelTrackingIssue)
        {
            builder.AppendLine($"Runfo Tracking Issue: [{modelTrackingIssue.IssueTitle}]({SiteLinkUtil.GetTrackingIssueUri(modelTrackingIssue.Id)})");
        }

        private static void AppendFooter(StringBuilder builder, IEnumerable<(int BuildNumber, DateTime? QueueTime)> builds, DateTime baseTime)
        {
            builder.AppendLine();
            builder.AppendLine("Build Result Summary");
            builder.AppendLine("|Day Hit Count|Week Hit Count|Month Hit Count|");
            builder.AppendLine("|---|---|---|");

            var list = builds
                .GroupBy(x => x.BuildNumber)
                .Select(x => (BuildNumber: x.Key, QueueTime: x.SelectNullableValue(x => x.QueueTime).FirstOrDefault()))
                .Where(x => x.QueueTime != default)
                .ToList();
            var dayCount = list.Count(x => x.QueueTime > baseTime - TimeSpan.FromDays(1));
            var weekCount = list.Count(x => x.QueueTime > baseTime - TimeSpan.FromDays(7));
            var monthCount = list.Count(x => x.QueueTime > baseTime - TimeSpan.FromDays(30));
            builder.AppendLine($"|{dayCount}|{weekCount}|{monthCount}|");
        }
    }
}
