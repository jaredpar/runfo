using System;

namespace DevOps.Util
{
    public readonly struct RepositoryInfo
    {
        public readonly string Id { get; }
        public readonly string Type { get; }

        public RepositoryInfo(string id, string type)
        {
            Id = id;
            Type = type;
        }
    }
}