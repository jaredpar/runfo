using System;

namespace DevOps.Util
{
    public readonly struct GitHubBuildInfo
    {
        public string Organization { get; }
        public string Repository { get; }
        public int? PullRequestNumber { get; }

        public GitHubPullRequestKey? PullRequestKey => PullRequestNumber is int number
            ? new GitHubPullRequestKey(Organization, Repository, number)
            : (GitHubPullRequestKey?)null;

        public GitHubBuildInfo(
            string organization,
            string repository,
            int? pullRequestNumber)
        {
            Organization = organization;
            Repository = repository;
            PullRequestNumber = pullRequestNumber;
        }

        public override string ToString() => $"{Organization} {Repository}";
    }

    public sealed class BuildInfo
    {
        public int Number { get; set; }
        public DefinitionInfo DefinitionInfo { get; }
        public GitHubBuildInfo? GitHubBuildInfo { get; }

        public string Organization => DefinitionInfo.Organization;
        public string Project => DefinitionInfo.Project;
        public string DefinitionName => DefinitionInfo.Name;
        public int DefinitionId => DefinitionInfo.Id;
        public string BuildUri => BuildKey.BuildUri;
        public BuildKey BuildKey => new BuildKey(Organization, Project, Number);
        public DefinitionKey DefinitionKey => DefinitionInfo.Key;
        public GitHubPullRequestKey? PullRequestKey => GitHubBuildInfo?.PullRequestKey;

        public BuildInfo(
            string organization,
            string project,
            int buildNumber,
            int definitionId,
            string definitionName,
            GitHubBuildInfo? gitHubBuildInfo)
        {
            Number = buildNumber;
            DefinitionInfo = new DefinitionInfo(organization, project, definitionId, definitionName);
            GitHubBuildInfo = gitHubBuildInfo;
        }

        public BuildInfo(
            int buildNumber,
            DefinitionInfo buildDefinitionInfo,
            GitHubBuildInfo? gitHubBuildInfo)
        {
            Number = buildNumber;
            DefinitionInfo = buildDefinitionInfo;
            GitHubBuildInfo = gitHubBuildInfo;
        }

        public override string ToString() => $"{DefinitionName} {Number}";
    }

    public sealed class BuildResultInfo
    {
        public BuildInfo BuildInfo { get; }
        public DateTime? StartTime { get; }
        public DateTime? FinishTime { get; }
        public BuildResult BuildResult { get; }

        public DefinitionInfo DefinitionInfo => BuildInfo.DefinitionInfo;
        public GitHubBuildInfo? GitHubBuildInfo => BuildInfo.GitHubBuildInfo;
        public GitHubPullRequestKey? PullRequestKey => BuildInfo.PullRequestKey;
        public string Organization => BuildInfo.Organization;
        public string Project => BuildInfo.Project;
        public int Number => BuildInfo.Number;
        public string DefinitionName => BuildInfo.DefinitionName;
        public BuildKey BuildKey => BuildInfo.BuildKey;
        public string BuildUri => BuildInfo.BuildUri;

        public BuildResultInfo(
            BuildInfo buildInfo,
            DateTime? startTime,
            DateTime? finishTime,
            BuildResult buildResult)
        {
            BuildInfo = buildInfo;
            StartTime = startTime;
            FinishTime = finishTime;
            BuildResult = buildResult;
        }

        public override string ToString() => $"{Organization} {Project} {Number}";
    }

    public sealed class DefinitionInfo
    {
        public DefinitionKey Key { get; }
        public string Name { get; }

        public string Organization => Key.Organization;
        public string Project => Key.Project;
        public int Id => Key.Id;
        public string DefinitionUri => Key.DefinitionUri;

        public DefinitionInfo(DefinitionKey key, string name)
        {
            Key = key;
            Name = name;
        }

        public DefinitionInfo(string organization, string project, int id, string name) 
            : this(new DefinitionKey(organization, project, id), name)
        {
        }

        public override string ToString() => $"{Project} {Name} {Id}";
    }
}