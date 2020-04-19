
using System;
using System.Collections.Generic;
using Mono.Options;

internal sealed class TriageOptionSet : OptionSet
{
    internal const string DefaultProject = BuildSearchOptionSet.DefaultProject;

    internal List<string> BuildIds { get; set; } = new List<string>();

    internal List<string> FilePaths { get; set; } = new List<string>();

    internal TriageOptionSet()
    {
        Add("b|build=", "build to add a reason", (string b) => BuildIds.Add(b));
        Add("f|file-paths=", "file containing build URIs", (string f) => FilePaths.Add(f));
    }
}
