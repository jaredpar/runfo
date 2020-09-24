using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;
using Octokit;

namespace DevOps.Util.DotNet.Triage
{
    public static class Extensions
    {
        #region ModelBuild

        public static BuildKey GetBuildKey(this ModelBuild modelBuild) =>
            new BuildKey(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.BuildNumber);

        public static BuildInfo GetBuildInfo(this ModelBuild modelBuild) =>
            new BuildInfo(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.BuildNumber,
                GetGitHubBuildInfo(modelBuild));

        public static BuildAndDefinitionInfo GetBuildAndDefinitionInfo(this ModelBuild modelBuild) =>
            new BuildAndDefinitionInfo(
                modelBuild.AzureOrganization,
                modelBuild.AzureProject,
                modelBuild.BuildNumber,
                modelBuild.ModelBuildDefinition.DefinitionId,
                modelBuild.ModelBuildDefinition.DefinitionName,
                GetGitHubBuildInfo(modelBuild));

        public static BuildResultInfo GetBuildResultInfo(this ModelBuild modelBuild) =>
            new BuildResultInfo(
                GetBuildAndDefinitionInfo(modelBuild),
                modelBuild.QueueTime,
                modelBuild.StartTime,
                modelBuild.FinishTime,
                modelBuild.BuildResult ?? BuildResult.None);

        public static GitHubBuildInfo GetGitHubBuildInfo(this ModelBuild modelBuild) =>
            new GitHubBuildInfo(
                modelBuild.GitHubOrganization,
                modelBuild.GitHubRepository,
                modelBuild.PullRequestNumber,
                modelBuild.GitHubTargetBranch);

        public static ModelBuildKind GetModelBuildKind(this ModelBuild modelBuild) =>
            TriageContextUtil.GetModelBuildKind(modelBuild.IsMergedPullRequest, modelBuild.PullRequestNumber);

        #endregion

        #region ModelBuildAttempt

        public static BuildAttemptKey GetBuildAttemptKey(this ModelBuildAttempt modelBuildAttempt) =>
            new BuildAttemptKey(
                modelBuildAttempt.ModelBuild.GetBuildKey(),
                modelBuildAttempt.Attempt);

        #endregion

        #region ModelBuildDefinition

        public static DefinitionKey GetDefinitionKey(this ModelBuildDefinition modelBuildDefinition) =>
            new DefinitionKey(
                modelBuildDefinition.AzureOrganization,
                modelBuildDefinition.AzureProject,
                modelBuildDefinition.DefinitionId);

        public static DefinitionInfo GetDefinitionInfo(this ModelBuildDefinition modelBuildDefinition) =>
            new DefinitionInfo(GetDefinitionKey(modelBuildDefinition), modelBuildDefinition.DefinitionName);

        #endregion

        #region ModelTestResult

        public static HelixLogInfo? GetHelixLogInfo(this ModelTestResult modelTestResult)
        {
            if (!modelTestResult.IsHelixTestResult)
            {
                return null;
            }

            return new HelixLogInfo(
                runClientUri: modelTestResult.HelixRunClientUri,
                consoleUri: modelTestResult.HelixConsoleUri,
                coreDumpUri: modelTestResult.HelixCoreDumpUri,
                testResultsUri: modelTestResult.HelixTestResultsUri);
        }

        #endregion

        #region ModelTrackingIssue

        public static GitHubIssueKey? GetGitHubIssueKey(this ModelTrackingIssue modelTrackingIssue)
        {
            if (modelTrackingIssue is
            {
                GitHubOrganization: { } organization,
                GitHubRepository: { } repository,
                GitHubIssueNumber: int number
            })
            {
                return new GitHubIssueKey(organization, repository, number);
            }

            return null;
        }

        #endregion

        #region TrackingIssueUtil

        public static async Task TriageBuildsAsync(this TrackingIssueUtil trackingIssueUtil, ModelTrackingIssue modelTrackingIssue, SearchBuildsRequest request, CancellationToken cancellationToken = default)
        {
            IQueryable<ModelBuildAttempt> buildAttemptQuery = trackingIssueUtil.Context.ModelBuildAttempts;
            if (modelTrackingIssue.ModelBuildDefinitionId is { } id)
            {
                buildAttemptQuery = buildAttemptQuery.Where(x => x.ModelBuild.ModelBuildDefinitionId == id);
                request.Definition = null;
            }

            buildAttemptQuery = request.Filter(buildAttemptQuery);

            var attempts = await buildAttemptQuery
                .Include(x => x.ModelBuild)
                .ThenInclude(x => x.ModelBuildDefinition)
                .ToListAsync();
            foreach (var attempt in attempts)
            {
                await trackingIssueUtil.TriageAsync(attempt, modelTrackingIssue);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        #endregion

        #region Misc

        public static string GetDisplayString(this ModelBuildKind kind) => kind switch
        {
            ModelBuildKind.All => "All",
            ModelBuildKind.MergedPullRequest => "Merged Pull Request",
            ModelBuildKind.PullRequest => "Pull Request",
            ModelBuildKind.Rolling => "Rolling",
            _ => throw new InvalidOperationException($"Unexpected value: {kind}")
        };

        public static GitHubIssueKey GetIssueKey(this Octokit.Issue issue)
        {
            var regex = new Regex(@"https://github.com/([\w\d-]+)/([\w\d-]+)/issues/\d+");
            var match = regex.Match(issue.HtmlUrl.ToString());
            if (!match.Success)
            {
                throw new Exception("Cannot parse GitHub issue URL");
            }

            return new GitHubIssueKey(
                match.Groups[1].Value,
                match.Groups[2].Value,
                issue.Number);
        }

        #endregion
    }
}