using System;
using System.Collections.Generic;
using Mono.Options;

internal sealed class BuildSearchOptionSet : OptionSet
{
    internal const string DefaultProject = "public";

    internal const int DefaultSearchCount = 5;

    internal List<string> BuildIds { get; set; } = new List<string>();

    internal List<string> Definitions { get; set; } = new List<string>();

    internal int? SearchCount { get; set; }

    internal string Repository { get; set; }

    internal string Branch { get; set; }

    internal DateTimeOffset? Before { get; set; }

    internal DateTimeOffset? After { get; set; }

    internal bool IncludePullRequests { get; set; }

    internal string Project { get; set; }

    internal BuildSearchOptionSet()
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
    }
}
