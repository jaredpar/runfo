using System;

namespace DevOps.Util.DotNet
{
    public readonly struct GitHubIssueKey
    {
        public string Organization { get; }

        public string Repository { get; }

        public int Id { get; }

        public string IssueUri => $"https://github.com/{Organization}/{Repository}/issues/{Id}";

        public GitHubIssueKey(string organization, string repository, int id)
        {
            Organization = organization;
            Repository = repository;
            Id = id;
        }

        public override string ToString() => $"{Organization}/{Repository}/{Id}";
    }
}
