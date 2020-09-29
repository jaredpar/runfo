using AspNet.Security.OAuth.GitHub;
using AspNet.Security.OAuth.VisualStudio;
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

        public static ClaimsIdentity? GetVsoIdentity(this ClaimsPrincipal principal) =>
            principal.Identities.FirstOrDefault(x => x.AuthenticationType == VisualStudioAuthenticationDefaults.AuthenticationScheme);

        public static async Task<IGitHubClient> CreateForUserAsync(this IGitHubClientFactory gitHubClientFactory, HttpContext httpContext)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");
            return GitHubClientFactory.CreateForToken(accessToken, AuthenticationType.Oauth);
        }

        public static async Task QueueTriageBuildQuery(this FunctionQueueUtil util, TriageContextUtil triageContextUtil, ModelTrackingIssue trackingIssue, SearchBuildsRequest buildsRequest, int limit = 100)
        {
            IQueryable<ModelBuild> query = triageContextUtil.GetModelBuildsQuery(trackingIssue);
            query = buildsRequest.Filter(query);
            var attemptsQuery = query
                .Where(x => !triageContextUtil.Context.ModelTrackingIssueResults.Any(r => r.ModelBuildAttempt.ModelBuildId == x.Id))
                .SelectMany(x => x.ModelBuildAttempts)
                .Include(x => x.ModelBuild)
                .Take(limit);
            var attempts = await attemptsQuery.ToListAsync();
            foreach (var attempt in attempts)
            {
                var key = attempt.GetBuildAttemptKey();
                await util.QueueTriageBuildAttemptAsync(key, trackingIssue);
            }
        }
    }
}
