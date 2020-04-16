using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Mono.Options;
using Model;
using static RuntimeInfoUtil;

internal sealed partial class RuntimeInfo
{
    internal async Task<int> Triage(IEnumerable<string> args)
    {
        using var context = new Model.RuntimeInfoDbContext();
        var optionSet = new BuildSearchOptionSet();
        var extra = optionSet.Parse(args);
        if (extra.Count == 0)
        {
            optionSet.WriteOptionDescriptions(Console.Out);
            var text = string.Join(' ', extra);
            throw new Exception($"Extra arguments: {text}");
        }

        switch (extra[0])
        {
            case "list":
                await RunList();
                break;
            default:
                Console.WriteLine($"Unrecognized option {extra}");
                break;
        }

        return ExitSuccess;

        async Task RunList()
        {
            foreach (var build in await GetBuilds())
            {
                Console.WriteLine($"{build.Id} {DevOpsUtil.GetBuildUri(build)}");
            }
        }

        async Task<IEnumerable<Build>> GetBuilds()
        {
            var list = await ListBuildsAsync(optionSet);
            return list.Where(x => !IsTriaged(x));
        }

        bool IsTriaged(Build build)
        {
            var key = RuntimeInfoModelUtil.GetTriageBuildKey(build);
            var triagedBuild = context.TriageBuilds
                .Where(x => x.Id == key)
                .FirstOrDefault();
            return triagedBuild?.IsComplete == true;
        }
    }
}