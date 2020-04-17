using System;

internal readonly struct GitHubIssueKey
{
    internal string Organization { get; }

    internal string Repository { get; }

    internal int Id { get; }

    internal string IssueUri => $"https://github.com/{Organization}/{Repository}/issues/{Id}";

    internal GitHubIssueKey(string organization, string repository, int id)
    {
        Organization = organization;
        Repository = repository;
        Id = id;
    }

    public override string ToString() => $"{Organization}/{Repository}/{Id}";
}
