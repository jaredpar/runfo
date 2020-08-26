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
                GetBuildKey(modelBuild),
                GetBuildDefinitionInfo(modelBuild.ModelBuildDefinition),
                modelBuild.GitHubOrganization,
                modelBuild.GitHubRepository,
                modelBuild.PullRequestNumber,
                modelBuild.StartTime,
                modelBuild.FinishTime,
                modelBuild.BuildResult ?? BuildResult.None);

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

        public static BuildDefinitionKey GetBuildDefinitionKey(this ModelBuildDefinition modelBuildDefinition) =>
            new BuildDefinitionKey(
                modelBuildDefinition.AzureOrganization,
                modelBuildDefinition.AzureProject,
                modelBuildDefinition.DefinitionId);

        public static BuildDefinitionInfo GetBuildDefinitionInfo(this ModelBuildDefinition modelBuildDefinition) =>
            new BuildDefinitionInfo(GetBuildDefinitionKey(modelBuildDefinition), modelBuildDefinition.DefinitionName);

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