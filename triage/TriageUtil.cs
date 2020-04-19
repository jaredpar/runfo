using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using Model;

internal enum TriageReasonItem
{
    Azure,
    Helix,

    NuGet,

    // General infrastructure owned by the .NET Team
    Infra,

    Build,
    Test,
    Other
}


// TODO: this class is designed to work when there is only one DB writer 
// occurring. That's a design flaw. Need to fix.
internal sealed class TriageUtil : IDisposable
{
    internal RuntimeInfoDbContext Context { get; }

    internal TriageUtil()
    {
        Context = new RuntimeInfoDbContext();
    }

    public void Dispose()
    {
        Context.Dispose();
    }

    internal bool IsReason(BuildKey key, string reason, string issue)
    {
        var triageKey = RuntimeInfoModelUtil.GetTriageBuildKey(key);
        return Context.TriageReasons
            .Where(x => x.TriageBuildId == triageKey && x.Reason == reason && x.IssueUri == issue)
            .Any();
    }

    internal bool IsTriaged(Build build)
    {
        var key = RuntimeInfoModelUtil.GetTriageBuildKey(build);
        var triagedBuild = Context.TriageBuilds
            .Where(x => x.Id == key)
            .FirstOrDefault();
        return triagedBuild?.IsComplete == true;
    }

    internal bool TryAddReason(BuildKey key, TriageReasonItem reason, string issue)
    {
        if (IsReason(key, reason.ToString(),  issue))
        {
            return false;
        }

        var triageBuild = GetOrCreateTriageBuild(key);
        var triageReason = new TriageReason()
        {
            Reason = reason.ToString(),
            IssueUri = issue,
            TriageBuildId = triageBuild.Id,
            TriageBuild = triageBuild,
        };

        Context.TriageReasons.Add(triageReason);
        Context.SaveChanges();
        return true;
    }

    internal TriageBuild GetOrCreateTriageBuild(BuildKey key)
    {
        var triageKey = RuntimeInfoModelUtil.GetTriageBuildKey(key);
        var triageBuild = Context.TriageBuilds.SingleOrDefault(x => x.Id == triageKey);
        if (triageBuild is null)
        {
            triageBuild = new TriageBuild()
            {
                Id = triageKey,
                Organization = key.Organization,
                Project = key.Project,
                BuildNumber = key.Id
            };
            Context.TriageBuilds.Add(triageBuild);
            Context.SaveChanges();
        }

        return triageBuild;
    }
}
