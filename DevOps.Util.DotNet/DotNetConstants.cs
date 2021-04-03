using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet
{
    public static class DotNetConstants
    {
        // public const string ConfigurationSqlConnectionString = "RUNFO_CONNECTION_STRING2";
#if DEBUG
        public const string ConfigurationSqlConnectionString = "RUNFO_CONNECTION_STRING2_TEST";
#endif
        public const string ConfigurationAppAzureToken = "RUNFO_AZURE_TOKEN";
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