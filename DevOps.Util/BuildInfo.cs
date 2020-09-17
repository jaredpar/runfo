using System;

namespace DevOps.Util
{
    public readonly struct GitHubBuildInfo
    {
        public string Organization { get; }
        public string Repository { get; }
        public int? PullRequestNumber { get; }
        public string? TargetBranch { get; }

        public GitHubPullRequestKey? PullRequestKey => PullRequestNumber is int number
            ? new GitHubPullRequestKey(Organization, Repository, number)
            : (GitHubPullRequestKey?)null;

        public GitHubBuildInfo(
            string organization,
            string repository,
            int? pullRequestNumber,
            string? targetBranch)
        {
            Organization = organization;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
            TargetBranch = targetBranch;
        }

        public override string ToString() => $"{Organization} {Repository}";
    }

    public sealed class BuildInfo
    {
        public BuildKey BuildKey { get; }
        public GitHubBuildInfo? GitHubBuildInfo { get; }

        public string Organization => BuildKey.Organization;
        public string Project => BuildKey.Project;
        public int Number => BuildKey.Number;
        public string BuildUri => BuildKey.BuildUri;
        public GitHubPullRequestKey? PullRequestKey => GitHubBuildInfo?.PullRequestKey;

        public BuildInfo(
            string organization,
            string project,
            int buildNumber,
            GitHubBuildInfo? gitHubBuildInfo)
        {
            BuildKey = new BuildKey(organization, project, buildNumber);
            GitHubBuildInfo = gitHubBuildInfo;
        }

        public override string ToString() => $"{Organization} {Project} {Number}";
    }

    public sealed class DefinitionInfo
    {
        public DefinitionKey DefinitionKey { get; }
        public string Name { get; }

        public string Organization => DefinitionKey.Organization;
        public string Project => DefinitionKey.Project;
        public int Id => DefinitionKey.Id;
        public string DefinitionUri => DefinitionKey.DefinitionUri;

        public DefinitionInfo(DefinitionKey key, string name)
        {
            DefinitionKey = key;
            Name = name;
        }

        public DefinitionInfo(string organization, string project, int id, string name) 
            : this(new DefinitionKey(organization, project, id), name)
        {
        }

        public override string ToString() => $"{Project} {Name} {Id}";
    }

    public sealed class BuildAndDefinitionInfo
    {
        public BuildInfo BuildInfo { get; }
        public DefinitionInfo DefinitionInfo { get; }

        public string Organization => DefinitionInfo.Organization;
        public string Project => DefinitionInfo.Project;
        public int BuildNumber => BuildInfo.Number;
        public GitHubBuildInfo? GitHubBuildInfo => BuildInfo.GitHubBuildInfo;
        public string DefinitionName => DefinitionInfo.Name;
        public int DefinitionId => DefinitionInfo.Id;
        public string BuildUri => BuildKey.BuildUri;
        public BuildKey BuildKey => BuildInfo.BuildKey;
        public DefinitionKey DefinitionKey => DefinitionInfo.DefinitionKey;
        public GitHubPullRequestKey? PullRequestKey => GitHubBuildInfo?.PullRequestKey;

        public BuildAndDefinitionInfo(
            string organization,
            string project,
            int buildNumber,
            int definitionId,
            string definitionName,
            GitHubBuildInfo? gitHubBuildInfo)
        {
            BuildInfo = new BuildInfo(organization, project, buildNumber, gitHubBuildInfo);
            DefinitionInfo = new DefinitionInfo(organization, project, definitionId, definitionName);
        }

        public override string ToString() => $"{DefinitionName} {BuildNumber}";
    }

    public sealed class BuildResultInfo
    {
        public BuildAndDefinitionInfo BuildAndDefinitionInfo { get; }
        public DateTime? StartTime { get; }
        public DateTime? FinishTime { get; }
        public DateTime? QueueTime { get; }
        public BuildResult BuildResult { get; }

        public BuildInfo BuildInfo => BuildAndDefinitionInfo.BuildInfo;
        public DefinitionInfo DefinitionInfo => BuildAndDefinitionInfo.DefinitionInfo;
        public GitHubBuildInfo? GitHubBuildInfo => BuildAndDefinitionInfo.GitHubBuildInfo;
        public GitHubPullRequestKey? PullRequestKey => BuildInfo.PullRequestKey;
        public string Organization => BuildInfo.Organization;
        public string Project => BuildInfo.Project;
        public int Number => BuildInfo.Number;
        public string DefinitionName => DefinitionInfo.Name;
        public BuildKey BuildKey => BuildInfo.BuildKey;
        public string BuildUri => BuildInfo.BuildUri;

        public BuildResultInfo(
            BuildAndDefinitionInfo buildAndDefinitionInfo,
            DateTime? queueTime,
            DateTime? startTime,
            DateTime? finishTime,
            BuildResult buildResult)
        {
            BuildAndDefinitionInfo = buildAndDefinitionInfo;
            QueueTime = queueTime;
            StartTime = startTime;
            FinishTime = finishTime;
            BuildResult = buildResult;
        }

        public override string ToString() => $"{Organization} {Project} {Number}";
    }
}