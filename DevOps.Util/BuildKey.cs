#nullable enable

using System;

namespace DevOps.Util
{
    public readonly struct BuildKey
    {
        // TODO: Consider strongly removing this and just noting that build keys are only
        // relevant in their org. So many times we're passing BuildKey in the context of
        // a DevOpsServer. That just introduces the possibility of getting different
        // orgs
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