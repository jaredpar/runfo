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
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mono.Options;
using Octokit;
using static DevOps.Util.DotNet.OptionSetUtil;

[assembly: Microsoft.Extensions.Configuration.UserSecrets.UserSecretsId("67c4a872-5dd7-422a-acad-fdbe907ace33")]

internal class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;

    public static async Task Main(string[] args) => await MainCore(args.ToList());

    public static bool IsDevelopment(IConfiguration configuration) => !string.IsNullOrEmpty(configuration["RUNFO_DEV"]);

    private static IConfiguration CreateConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();
            return config;
    }

    private static void ConfigureOptions(DbContextOptionsBuilder builder, IConfiguration configuration, bool isDevelopment)
    {
        if (isDevelopment)
        {
            var connectionString = configuration["RUNFO_CONNECTION_STRING_DEV"];
            builder.UseSqlServer(connectionString);
        }
        else
        {
            var connectionString = configuration["RUNFO_CONNECTION_STRING"];
            builder.UseSqlServer(connectionString);
        }
    }

    // This entry point exists so that `dotnet ef database` and `migrations` has an 
    // entry point to create TriageDbContext
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<TriageContext>(options => Config(options));
            });

        static void Config(DbContextOptionsBuilder builder)
        {
            var configuration = CreateConfiguration();
            var kind = IsDevelopment(configuration) ? "dev" : "production";
            Console.WriteLine($"Using {kind} sql");
            ConfigureOptions(builder, configuration, IsDevelopment(configuration));
        }
    }

    // internal static async Task<int> Main(string[] args) => await MainCore(args.ToList());

    internal static async Task<int> MainCore(List<string> args)
    {
        await Task.Delay(1);
        return 1;
        /*
        var (server, context) = Create(ref args);
        var gitHubClient = GitHubClientFactory.
        var queryUtil = new DotNetQueryUtil(server, gitHubClient);
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

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

        var autoTriageUtil = new AutoTriageUtil(server, context, gitHubClient, loggerFactory.CreateLogger<AutoTriageUtil>());
        var gitHubUtil = new TriageGitHubUtil(gitHubClient, context, loggerFactory.CreateLogger<GitHubUtil>());
        switch (command)
        {
            case "list":
                await RunList();
                break;
            case "rebuild":
                await RunRebuild();
                break;
            case "issues":
                await RunIssues();
                break;
            case "scratch":
                await RunScratch();
                break;
            default:
                Console.WriteLine($"Unrecognized option {command}");
                break;
        }

        return ExitSuccess;

        async Task RunList()
        {
            foreach (var build in await queryUtil.ListBuildsAsync("-d runtime"))
            {
                Console.WriteLine(DevOpsUtil.GetBuildUri(build));
                var key = build.GetBuildKey();
                var jobs = await queryUtil.ListFailedJobs(build);
                foreach (var job in jobs)
                {
                    var result = context.ModelTriageIssueResults
                        .Include(x => x.ModelBuild)
                        .Include(x => x.ModelTriageIssue)
                        .Where(x => 
                            x.JobName == job.Name &&
                            x.ModelBuild.BuildNumber == key.Number)
                        .FirstOrDefault();
                    if (result is null)
                    {
                        Console.WriteLine($" Unknown: {job.Name}");
                    }
                    else
                    {
                        Console.WriteLine($" {result.ModelTriageIssue.TriageIssueKind}: {job.Name}");
                    }
                }
            }
        }

        async Task RunRebuild()
        {
            autoTriageUtil.EnsureTriageIssues();
            await autoTriageUtil.TriageQueryAsync("-d runtime -c 1000 -pr");
            await autoTriageUtil.TriageQueryAsync("-d aspnet -c 1000 -pr");
            await autoTriageUtil.TriageQueryAsync("-d runtime-official -c 100 -pr");
            await autoTriageUtil.TriageQueryAsync("-d aspnet-official -c 100 -pr");
        }

        async Task RunIssues()
        {
            autoTriageUtil.EnsureTriageIssues();
            await gitHubUtil.UpdateGithubIssues();
            // await gitHubUtil.UpdateStatusIssue();
        }

        async Task RunScratch()
        {
            // autoTriageUtil.EnsureTriageIssues();
            // await autoTriageUtil.Triage("-d aspnet -c 100 -pr");
            // await autoTriageUtil.Triage("-d runtime -c 500 -pr");
            //await autoTriageUtil.Triage("-d runtime -c 100 -pr");
            // await gitHubUtil.UpdateGithubIssues
            autoTriageUtil.EnsureTriageIssues();
            await gitHubUtil.UpdateGithubIssues();
            
        }

        static (DevOpsServer Server, TriageContext Context) Create(ref List<string> args)
        {
            var azdoToken = Environment.GetEnvironmentVariable("RUNFO_AZURE_TOKEN");
            var gitHubToken = Environment.GetEnvironmentVariable("RUNFO_GITHUB_TOKEN");
            if (gitHubToken.Contains(':'))
            {
                gitHubToken = gitHubToken.Split(new[] {':'}, count: 2)[1];
            }

            var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "runfo", "json");
            var configuration = CreateConfiguration();
            var isDevelopment = IsDevelopment(configuration);

            var devSql = isDevelopment;
            var devGitHub = isDevelopment;
            var optionSet = new OptionSet()
            {
                { "ds|devsql", "Use sql", d => devSql = d is object },
                { "dg|devgithub", "Use devops-util issues", d => devGitHub = d is object },
            };

            args = optionSet.Parse(args);
            Console.WriteLine($"Using dev SQL {devSql}");
            Console.WriteLine($"Using dev GitHub {devGitHub}");

            var builder = new DbContextOptionsBuilder<TriageContext>();
            ConfigureOptions(builder, configuration, devSql);
            var context = new TriageContext(builder.Options);
            var server = new DevOpsServer(
                "dnceng",
                new CachingAzureClient(cacheDirectory, azdoToken));

            return (server, context);
        }
        */
    }
}