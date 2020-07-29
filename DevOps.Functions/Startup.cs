using System;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

[assembly: Microsoft.Extensions.Configuration.UserSecrets.UserSecretsId("67c4a872-5dd7-422a-acad-fdbe907ace33")]
[assembly: FunctionsStartup(typeof(DevOps.Functions.Startup))]

namespace DevOps.Functions
{
    internal class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Startup>()
                .AddEnvironmentVariables()
                .Build();

            var connectionString = config["RUNFO_CONNECTION_STRING"];
            var azdoToken = config["RUNFO_AZURE_TOKEN"];
            builder.Services.AddDbContext<TriageContext>(options => options.UseSqlServer(connectionString));
            builder.Services.AddScoped<DevOpsServer>(_ => new DevOpsServer(DotNetUtil.AzureOrganization, azdoToken));
            builder.Services.AddScoped<GitHubClientFactory>(_ =>
            {
                var appId = int.Parse(config[DotNetConstants.ConfigurationGitHubAppId]);
                var appPrivateKey = config[DotNetConstants.ConfigurationGitHubAppPrivateKey];
                return new GitHubClientFactory(appId, appPrivateKey);
            });
        }
    }
}