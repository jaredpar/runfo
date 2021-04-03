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

        /// <summary>
        /// This function will queue up a number of <see cref="ModelBuildAttempt"/> instances to triage against the specified 
        /// <see cref="ModelTrackingIssue"/>. This is useful to essentially seed old builds against a given tracking 
        /// issue (aka populate the data set) while at the same time new builds will be showing up via normal completion.
        /// It will return the number of attempts that were queued for processing,
        ///
        /// One particular challenge we have to keep in mind is that this is going to be queueing up a lot of builds 
        /// into our Azure functions. Those will scale to whatever data we put into there. Need to be mindful to not 
        /// queue up say 100,000 builds as that will end up spiking all our resources. Have to put some throttling
        /// in here.
        /// </summary>
        public static async Task<int> QueueTriageBuildAttempts(
            this FunctionQueueUtil util,
            TriageContextUtil triageContextUtil,
            ModelTrackingIssue trackingIssue,
            SearchBuildsRequest buildsRequest,
            int limit = 200)
        {
            // Need to filter to a bulid definition other wise there is no reasonable way to filter the builds. Any
            // triage is basically pointless.
            if (trackingIssue.ModelBuildDefinition is null && !buildsRequest.HasDefinition)
            {
                throw new Exception("Must filter to a build definition");
            }

            // Ensure there is some level of filtering occuring here.
            if (buildsRequest.BuildResult is null)
            {
                buildsRequest.BuildResult = new BuildResultRequestValue(ModelBuildResult.Succeeded, EqualsKind.NotEquals);
            }

            if (buildsRequest.Queued is null && buildsRequest.Started is null && buildsRequest.Finished is null)
            {
                buildsRequest.Queued = new DateRequestValue(7, RelationalKind.GreaterThan);
            }

            var buildsQuery = buildsRequest
                .Filter(triageContextUtil.Context.ModelBuilds)
                .OrderByDescending(x => x.BuildNumber)
                .SelectMany(x => x.ModelBuildAttempts)
                .Select(x => new
                {
                    x.ModelBuild.BuildNumber,
                    x.ModelBuild.AzureOrganization,
                    x.ModelBuild.AzureProject,
                    x.Attempt,
                    x.Id
                });

            var existingAttemptsQuery = triageContextUtil
                .Context
                .ModelTrackingIssueResults
                .Where(x => x.ModelTrackingIssueId == trackingIssue.Id)
                .Include(x => x.ModelBuildAttempt)
                .ThenInclude(x => x.ModelBuild)
                .Select(x => new
                {
                    x.ModelBuildAttempt.ModelBuild.AzureOrganization,
                    x.ModelBuildAttempt.ModelBuild.AzureProject,
                    x.ModelBuildAttempt.ModelBuild.BuildNumber,
                    x.ModelBuildAttempt.Attempt,
                });

            var existingAttemptsResults = await existingAttemptsQuery.ToListAsync();
            var existingAttemptsSet = new HashSet<BuildAttemptKey>(
                existingAttemptsResults.Select(x => new BuildAttemptKey(x.AzureOrganization, x.AzureProject, x.BuildNumber, x.Attempt)));

            var attempts = await buildsQuery.ToListAsync();
            var attemptKeys = attempts
                .Select(x => new BuildAttemptKey(x.AzureOrganization, x.AzureProject, x.BuildNumber, x.Attempt))
                .Where(x => !existingAttemptsSet.Contains(x))
                .Take(limit)
                .ToList();

            await util.QueueTriageBuildAttemptsAsync(trackingIssue, attemptKeys);

            return attemptKeys.Count;
        }
    }
}
