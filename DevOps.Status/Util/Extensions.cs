using AspNet.Security.OAuth.GitHub;
using AspNet.Security.OAuth.VisualStudio;
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

        public static ClaimsIdentity? GetVsoIdentity(this ClaimsPrincipal principal) =>
            principal.Identities.FirstOrDefault(x => x.AuthenticationType == VisualStudioAuthenticationDefaults.AuthenticationScheme);

        public static async Task<IGitHubClient> CreateForUserAsync(this IGitHubClientFactory gitHubClientFactory, HttpContext httpContext)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");
            return GitHubClientFactory.CreateForToken(accessToken, AuthenticationType.Oauth);
        }

        public static async Task QueueTriageBuildQuery(this FunctionQueueUtil util, TriageContextUtil triageContextUtil, ModelTrackingIssue trackingIssue, SearchBuildsRequest buildsRequest, int limit = 100)
        {
            var resultQuery = triageContextUtil.Context
                .ModelTrackingIssueResults
                .Where(x => x.ModelTrackingIssueId == trackingIssue.Id)
                .Select(x => x.ModelBuildAttempt);
            resultQuery = buildsRequest.Filter(resultQuery);
            var attemptIds = await resultQuery
                .Select(x => x.Id)
                .ToListAsync();
            var attemptIdSet = new HashSet<int>(attemptIds);

            IQueryable<ModelBuild> query = triageContextUtil.GetModelBuildsQuery(trackingIssue, buildsRequest);
            var attemptsQuery = query
                .SelectMany(x => x.ModelBuildAttempts)
                .Select(x => new
                {
                    x.ModelBuild.BuildNumber,
                    x.ModelBuild.AzureOrganization,
                    x.ModelBuild.AzureProject,
                    x.Attempt,
                    x.Id
                });

            var count = 0;
            var attempts = await attemptsQuery.ToListAsync();
            foreach (var attempt in attempts)
            {
                if (!attemptIds.Contains(attempt.Id))
                {
                    var key = new BuildAttemptKey(
                        new BuildKey(attempt.AzureOrganization, attempt.AzureProject, attempt.BuildNumber),
                        attempt.Id);
                    await util.QueueTriageBuildAttemptAsync(key, trackingIssue);
                }

                if (count++ >= limit)
                {
                    break;
                }
            }
        }
    }
}
