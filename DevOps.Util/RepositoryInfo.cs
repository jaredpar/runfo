using System;
using System.Diagnostics.CodeAnalysis;

namespace DevOps.Util
{
    public readonly struct RepositoryInfo
    {
        public const string GitHubTypeName = "GitHub";

        public readonly string Id { get; }
        public readonly string Type { get; }

        public RepositoryInfo(string id, string type)
        {
            Id = id;
            Type = type;
        }

        public RepositoryInfo(GitHubPullRequestKey prKey)
        {
            Id = $"{prKey.Organization}/{prKey.Repository}";
            Type = GitHubTypeName;
        }

        public bool TryGetGitHubInfo([NotNullWhen(true)] out string? organization, [NotNullWhen(true)] out string? repository)
        {
            if (Type == GitHubTypeName && Id is object)
            {
                var both = Id.Split("/");
                if (both.Length == 2)
                {
                    organization = both[0];
                    repository = both[1];
                    return true;
                }
            }

            organization = null;
            repository = null;
            return false;
        }
    }
}