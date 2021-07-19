using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet
{
    public static class DotNetConstants
    {
#if DEBUG
        public const string KeyVaultEndPoint = "https://runfo-test.vault.azure.net/";
#else
        public const string KeyVaultEndPoint = "https://runfo-prod.vault.azure.net/";
#endif


        public const string ConfigurationSqlConnectionString = "RunfoConnectionString";
#if DEBUG
        //public const string ConfigurationSqlConnectionString = "RunfoConnectionStringTest";
#endif
        public const string ConfigurationAzdoToken = "RunfoAzdoToken";
        public const string ConfigurationGitHubAppId = "GitHubAppId";
        public const string ConfigurationGitHubAppPrivateKey = "GitHubAppPrivateKey";
        public const string ConfigurationGitHubClientId = "GitHubClientId";
        public const string ConfigurationGitHubClientSecret = "GitHubClientSecret";
        public const string ConfigurationVsoClientId = "VsoClientId";
        public const string ConfigurationVsoClientSecret = "VsoClientSecret";
        public const string ConfigurationAzureBlobConnectionString = "AzureWebJobsStorage";

        public static string GitHubOrganization => "dotnet";
        public static string AzureOrganization => "dnceng";
        public static string DefaultAzureProject => "public";

    }
}