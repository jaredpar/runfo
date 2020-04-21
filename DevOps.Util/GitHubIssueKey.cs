using System;

namespace DevOps.Util
{
    public readonly struct GitHubIssueKey
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

        public override string ToString() => $"{Organization}/{Repository}/{Number}";
    }

    public readonly struct GitHubPullRequestKey
    {
        public string Organization { get; }

        public string Repository { get; }

        public int Number { get; }

        public string PullRequestUri => $"https://github.com/{Organization}/{Repository}/pull/{Number}";

        public GitHubPullRequestKey(string organization, string repository, int number)
        {
            Organization = organization;
            Repository = repository;
            Number = number;
        }

        public GitHubIssueKey ToIssueKey() => new GitHubIssueKey(Organization, Repository, Number);

        public override string ToString() => $"{Organization}/{Repository}/{Number}";
    }
}
