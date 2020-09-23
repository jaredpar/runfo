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
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Octokit;
using Org.BouncyCastle.Asn1.Crmf;

namespace DevOps.Util.DotNet.Triage
{
    /// <summary>
    /// This class is responsible for making the updates to GitHub based on the stored state 
    /// of the model
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
        public ReportBuilder ReportBuilder { get; } = new ReportBuilder();

        private ILogger Logger { get; }

        public TriageContext Context => TriageContextUtil.Context;

        public TrackingGitHubUtil(
            IGitHubClientFactory gitHubClientFactory,
            TriageContext context,
            ILogger logger)
        {
            GitHubClientFactory = gitHubClientFactory;
            TriageContextUtil = new TriageContextUtil(context);
            Logger = logger;
        }

        public async Task UpdateGithubIssuesAsync()
        {
            foreach (var modelTrackingIssue in Context.ModelTrackingIssues.Where(x => x.IsActive))
            {
                try
                {
                    await UpdateGitHubIssueAsync(modelTrackingIssue).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error updating {modelTrackingIssue.Id}: {ex.Message}");
                }
            }
        }

        public async Task UpdateGitHubIssueAsync(int modelTrackingIssueId)
        {
            var modelTrackingIssue = await Context
                .ModelTrackingIssues
                .Where(x => x.Id == modelTrackingIssueId)
                .SingleAsync().ConfigureAwait(false);
            await UpdateGitHubIssueAsync(modelTrackingIssue).ConfigureAwait(false);
        }

        public async Task UpdateGitHubIssueAsync(ModelTrackingIssue modelTrackingIssue)
        {
            if (modelTrackingIssue.GetGitHubIssueKey() is { } issueKey)
            {
                await UpdateGitHubIssueAsync(modelTrackingIssue, issueKey).ConfigureAwait(false);
            }
        }

        private async Task UpdateGitHubIssueAsync(ModelTrackingIssue modelTrackingIssue, GitHubIssueKey issueKey)
        {
            IGitHubClient gitHubClient;
            try
            {
                gitHubClient = await GitHubClientFactory.CreateForAppAsync(
                    issueKey.Organization,
                    issueKey.Repository).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Cannot create GitHubClient for {issueKey.Organization} {issueKey.Repository}: {ex.Message}");
                return;
            }

            var report = await GetReportAsync(modelTrackingIssue).ConfigureAwait(false);
            var succeeded = await UpdateGitHubIssueReport(gitHubClient, issueKey, report).ConfigureAwait(false);
            Logger.LogInformation($"Updated {issueKey.IssueUri}");
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

        public async Task<string> GetReportAsync(ModelTrackingIssue modelTrackingIssue, int limit = DefaultReportLimit, bool includeMarkers = true)
        {
            var reportTextTask = modelTrackingIssue.TrackingKind switch
            {
                TrackingKind.Test => GetReportForTestAsync(modelTrackingIssue, limit),
                TrackingKind.Timeline => GetReportForTimelineAsync(modelTrackingIssue, limit),
                TrackingKind.HelixConsole => GetReportForHelixAsync(modelTrackingIssue, HelixLogKind.Console, limit),
                TrackingKind.HelixRunClient => GetReportForHelixAsync(modelTrackingIssue, HelixLogKind.RunClient, limit),
                _ => throw new Exception($"Invalid value {modelTrackingIssue.TrackingKind}"),
            };

            var reportText = await reportTextTask.ConfigureAwait(false);
            return includeMarkers
                ? WrapInStartEndMarkers(reportText)
                : reportText;
        }

        private async Task<string> GetReportForTestAsync(ModelTrackingIssue modelTrackingIssue, int limit)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Take(limit)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    GitHubTargetBranch = x.ModelBuildAttempt.ModelBuild.GitHubTargetBranch,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    TestRunName = x.ModelTestResult.ModelTestRun.Name,
                    TestResult = x.ModelTestResult,
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchTests(
                matches.Select(x => (
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
        }

        private async Task<string> GetReportForTimelineAsync(ModelTrackingIssue modelTrackingIssue, int limit)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Take(limit)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    GitHubTargetBranch = x.ModelBuildAttempt.ModelBuild.GitHubTargetBranch,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    TimelineIssue = x.ModelTimelineIssue
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchTimeline(
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
        }

        private async Task<string> GetReportForHelixAsync(ModelTrackingIssue modelTrackingIssue, HelixLogKind helixLogKind, int limit)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .OrderByDescending(x => x.ModelBuildAttempt.ModelBuild.BuildNumber)
                .Take(limit)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    GitHubTargetBranch = x.ModelBuildAttempt.ModelBuild.GitHubTargetBranch,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    HelixLogUri = x.HelixLogUri,
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchHelix(
                matches.Select(x => (
                    new BuildInfo(
                        x.AzureOrganization,
                        x.AzureProject,
                        x.BuildNumber,
                        new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.GitHubPullRequestNumber, x.GitHubTargetBranch)),
                    (HelixLogInfo?)(new HelixLogInfo(helixLogKind, x.HelixLogUri)))),
                new[] { helixLogKind },
                markdown: true);
        }
    }
}
