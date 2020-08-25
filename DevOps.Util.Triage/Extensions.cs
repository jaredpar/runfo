using System;
using System.Text.RegularExpressions;
using DevOps.Util;
using Octokit;

namespace DevOps.Util.Triage
{
    public static class Extensions
    {
        #region ModelBuild

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