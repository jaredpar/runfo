#nullable enable

using System;

namespace DevOps.Util
{
    public readonly struct GitHubInfo
    {
        public const string TypeName = "GitHub";

        public string Organization { get; }

        public string Repository { get; }

        public RepositoryInfo RepositoryInfo => new RepositoryInfo(id: $"{Organization}/{Repository}", TypeName);

        public GitHubInfo(
            string organization,
            string repository)
        {
            Organization = organization;
            Repository = repository;
        }

        public static GitHubInfo? TryCreate(RepositoryInfo repositoryInfo)
        {
            if (repositoryInfo.Type == TypeName &&
                repositoryInfo.Id is object)
            {
                var both = repositoryInfo.Id.Split("/");
                if (both.Length == 2)
                {
                    return new GitHubInfo(both[0], both[1]);
                }
            }

            return null;
        }

        public override string ToString() => $"{Organization} {Repository}";
    }

    public class BuildInfo
    {
        public BuildKey Key { get; }

        public BuildDefinitionInfo DefinitionInfo { get; }

        public GitHubInfo? GitHubInfo { get; }

        public int? PullRequestNumber { get;}

        public DateTime? StartTime { get; }

        public DateTime? FinishTime { get; }

        public BuildResult BuildResult { get; }

        public GitHubPullRequestKey? PullRequestKey => PullRequestNumber.HasValue && GitHubInfo.HasValue
            ? (GitHubPullRequestKey?)new GitHubPullRequestKey(GitHubInfo.Value.Organization, GitHubInfo.Value.Repository, PullRequestNumber.Value)
            : null;

        public string Organization => Key.Organization;

        public string Project => Key.Project;

        public int Number => Key.Number;

        public string DefinitionName => DefinitionInfo.Name;

        public string BuildUri => Key.BuildUri;

        public BuildInfo(
            BuildKey buildKey,
            BuildDefinitionInfo buildDefinitionInfo,
            GitHubPullRequestKey pullRequestKey,
            DateTime? startTime,
            DateTime? finishTime,
            BuildResult buildResult)
        {
            Key = buildKey;
            DefinitionInfo = buildDefinitionInfo;
            GitHubInfo = new GitHubInfo(pullRequestKey.Organization, pullRequestKey.Repository);
            PullRequestNumber = pullRequestKey.Number;
            StartTime = startTime;
            FinishTime = finishTime;
            BuildResult = buildResult;
        }

        public BuildInfo(
            BuildKey buildKey,
            BuildDefinitionInfo buildDefinitionInfo,
            GitHubInfo? gitHubInfo,
            DateTime? startTime,
            DateTime? finishTime,
            BuildResult buildResult)
        {
            Key = buildKey;
            DefinitionInfo = buildDefinitionInfo;
            GitHubInfo = gitHubInfo;
            PullRequestNumber = null;
            StartTime = startTime;
            FinishTime = finishTime;
            BuildResult = buildResult;
        }

        public BuildInfo(
            BuildKey buildKey,
            BuildDefinitionInfo buildDefinitionInfo,
            string? gitHubOrganization,
            string? gitHubRepository,
            int? pullRequestNumber,
            DateTime? startTime,
            DateTime? finishTime,
            BuildResult buildResult)
        {
            Key = buildKey;
            DefinitionInfo = buildDefinitionInfo;
            if (gitHubOrganization is object && gitHubRepository is object)
            {
                GitHubInfo = new GitHubInfo(gitHubOrganization, gitHubRepository);
                PullRequestNumber = pullRequestNumber;
            }

            StartTime = startTime;
            FinishTime = finishTime;
        }

        public override string ToString() => $"{Organization} {Project} {Number}";
    }

    public sealed class BuildDefinitionInfo
    {
        public string Organization { get; }

        public string Project { get; }

        public string Name { get; }

        public int Id { get; }

        public string DefinitionUri => DevOpsUtil.GetBuildDefinitionUri(Organization, Project, Id);

        public BuildDefinitionInfo(string organization, string project, string name, int id)
        {
            Organization = organization;
            Project = project;
            Name = name;
            Id = id;
        }

        public override string ToString() => $"{Project} {Name} {Id}";
    }
}