using System;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

[assembly: FunctionsStartup(typeof(DevOps.Functions.Startup))]

namespace DevOps.Functions
{
    internal class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            var config = new ConfigurationBuilder()
                .AddAzureKeyVault(DotNetConstants.KeyVaultEndPoint, keyVaultClient, new DefaultKeyVaultSecretManager())
                .Build();

            var connectionString = config[DotNetConstants.ConfigurationSqlConnectionString];
            var azdoToken = config[DotNetConstants.ConfigurationAzdoToken];
            var helixToken = config[DotNetConstants.ConfigurationAzdoToken];
            builder.Services.AddDbContext<TriageContext>(
                options => options.UseSqlServer(connectionString, o => o.CommandTimeout((int)TimeSpan.FromMinutes(10).TotalSeconds)));
            builder.Services.AddScoped<DevOpsServer>(_ =>
                new DevOpsServer(
                    DotNetConstants.AzureOrganization,
                    new AuthorizationToken(AuthorizationKind.PersonalAccessToken, azdoToken)));

            // Using anonymous Helix for now as it has a higher API rate limit than authenticated
            // https://github.com/dotnet/arcade/issues/10764
            builder.Services.AddScoped(_ => new HelixServer(token: null));

            builder.Services.AddScoped<GitHubClientFactory>(_ =>
            {
                var appId = int.Parse(config[DotNetConstants.ConfigurationGitHubAppId]);
                var appPrivateKey = config[DotNetConstants.ConfigurationGitHubAppPrivateKey];
                return new GitHubClientFactory(appId, appPrivateKey);
            });
        }
    }
}