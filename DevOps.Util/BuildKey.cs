#nullable enable

using System;

namespace DevOps.Util
{
    public readonly struct BuildKey : IEquatable<BuildKey>
    {
        public string Organization { get; }

        public string Project { get; }

        public int Number { get; }

        public string BuildUri => DevOpsUtil.GetBuildUri(Organization, Project, Number);

        public BuildKey(string organization, string project, int number)
        {
            Organization = organization;
            Project = project;
            Number = number;
        }

        public BuildKey(Build build)
        {
            Organization = DevOpsUtil.GetOrganization(build);
            Project = build.Project.Name;
            Number = build.Id;
        }

        public static bool operator==(BuildKey left, BuildKey right) => left.Equals(right); 

        public static bool operator!=(BuildKey left, BuildKey right) => !left.Equals(right); 

        public static implicit operator BuildKey(Build build) => new BuildKey(build);

        public bool Equals(BuildKey other) =>
            other.Organization == Organization &&
            other.Project == Project &&
            other.Number == Number;

        public override bool Equals(object? other) => other is BuildKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(Organization, Project, Number);

        public override string ToString() => $"{Organization} {Project} {Number}";
    }

    public readonly struct BuildAttemptKey : IEquatable<BuildAttemptKey>
    {
        public BuildKey BuildKey { get; }

        public int Attempt { get; }

        public string Organization => BuildKey.Organization;

        public string Project => BuildKey.Project;

        public int Number => BuildKey.Number;

        public string BuildUri => BuildKey.BuildUri;

        public BuildAttemptKey(string organization, string project, int number, int attempt)
        {
            BuildKey = new BuildKey(organization, project, number);
            Attempt = attempt;
        }

        public BuildAttemptKey(Build build, Timeline timeline)
        {
            BuildKey = new BuildKey(build);
            Attempt = timeline.GetAttempt();
        }

        public static bool operator==(BuildAttemptKey left, BuildAttemptKey right) => left.Equals(right); 

        public static bool operator!=(BuildAttemptKey left, BuildAttemptKey right) => !left.Equals(right); 

        public bool Equals(BuildAttemptKey other) =>
            other.BuildKey == BuildKey &&
            other.Attempt == Attempt;

        public override bool Equals(object? other) => other is BuildAttemptKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(BuildKey, Attempt);

        public override string ToString() => $"{Organization} {Project} {Number} {Attempt};
    }

    public readonly struct BuildDefinitionKey : IEquatable<BuildDefinitionKey>
    {
        public string Organization { get; }
        public string Project { get; }
        public int Id { get; }

        public BuildDefinitionKey(string organization, string project, int id)
        {
            Organization = organization;
            Project = project;
            Id = id;
        }

        public static bool operator==(BuildDefinitionKey left, BuildDefinitionKey right) => left.Equals(right); 

        public static bool operator!=(BuildDefinitionKey left, BuildDefinitionKey right) => !left.Equals(right); 

        public bool Equals(BuildDefinitionKey other) =>
            other.Organization == Organization &&
            other.Project == Project &&
            other.Id == Id;

        public override bool Equals(object? other) => other is BuildDefinitionKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(Organization, Project, Id);

        public override string ToString() => $"{Organization} {Project} {Id}";
    }
}