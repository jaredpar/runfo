using System;

namespace DevOps.Util
{
    public readonly struct BuildKey
    {
        public readonly string Organization;
        public readonly string Project;
        public readonly int Number;

        public string BuildUri => DevOpsUtil.GetBuildUri(Organization, Project, Number);

        public BuildKey(string organization, string project, int number)
        {
            Organization = organization;
            Project = project;
            Number = number;
        }

        public override string ToString() => $"{Organization} {Project} {Number}";
    }

    public readonly struct BuildDefinitionKey
    {
        public readonly string Organization;
        public readonly string Project;
        public readonly int Id;

        public BuildDefinitionKey(string organization, string project, int id)
        {
            Organization = organization;
            Project = project;
            Id = id;
        }

        public override string ToString() => $"{Organization} {Project} {Id}";
    }
}