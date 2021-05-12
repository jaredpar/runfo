using System;

namespace DevOps.Util
{
    public readonly struct BuildKey : IEquatable<BuildKey>
    {
        public string Organization { get; }
        public string Project { get; }
        public int Number { get; }

        public string BuildUri => DevOpsUtil.GetBuildUri(Organization, Project, Number);

        public string NameKey => $"{Organization}-{Project}-{Number}";

        public BuildKey(string organization, string project, int number)
        {
            if (organization is null)
            {
                throw new ArgumentNullException(nameof(organization));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            Organization = organization;
            Project = project;
            Number = number;
        }

        public BuildKey(Build build) : 
            this(DevOpsUtil.GetOrganization(build), build.Project.Name, build.Id)
        {
        }

        public static BuildKey FromNameKey(string nameKey)
        {
            var parts = nameKey.Split('-');
            return new BuildKey(parts[0], parts[1], int.Parse(parts[2]));
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

        public BuildAttemptKey(BuildKey buildKey, int attempt)
        {
            BuildKey = buildKey;
            Attempt = attempt;
        }

        public static bool operator==(BuildAttemptKey left, BuildAttemptKey right) => left.Equals(right); 

        public static bool operator!=(BuildAttemptKey left, BuildAttemptKey right) => !left.Equals(right); 

        public bool Equals(BuildAttemptKey other) =>
            other.BuildKey == BuildKey &&
            other.Attempt == Attempt;

        public override bool Equals(object? other) => other is BuildAttemptKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(BuildKey, Attempt);

        public override string ToString() => $"{Organization} {Project} {Number} {Attempt}";
    }

    public readonly struct DefinitionKey : IEquatable<DefinitionKey>
    {
        public string Organization { get; }
        public string Project { get; }
        public int Id { get; }

        public string DefinitionUri => DevOpsUtil.GetDefinitionUri(Organization, Project, Id);

        public DefinitionKey(string organization, string project, int id)
        {
            Organization = organization;
            Project = project;
            Id = id;
        }

        public static bool operator==(DefinitionKey left, DefinitionKey right) => left.Equals(right); 

        public static bool operator!=(DefinitionKey left, DefinitionKey right) => !left.Equals(right); 

        public bool Equals(DefinitionKey other) =>
            other.Organization == Organization &&
            other.Project == Project &&
            other.Id == Id;

        public override bool Equals(object? other) => other is DefinitionKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(Organization, Project, Id);

        public override string ToString() => $"{Organization} {Project} {Id}";
    }

    public readonly struct DefinitionNameKey : IEquatable<DefinitionNameKey>
    {
        public string Organization { get; }
        public string Project { get; }
        public string Name { get; }

        public DefinitionNameKey(string organization, string project, string name)
        {
            Organization = organization;
            Project = project;
            Name = name;
        }

        public static bool operator==(DefinitionNameKey left, DefinitionNameKey right) => left.Equals(right); 

        public static bool operator!=(DefinitionNameKey left, DefinitionNameKey right) => !left.Equals(right);

        public bool Equals(DefinitionNameKey other) =>
            other.Organization == Organization &&
            other.Project == Project &&
            other.Name == Name;

        public override bool Equals(object? other) => other is DefinitionNameKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(Organization, Project, Name);

        public override string ToString() => $"{Organization} {Project} {Name}";
    }
}