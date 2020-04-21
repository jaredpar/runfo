using System;

namespace DevOps.Util
{
    public readonly struct BuildKey
    {
        public readonly string Organization;
        public readonly string Project;
        public readonly int Number;

        public BuildKey(string organization, string project, int number)
        {
            Organization = organization;
            Project = project;
            Number = number;
        }

        public override string ToString() => $"{Organization} {Project} {Number}";
    }
}