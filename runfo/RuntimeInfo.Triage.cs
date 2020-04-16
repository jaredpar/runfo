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
    private enum Reason
    {
        Azure,
        Helix,
        Build,
        Test,
        Other
    }

    internal async Task<int> Triage(List<string> args)
    {
        using var context = new RuntimeInfoDbContext();
        string command;
        if (args.Count == 0)
        {
            command = "list";
        }
        else
        {
            command = args[0];
            args = args.Skip(1).ToList();
        }

        switch (command)
        {
            case "list":
                await RunList(args);
                break;
            case "reason":
                RunReason(args);
                break;
            default:
                Console.WriteLine($"Unrecognized option {command}");
                break;
        }

        return ExitSuccess;

        async Task RunList(List<string> args)
        {
            using var context = new RuntimeInfoDbContext();
            var optionSet = new BuildSearchOptionSet();
            ParseAll(optionSet, args);
            foreach (var build in await GetUntriagedBuilds(optionSet, context))
            {
                Console.WriteLine($"{build.Id} {DevOpsUtil.GetBuildUri(build)}");
            }
        }

        void RunReason(List<string> args)
        {
            string reason = null;
            string issue = null;
            string buildInfo = null;
            var optionSet = new OptionSet()
            {
                { "r|reason=", "Azure,Helix,Build,Test,Other", (string r) => reason = r },
                { "i|issue=", "issue uri", (string i) => issue = i },
                { "b|build=", "build to add a reason", (string b) => buildInfo = b},
            };

            ParseAll(optionSet, args);

            if (reason == null || !Enum.TryParse<Reason>(reason, ignoreCase: true, out var reasonValue))
            {
                throw OptionFailureWithException("Need to provide a reason", optionSet);
            }

            if (!TryGetBuildId(buildInfo, BuildSearchOptionSet.DefaultProject, out var project, out var buildId))
            {
                throw OptionFailureWithException("Need a valid build", optionSet);
            }

            var triageBuild = GetOrCreateTriageBuild(project, buildId);
            var triageReason = new TriageReason()
            {
                Reason = reasonValue.ToString(),
                IssueUri = issue,
                TriageBuildId = triageBuild.Id,
                TriageBuild = triageBuild,
            };
            context.TriageReasons.Add(triageReason);
            context.SaveChanges();
        }

        async Task<IEnumerable<Build>> GetUntriagedBuilds(BuildSearchOptionSet optionSet, RuntimeInfoDbContext context)
        {
            var list = await ListBuildsAsync(optionSet);
            return list.Where(x => !IsTriaged(x, context));
        }

        bool IsTriaged(Build build, RuntimeInfoDbContext context)
        {
            var key = RuntimeInfoModelUtil.GetTriageBuildKey(build);
            var triagedBuild = context.TriageBuilds
                .Where(x => x.Id == key)
                .FirstOrDefault();
            return triagedBuild?.IsComplete == true;
        }

        TriageBuild GetOrCreateTriageBuild(string project, int buildId)
        {
            var organization = Server.Organization;
            var key = RuntimeInfoModelUtil.GetTriageBuildKey(organization, project, buildId);
            var triageBuild = context.TriageBuilds.SingleOrDefault(x => x.Id == key);
            if (triageBuild is null)
            {
                triageBuild = new TriageBuild()
                {
                    Id = key,
                    Organization = organization,
                    Project = project,
                    BuildNumber = buildId
                };
                context.TriageBuilds.Add(triageBuild);
                context.SaveChanges();
            }

            return triageBuild;
        }
    }
}