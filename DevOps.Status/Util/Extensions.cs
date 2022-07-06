using AspNet.Security.OAuth.GitHub;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Function;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NodeTypeResolvers;

namespace DevOps.Status.Util
{
    public static class Extensions
    {
        public static ClaimsIdentity? GetGitHubIdentity(this ClaimsPrincipal principal) =>
            principal.Identities.FirstOrDefault(x => x.AuthenticationType == GitHubAuthenticationDefaults.AuthenticationScheme);

        public static async Task<IGitHubClient> CreateForUserAsync(this IGitHubClientFactory gitHubClientFactory, HttpContext httpContext)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");
            return GitHubClientFactory.CreateForToken(accessToken, AuthenticationType.Oauth);
        }

    }
}
