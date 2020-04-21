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
            case "auto":
                await RunAutoTriage(args);
                break;
            case "rebuild":
                await RunRebuild();
                break;
            case "scratch":
                await RunScratch();
                break;
            default:
                Console.WriteLine($"Unrecognized option {command}");
                break;
        }

        return ExitSuccess;

        async Task RunAutoTriage(List<string> args)
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            autoTriageUtil.EnsureTriageIssues();
            // TODO: need to triage builds that occurred since last query
            await autoTriageUtil.Triage("-d runtime -c 100 -pr");
        }

        async Task RunRebuild()
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            autoTriageUtil.EnsureTriageIssues();
            await autoTriageUtil.Triage("-d runtime -c 100 -pr");
            await autoTriageUtil.UpdateQueryIssues();
        }

        async Task RunScratch()
        {
            using var autoTriageUtil = new AutoTriageUtil(server, gitHubClient);
            // autoTriageUtil.EnsureTriageIssues();
            // await autoTriageUtil.Triage("-d runtime -c 100 -pr");
            await autoTriageUtil.UpdateQueryIssues();
        }
    }
}