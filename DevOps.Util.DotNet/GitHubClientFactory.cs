using GitHubJwt;
using Microsoft.Extensions.Configuration;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public interface IGitHubClientFactory
    {
        void SetUserOAuthToken(string token);
        Task<IGitHubClient> CreateForAppAsync(string owner, string repository);
    }

    public sealed class OAuthAppClientFactory : IGitHubClientFactory
    {
        string _oauthToken;

        #region IGitHubClientFactory

        public void SetUserOAuthToken(string token)
        {
            _oauthToken = token;
        }

        async Task<IGitHubClient> IGitHubClientFactory.CreateForAppAsync(string owner, string repository)
        {
            if(_oauthToken == null)
            {
                throw new Exception("This action requires the user to be logged in via GitHub first");
            }
            return GitHubClientFactory.CreateForToken(_oauthToken, AuthenticationType.Oauth);
        }

        #endregion
    }

    public sealed class GitHubAppClientFactory : IGitHubClientFactory
    {
        private string AppPrivateKey { get; }
        public int AppId { get; }

        public GitHubAppClientFactory(int appId, string appPrivateKey)
        {
            AppId = appId;
            AppPrivateKey = appPrivateKey;
        }

        public async Task<GitHubClient> CreateForAppAsync(string owner, string repository)
        {
            var gitHubClient = CreateForAppCore();
            var installation = await gitHubClient.GitHubApps.GetRepositoryInstallationForCurrent(owner, repository).ConfigureAwait(false);
            var installationToken = await gitHubClient.GitHubApps.CreateInstallationToken(installation.Id);
            return GitHubClientFactory.CreateForToken(installationToken.Token, AuthenticationType.Oauth);
        }

        private GitHubClient CreateForAppCore()
        {
            // See: https://octokitnet.readthedocs.io/en/latest/github-apps/ for details.

            var privateKeySource = new PlainStringPrivateKeySource(AppPrivateKey);
            var generator = new GitHubJwtFactory(
                privateKeySource,
                new GitHubJwtFactoryOptions
                {
                    AppIntegrationId = AppId,
                    ExpirationSeconds = 600
                });

            var token = generator.CreateEncodedJwtToken();

            return GitHubClientFactory.CreateForToken(token, AuthenticationType.Bearer);
        }

        #region IGitHubClientFactory

        public void SetUserOAuthToken(string token) { }

        async Task<IGitHubClient> IGitHubClientFactory.CreateForAppAsync(string owner, string repository) =>
            await CreateForAppAsync(owner, repository).ConfigureAwait(false);

        #endregion

        private sealed class PlainStringPrivateKeySource : IPrivateKeySource
        {
            private readonly string _key;

            internal PlainStringPrivateKeySource(string key)
            {
                _key = key;
            }

            public TextReader GetPrivateKeyReader() => new StringReader(_key);
        }
    }

    public static class GitHubClientFactory
    {
        public const string GitHubProductName = "runfo.azurewebsites.net";

        public static GitHubClient CreateAnonymous() => new GitHubClient(new ProductHeaderValue(GitHubProductName));

        public static GitHubClient CreateForToken(string token, AuthenticationType authenticationType)
        {
            var productInformation = new ProductHeaderValue(GitHubProductName);
            var client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(token, authenticationType)
            };
            return client;
        }

        public static IGitHubClientFactory Create(IConfiguration configuration)
        {
            if (configuration[DotNetConstants.ConfigurationGitHubImpersonateUser] == "true")
            {
                return new OAuthAppClientFactory();
            }
            else
            {
                return new GitHubAppClientFactory(int.Parse(configuration.GetNonNull(DotNetConstants.ConfigurationGitHubAppId)),
                    configuration.GetNonNull(DotNetConstants.ConfigurationGitHubAppPrivateKey));
            }
        }
    }
}
