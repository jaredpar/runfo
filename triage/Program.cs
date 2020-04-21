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
using Octokit;
using static DevOps.Util.DotNet.OptionSetUtil;

internal static class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;

    internal static async Task<int> Main(string[] args) => await MainCore(args.ToList());

    internal static async Task<int> MainCore(List<string> args)
    {
        var azdoToken = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
        var gitHubToken = Environment.GetEnvironmentVariable("RUNFO_GITHUB_TOKEN");
        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "runfo", "json");

        var server = new CachingDevOpsServer(cacheDirectory, "dnceng", azdoToken);
        var gitHubClient = new GitHubClient(new ProductHeaderValue("RuntimeStatusPage"));
        var queryUtil = new DotNetQueryUtil(server);

        // TODO: should not hard code jaredpar here
        gitHubClient.Credentials = new Credentials("jaredpar", gitHubToken);

        using var triageUtil = new TriageUtil();
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
            case "complete":
                RunComplete(args);
                break;
            case "auto":
                await RunAutoTriage(args);
                break;
            default:
                Console.WriteLine($"Unrecognized option {command}");
                break;
        }

        return ExitSuccess;

        async Task RunList(List<string> args)
        {
            /*
            var showAll = false;
            var optionSet = new BuildSearchOptionSet()
            {
                { "a|all", "show all builds", a => showAll = a is object },
            };

            ParseAll(optionSet, args);
            foreach (var build in await GetUntriagedBuilds(optionSet, showAll))
            {
                var key = RuntimeInfoModelUtil.GetTriageBuildKey(build);
                var reasons = triageUtil.Context.TriageReasons
                    .Where(x => x.TriageBuildId == key)
                    .Select(x => x.Reason.ToString());
                var reason = string.Join(',', reasons);

                Console.WriteLine($"{DevOpsUtil.GetBuildUri(build)} {reason}");
            }
            */
        }

        void RunComplete(List<string> args)
        {
            /*
            var optionSet = new TriageOptionSet();
            ParseAll(optionSet, args);
            foreach (var key in ListBuildKeys(optionSet))
            {
                var triageBuild = triageUtil.GetOrCreateTriageBuild(key);
                triageBuild.IsComplete = true;
            }
            triageUtil.Context.SaveChanges();
            */
        }

        async Task RunAutoTriage(List<string> args)
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            autoTriageUtil.EnsureTriageIssues();
            await autoTriageUtil.Triage("-d runtime -c 100 -pr");
        }

        void RunReason(List<string> args)
        {
            /*
            string reason = null;
            string issue = null;
            var optionSet = new TriageOptionSet()
            {
                { "r|reason=", "Azure,Helix,Build,Test,Other", (string r) => reason = r },
                { "i|issue=", "issue uri", (string i) => issue = i },
            };

            ParseAll(optionSet, args);

            if (reason == null || !Enum.TryParse<TriageReasonItem>(reason, ignoreCase: true, out var reasonValue))
            {
                throw OptionFailureWithException("Need to provide a reason", optionSet);
            }

            foreach (var key in ListBuildKeys(optionSet))
            {
                if (triageUtil.IsReason(key, reason,  issue))
                {
                    continue;
                }

                var triageBuild = triageUtil.GetOrCreateTriageBuild(key);
                var triageReason = new TriageReason()
                {
                    Reason = reasonValue.ToString(),
                    IssueUri = issue,
                    TriageBuildId = triageBuild.Id,
                    TriageBuild = triageBuild,
                };

                triageUtil.Context.TriageReasons.Add(triageReason);
            }
            triageUtil.Context.SaveChanges();
            */
        }

        async Task<IEnumerable<Build>> GetUntriagedBuilds(BuildSearchOptionSet optionSet, bool showAll = false)
        {
            /*
            IEnumerable<Build> list = await queryUtil.ListBuildsAsync(optionSet);
            list = list.Where(x => x.Result != BuildResult.Succeeded);
            if (!showAll)
            {
                list = list.Where(x => triageUtil.IsTriaged(x));
            }

            return list;
            */
            await Task.Yield();
            throw null;
        }

        List<BuildKey> ListBuildKeys(TriageOptionSet optionSet)
        {
            var list = new List<BuildKey>();
            foreach (var build in optionSet.BuildIds)
            {
                if (!DotNetQueryUtil.TryGetBuildId(build, TriageOptionSet.DefaultProject, out var project, out var buildId))
                {
                    throw OptionFailureWithException("Need a valid build", optionSet);
                }

                list.Add(new BuildKey(server.Organization, project, buildId));
            }

            foreach (var filePath in optionSet.FilePaths)
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    if (Uri.TryCreate(line.Trim(), UriKind.Absolute, out var uri) &&
                        DevOpsUtil.TryParseBuildKey(uri, out var buildKey))
                    {
                        list.Add(buildKey);
                    }
                }
            }

            return list;
        }
    }
}