using System;

namespace DevOps.Util
{
    public readonly struct BuildKey
    {
        public readonly string Organization;
        public readonly string Project;
        public readonly int Id;

        public BuildKey(string organization, string project, int id)
        {
            Organization = organization;
            Project = project;
            Id = id;
        }

        public override string ToString() => $"{Organization} {Project} {Id}";
    }
}