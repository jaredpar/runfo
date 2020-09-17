using System;
using System.Text.RegularExpressions;
using DevOps.Util;
using DevOps.Util.DotNet;
using Octokit;

namespace DevOps.Util.Triage
{
    public static class Extensions
    {
        #region ModelBuild

        public static BuildKey GetBuildKey(this ModelBuild modelBuild) =>
            new BuildKey(
                modelBuild.ModelBuildDefinition.AzureOrganization,
                modelBuild.ModelBuildDefinition.AzureProject,
                modelBuild.BuildNumber);

        public static BuildInfo GetBuildInfo(this ModelBuild modelBuild) =>
            new BuildInfo(
                modelBuild.ModelBuildDefinition.AzureOrganization,
                modelBuild.ModelBuildDefinition.AzureProject,
                modelBuild.BuildNumber,
                GetGitHubBuildInfo(modelBuild));

        public static BuildAndDefinitionInfo GetBuildAndDefinitionInfo(this ModelBuild modelBuild) =>
            new BuildAndDefinitionInfo(
                modelBuild.ModelBuildDefinition.AzureOrganization,
                modelBuild.ModelBuildDefinition.AzureProject,
                modelBuild.BuildNumber,
                modelBuild.ModelBuildDefinition.DefinitionId,
                modelBuild.ModelBuildDefinition.DefinitionName,
                GetGitHubBuildInfo(modelBuild));

        public static BuildResultInfo GetBuildResultInfo(this ModelBuild modelBuild) =>
            new BuildResultInfo(
                GetBuildAndDefinitionInfo(modelBuild),
                modelBuild.StartTime,
                modelBuild.FinishTime,
                modelBuild.BuildResult ?? BuildResult.None);

        public static GitHubBuildInfo GetGitHubBuildInfo(this ModelBuild modelBuild) =>
            new GitHubBuildInfo(
                modelBuild.GitHubOrganization,
                modelBuild.GitHubRepository,
                modelBuild.PullRequestNumber);


        public static ModelBuildKind GetModelBuildKind(this ModelBuild modelBuild)
        {
            if (modelBuild.IsMergedPullRequest)
            {
                return ModelBuildKind.MergedPullRequest;
            }

            if (modelBuild.PullRequestNumber.HasValue)
            {
                return ModelBuildKind.PullRequest;
            }

            return ModelBuildKind.Rolling;
        }

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