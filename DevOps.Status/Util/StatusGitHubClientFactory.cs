using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util.DotNet;
using GitHubJwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace DevOps.Status.Util
{
    public sealed class StatusGitHubClientFactory
    {
        private readonly IConfiguration _configuration;
        private readonly GitHubClientFactory _gitHubClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public StatusGitHubClientFactory(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _gitHubClientFactory = new GitHubClientFactory(configuration);
        }

        /// <summary>
        /// This creates a <see cref="GitHubClient"/> that doesn't have any authenication associated with it.
        ///
        /// TODO: this should really be deleted. It's a bridge until I can get GitHubApp credentials fully plumbed
        /// through my services
        /// </summary>
        public GitHubClient CreateAnonymous() => GitHubClientFactory.CreateAnonymous();

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
            return GitHubClientFactory.CreateForToken(accessToken, AuthenticationType.Oauth);
        }

        public async Task<GitHubClient> CreateForAppAsync(string owner, string repository) =>
            await _gitHubClientFactory.CreateForAppAsync(owner, repository);
    }
}
