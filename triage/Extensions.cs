#nullable enable

using System;
using System.Text.RegularExpressions;
using DevOps.Util;
using Octokit;

internal static class Extensions
{
    internal static GitHubIssueKey GetIssueKey(this Octokit.Issue issue)
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
}