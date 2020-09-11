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
    /// <summary>
    /// This class is responsible for making the updates to GitHub based on the stored state 
    /// of the model
    /// </summary>
    public sealed class TrackingGitHubUtil
    {
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
            // TODO: need to implemente
            throw null;
            /*
            foreach (var modelTrackingIssue in Context.ModelTrackingIssues.Where(x => x.IsActive))
            {
                if (modelTrackingIssue.GetGitHubIssueKey() is { } issueKey)
                {
                }
            }

            async Task UpdateIssuesForSearchTimeline(ModelTriageIssue triageIssue)
            {
                foreach (var gitHubIssue in triageIssue.ModelTriageGitHubIssues.ToList())
                {
                    await UpdateIssueForSearchTimeline(triageIssue, gitHubIssue).ConfigureAwait(false);
                }
            }

            async Task UpdateIssueForSearchTimeline(ModelTriageIssue triageIssue, ModelTriageGitHubIssue gitHubIssue)
            {
                GitHubClient gitHubClient;
                try
                {
                    gitHubClient = await GitHubClientFactory.CreateForAppAsync(
                        gitHubIssue.Organization,
                        gitHubIssue.Repository).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Cannot create GitHubClient for {gitHubIssue.Organization} {gitHubIssue.Repository}: {ex.Message}");
                    return;
                }

                var results = await TriageContextUtil.FindModelTriageIssueResultsAsync(triageIssue, gitHubIssue).ConfigureAwait(false);
                var footer = new StringBuilder();
                var mostRecent = results
                    .Select(x => x.ModelBuild)
                    .OrderByDescending(x => x.StartTime)
                    .FirstOrDefault();
                if (mostRecent is object && mostRecent.StartTime is DateTime startTime)
                {
                    Debug.Assert(mostRecent.StartTime.HasValue);
                    var buildKey = mostRecent.GetBuildKey();
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                    startTime = TimeZoneInfo.ConvertTimeFromUtc(startTime, zone);

                    footer.AppendLine($"Most [recent]({buildKey.BuildUri}) failure {startTime}");
                }

                const int limit = 100;
                if (results.Count > limit)
                {
                    footer.AppendLine($"Limited to {limit} items (removed {results.Count - limit})");
                    results = results.Take(limit).ToList();
                }

                // TODO: we use same Server here even if the Organization setting in the 
                // item specifies a different organization. Need to replace Server with 
                // a map from org -> DevOpsServer
                var searchResults = results
                    .Select(x => (x.ModelBuild.GetBuildInfo(), (string?)x.JobName));
                var reportBody = ReportBuilder.BuildSearchTimeline(
                    searchResults,
                    markdown: true,
                    includeDefinition: gitHubIssue.IncludeDefinitions,
                    footer.ToString());

                var succeeded = await UpdateGitHubIssueReport(gitHubClient, gitHubIssue.IssueKey, reportBody).ConfigureAwait(false);
                Logger.LogInformation($"Updated {gitHubIssue.IssueKey.IssueUri}");
            }
            */
        }

        public Task<string> GetReportAsync(ModelTrackingIssue modelTrackingIssue)
        {
            switch (modelTrackingIssue.TrackingKind)
            {
                case TrackingKind.Test:
                    return GetReportForTestAsync(modelTrackingIssue);
                case TrackingKind.Timeline:
                    return GetReportForTimelineAsync(modelTrackingIssue);
                case TrackingKind.HelixConsole:
                    return GetReportForHelixAsync(modelTrackingIssue, HelixLogKind.Console);
                case TrackingKind.HelixRunClient:
                    return GetReportForHelixAsync(modelTrackingIssue, HelixLogKind.RunClient);
                default:
                    return Task.FromException<string>(new Exception($"Invalid value {modelTrackingIssue.TrackingKind}"));
            }
        }

        private async Task<string> GetReportForTestAsync(ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Test);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    TestRunName = x.ModelTestResult.ModelTestRun.Name,
                    TestResult = x.ModelTestResult,
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchTests(
                matches.Select(x => (
                    new BuildInfo(
                        x.BuildNumber,
                        new DefinitionInfo(x.AzureOrganization, x.AzureProject, x.DefinitionId, x.DefinitionName),
                        new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.GitHubPullRequestNumber)),
                    (string?)x.TestRunName,
                    x.TestResult.GetHelixLogInfo())),
                includeDefinition: true,
                includeHelix: matches.Any(x => x.TestResult.IsHelixTestResult));
        }

        private async Task<string> GetReportForTimelineAsync(ModelTrackingIssue modelTrackingIssue)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    TimelineIssue = x.ModelTimelineIssue
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchTimeline(
                matches.Select(x => (
                    new BuildInfo(
                        x.BuildNumber,
                        new DefinitionInfo(x.AzureOrganization, x.AzureProject, x.DefinitionId, x.DefinitionName),
                        new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.GitHubPullRequestNumber)),
                    x.TimelineIssue?.JobName)),
                markdown: true,
                includeDefinition: true);
        }

        private async Task<string> GetReportForHelixAsync(ModelTrackingIssue modelTrackingIssue, HelixLogKind helixLogKind)
        {
            Debug.Assert(modelTrackingIssue.TrackingKind == TrackingKind.Timeline);
            var matches = await Context
                .ModelTrackingIssueMatches
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .Select(x => new
                {
                    AzureOrganization = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureOrganization,
                    AzureProject = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.AzureProject,
                    DefinitionId = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionId,
                    DefinitionName = x.ModelBuildAttempt.ModelBuild.ModelBuildDefinition.DefinitionName,
                    GitHubOrganization = x.ModelBuildAttempt.ModelBuild.GitHubOrganization,
                    GitHubRepository = x.ModelBuildAttempt.ModelBuild.GitHubRepository,
                    GitHubPullRequestNumber = x.ModelBuildAttempt.ModelBuild.PullRequestNumber,
                    BuildNumber = x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    HelixLogUri = x.HelixLogUri,
                })
                .ToListAsync().ConfigureAwait(false);

            var reportBuilder = new ReportBuilder();
            return reportBuilder.BuildSearchHelix(
                matches.Select(x => (
                    new BuildInfo(
                        x.BuildNumber,
                        new DefinitionInfo(x.AzureOrganization, x.AzureProject, x.DefinitionId, x.DefinitionName),
                        new GitHubBuildInfo(x.GitHubOrganization, x.GitHubRepository, x.GitHubPullRequestNumber)),
                    (HelixLogInfo?)(new HelixLogInfo(helixLogKind, x.HelixLogUri)))),
                new[] { helixLogKind },
                markdown: true);
        }

        private async Task<bool> UpdateGitHubIssueAsync(GitHubIssueKey issueKey, string reportBody)
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
                return false;
            }

            return await UpdateGitHubIssueAsync(gitHubClient, issueKey, reportBody).ConfigureAwait(false);
        }

        private async Task<bool> UpdateGitHubIssueAsync(IGitHubClient gitHubClient, GitHubIssueKey issueKey, string reportBody)
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
                    if (ReportBuilder.MarkdownReportEndRegex.IsMatch(line))
                    {
                        inReportBody = false;
                        foundEnd = true;
                    }
                }
                else if (ReportBuilder.MarkdownReportStartRegex.IsMatch(line))
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
    }
}
