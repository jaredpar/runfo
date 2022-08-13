using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet
{
    public static class DotNetConstants
    {
        public const string KeyVaultEndPointProduction = "https://runfo-prod.vault.azure.net/";
        public const string KeyVaultEndPointTest = "https://runfo-test.vault.azure.net/";
#if DEBUG
        public const string KeyVaultEndPoint = KeyVaultEndPointTest;
#else
        public const string KeyVaultEndPoint = KeyVaultEndPointProduction;
#endif

        public const string ConfigurationSqlConnectionString = "RunfoConnectionString";
        public const string ConfigurationAzdoToken = "RunfoAzdoToken";
        public const string ConfigurationGitHubImpersonateUser = "GitHubImpersonateUser";
        public const string ConfigurationGitHubAppId = "GitHubAppId";
        public const string ConfigurationGitHubAppPrivateKey = "GitHubAppPrivateKey";
        public const string ConfigurationGitHubClientId = "GitHubClientId";
        public const string ConfigurationGitHubClientSecret = "GitHubClientSecret";
        public const string ConfigurationAzureBlobConnectionString = "AzureWebJobsStorage";

        public static string GitHubOrganization => "dotnet";
        public static string AzureOrganization => "dnceng";
        public static string DefaultAzureProject => "public";

    }
}
