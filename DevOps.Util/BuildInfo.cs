#nullable enable

using System;

namespace DevOps.Util
{
    public class BuildInfo
    {
        public BuildKey Key { get; }

        public BuildDefinitionInfo DefinitionInfo { get; }

        public GitHubPullRequestKey? PullRequestKey { get;}

        public int? PullRequestNumber => PullRequestKey?.Number;

        public string Organization => Key.Organization;

        public string Project => Key.Project;

        public int Number => Key.Number;

        public string DefinitionName => DefinitionInfo.Name;

        public string BuildUri => Key.BuildUri;

        public BuildInfo(
            BuildKey buildKey,
            BuildDefinitionInfo buildDefinitionInfo,
            GitHubPullRequestKey? pullRequestKey)
        {
            Key = buildKey;
            DefinitionInfo = buildDefinitionInfo;
            PullRequestKey = pullRequestKey;
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