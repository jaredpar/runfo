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
        /// This function will queue up a number of <see cref="ModelBuild"/> instances to triage against the specified 
        /// <see cref="ModelTrackingIssue"/>. This is useful to essentially seed old builds against a given tracking 
        /// issue (aka populate the data set) while at the same time new builds will be showing up via normal completion.
        ///
        /// One particular challenge we have to keep in mind is that this is going to be queueing up a lot of builds 
        /// into our Azure functions. Those will scale to whatever data we put into there. Need to be mindful to not 
        /// queue up say 100,000 builds as that will end up spiking all our resources. Have to put some throttling
        /// in here.
        /// </summary>
        public static async Task QueueTriageBuildQuery(
            this FunctionQueueUtil util,
            TriageContextUtil triageContextUtil,
            ModelTrackingIssue trackingIssue,
            SearchBuildsRequest buildsRequest,
            int limit = 250)
        {
            // Need to filter to a bulid definition other wise there is no reasonable way to filter the builds. Any
            // triage is basically pointless.
            if (trackingIssue.ModelBuildDefinition is null && !buildsRequest.HasDefinition)
            {
                throw new Exception("Must filter to a build definition");
            }

            if (buildsRequest.HasDefinition)
            {
                buildsRequest.Definition = trackingIssue.ModelBuildDefinition!.DefinitionId.ToString();
            }

            if (buildsRequest.Result is null)
            {
                buildsRequest.Result = new BuildResultRequestValue(BuildResult.Succeeded, EqualsKind.NotEquals);
            }

            if (buildsRequest.Queued is null && buildsRequest.Started is null && buildsRequest.Finished is null)
            {
                buildsRequest.Queued = new DateRequestValue(7, RelationalKind.GreaterThan);
            }

            var query = buildsRequest
                .Filter(triageContextUtil.Context.ModelBuilds)
                .Take(limit)
                .SelectMany(x => x.ModelBuildAttempts)
                .Select(x => new
                {
                    x.ModelBuild.BuildNumber,
                    x.ModelBuild.AzureOrganization,
                    x.ModelBuild.AzureProject,
                    x.Attempt,
                    x.Id
                });

            var attempts = await query.ToListAsync();
            foreach (var attempt in attempts)
            {
                var key = new BuildAttemptKey(
                    new BuildKey(attempt.AzureOrganization, attempt.AzureProject, attempt.BuildNumber),
                    attempt.Attempt);
                await util.QueueTriageBuildAttemptAsync(key, trackingIssue);
            }
        }
    }
}
