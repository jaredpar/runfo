using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet
{
    public static class DotNetConstants
    {
        public const string ConfigurationSqlConnectionString = "RUNFO_CONNECTION_STRING";
#if DEBUG
        //public const string ConfigurationSqlConnectionString = "RUNFO_CONNECTION_STRING_DEV";
#endif
        public const string ConfigurationGitHubAppId = "GitHubAppId";
        public const string ConfigurationGitHubAppPrivateKey = "GitHubAppPrivateKey";
        public const string ConfigurationGitHubClientId = "GitHubClientId";
        public const string ConfigurationGitHubClientSecret = "GitHubClientSecret";
        public const string ConfigurationAzureBlobConnectionString = "AzureWebJobsStorage";
    }
}