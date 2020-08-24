using System;
using System.Collections.Generic;
using Mono.Options;

namespace DevOps.Util.DotNet
{
    public class BuildSearchOptionSet : OptionSet
    {
        public const int DefaultSearchCount = 5;

        public List<string> BuildIds { get; set; } = new List<string>();

        public List<string> Definitions { get; set; } = new List<string>();

        public List<int> ExcludedBuildIds { get; set; } = new List<int>();

        public int? SearchCount { get; set; }

        public string? Repository { get; set; }

        public string? Branch { get; set; }

        public DateTimeOffset? Before { get; set; }

        public DateTimeOffset? After { get; set; }

        public bool IncludePullRequests { get; set; }

        public string? Project { get; set; }

        public BuildSearchOptionSet()
        {
            Add("d|definition=", "build definition (name|id)(:project)?", d => Definitions.Add(d));
            Add("p|project=", "default project to search (public)", p => Project = p);
            Add("c|count=", "count of builds to show for a definition", (int c) => SearchCount = c);
            Add("pr", "include pull requests", p => IncludePullRequests = p is object);
            Add("before=", "filter to builds before this date", (DateTime d) => Before = d);
            Add("after=", "filter to builds after this date", (DateTime d) => After = d);
            Add("r|repository=", "filter to repository", r => Repository = r);
            Add("br|branch=", "filter to builds against this branch", b => Branch = b);
            Add("b|build=", "build id to print tests for", b => BuildIds.Add(b));
            Add("e|exclude=", "exclude build ids from the results", (int b) => ExcludedBuildIds.Add(b));
        }
    }
}
