using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace DevOps.Status.Util
{
    public sealed class GitHubClientFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GitHubClientFactory(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// This creates a <see cref="GitHubClient"/> that doesn't have any authenication associated with it.
        ///
        /// TODO: this should really be deleted. It's a bridge until I can get GitHubApp credentials fully plumbed
        /// through my services
        /// </summary>
        public GitHubClient CreateAnonymous() => new GitHubClient(new ProductHeaderValue(Constants.ProductName));

        public async Task<GitHubClient> CreateForUserOrAnonymousAsync()
        {
            if (_httpContextAccessor.HttpContext.User?.Identity?.IsAuthenticated == true)
            {
                return await CreateForUserAsync();
            }

            return CreateAnonymous();
        }

        public async Task<GitHubClient> CreateForUserAsync()
        {
            var accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
            return CreateForToken(accessToken, AuthenticationType.Oauth);
        }

        public async Task<GitHubClient> CreateForAppAsync()
        {
            // See: https://octokitnet.readthedocs.io/en/latest/github-apps/ for details.

            var appId = Convert.ToInt32(_configuration["GitHubAppId"]);
            var privateKey = _configuration["GitHubAppPrivateKey"];

            var privateKeySource = new PlainStringPrivateKeySource(privateKey);
            var generator = new GitHubJwtFactory(
                privateKeySource,
                new GitHubJwtFactoryOptions
                {
                    AppIntegrationId = appId,
                    ExpirationSeconds = 600                    
                });
            var token = generator.CreateEncodedJwtToken();

            var client = CreateForToken(token, AuthenticationType.Bearer);

            var installations = await client.GitHubApps.GetAllInstallationsForCurrent();
            var installation = installations.Single();
            var installationTokenResult = await client.GitHubApps.CreateInstallationToken(installation.Id);

            return CreateForToken(installationTokenResult.Token, AuthenticationType.Oauth);
        }

        public static GitHubClient CreateForToken(string token, AuthenticationType authenticationType)
        {
            var productInformation = new ProductHeaderValue(Constants.ProductName);
            var client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(token, authenticationType)
            };
            return client;
        }

        public sealed class PlainStringPrivateKeySource : IPrivateKeySource
        {
            private readonly string _key;

            public PlainStringPrivateKeySource(string key)
            {
                _key = key;
            }

            public TextReader GetPrivateKeyReader()
            {
                return new StringReader(_key);
            }
        }
    }
}
