#nullable enable

using System;

namespace DevOps.Util
{
    public readonly struct GitHubIssueKey : IEquatable<GitHubIssueKey>
    {
        public string Organization { get; }

        public string Repository { get; }

        public int Number { get; }

        public string IssueUri => $"https://github.com/{Organization}/{Repository}/issues/{Number}";

        public GitHubIssueKey(string organization, string repository, int number)
        {
            Organization = organization;
            Repository = repository;
            Number = number;
        }

        public static bool operator==(GitHubIssueKey left, GitHubIssueKey right) => left.Equals(right);

        public static bool operator!=(GitHubIssueKey left, GitHubIssueKey right) => !left.Equals(right);

        public bool Equals(GitHubIssueKey other) =>
            other.Organization == Organization &&
            other.Repository == Repository &&
            other.Number == Number;

        public override bool Equals(object? obj) => obj is GitHubIssueKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Organization, Repository, Number);

        public override string ToString() => $"{Organization}/{Repository}/{Number}";
    }

    public readonly struct GitHubPullRequestKey : IEquatable<GitHubPullRequestKey>
    {
        public string Organization { get; }

        public string Repository { get; }

        public int Number { get; }

        public GitHubInfo GitHubInfo => new GitHubInfo(Organization, Repository);

        public string PullRequestUri => GetPullRequestUri(Organization, Repository, Number);

        public GitHubPullRequestKey(string organization, string repository, int number)
        {
            Organization = organization;
            Repository = repository;
            Number = number;
        }

        public GitHubIssueKey ToIssueKey() => new GitHubIssueKey(Organization, Repository, Number);

        public static string GetPullRequestUri(string organization, string repository, int number) => 
            $"https://github.com/{organization}/{repository}/pull/{number}";

        public static bool operator==(GitHubPullRequestKey left, GitHubPullRequestKey right) => left.Equals(right);

        public static bool operator!=(GitHubPullRequestKey left, GitHubPullRequestKey right) => !left.Equals(right);

        public bool Equals(GitHubPullRequestKey other) =>
            other.Organization == Organization &&
            other.Repository == Repository &&
            other.Number == Number;

        public override bool Equals(object? obj) => obj is GitHubPullRequestKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Organization, Repository, Number);

        public override string ToString() => $"{Organization}/{Repository}/{Number}";
    }
}
