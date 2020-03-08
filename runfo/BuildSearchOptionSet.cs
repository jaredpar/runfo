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

    internal DateTime? Before { get; set; }

    internal DateTime? After { get; set; }

    internal bool IncludePullRequests { get; set; }

    internal string Project { get; set; }

    internal BuildSearchOptionSet()
    {
        Add("b|build=", "build id to print tests for", b => BuildIds.Add(b));
        Add("d|definition=", "build definition (name|id)(:project)?", d => Definitions.Add(d));
        Add("c|count=", "count of builds to show for a definition", (int c) => SearchCount = c);
        Add("pr", "include pull requests", p => IncludePullRequests = p is object);
        Add("before=", "filter to builds before this date", (DateTime d) => Before = d);
        Add("after=", "filter to builds after this date", (DateTime d) => After = d);
        Add("p|project=", "default project to search (public)", p => Project = p);
        Add("r|repository=", "repository to search against", r => Repository = r);
    }
}
