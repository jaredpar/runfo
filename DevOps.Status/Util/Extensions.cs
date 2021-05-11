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
        public static async Task<(int Queued, int Total)> QueueTriageBuildAttempts(
            this FunctionQueueUtil util,
            TriageContextUtil triageContextUtil,
            ModelTrackingIssue modelTrackingIssue,
            string extraQuery,
            int limit = 200)
        {
            var context = triageContextUtil.Context;
            var (query, request) = GetQueryData();

            var attempts = await query
                .Include(x => x.ModelBuild)
                .Select(x => new
                {
                    x.ModelBuild.BuildNumber,
                    x.ModelBuild.AzureOrganization,
                    x.ModelBuild.AzureProject,
                    x.Attempt,
                    ModelBuildAttemptId = x.Id
                }).ToListAsync();

            // Want to filter to attempts that haven't already been triaged so grab the set of already 
            // triaged ids and use that to filter down the list. 
            var triagedAttempts = await context
                .ModelTrackingIssueResults
                .Where(x => x.ModelTrackingIssueId == modelTrackingIssue.Id)
                .Select(x => x.ModelBuildAttemptId)
                .ToListAsync()
                .ConfigureAwait(false);
            var triageAttemptsSet = new HashSet<int>(triagedAttempts);

            var attemptKeys = attempts
                .Where(x => !triageAttemptsSet.Contains(x.ModelBuildAttemptId))
                .Select(x => new BuildAttemptKey(x.AzureOrganization, x.AzureProject, x.BuildNumber, x.Attempt))
                .ToList();
            var total = attemptKeys.Count;
            var queued = total >= limit ? limit : total;

            await util.QueueTriageBuildAttemptsAsync(modelTrackingIssue, attemptKeys.Take(limit));

            return (queued, total);

            (IQueryable<ModelBuildAttempt> Query, SearchRequestBase SearchRequest) GetQueryData()
            {
                switch (modelTrackingIssue.TrackingKind)
                {
                    case TrackingKind.Timeline:
                        {
                            var request = new SearchTimelinesRequest(modelTrackingIssue.SearchQuery);
                            request.ParseQueryString(extraQuery);
                            UpdateRequest(request);
                            var query = request.Filter(context.ModelTimelineIssues).Select(x => x.ModelBuildAttempt).Distinct();
                            return (query, request);
                        }
                    case TrackingKind.Test:
                        {
                            var request = new SearchTestsRequest(modelTrackingIssue.SearchQuery);
                            request.ParseQueryString(extraQuery);
                            UpdateRequest(request);
                            var query = request.Filter(context.ModelTestResults).Select(x => x.ModelBuildAttempt).Distinct();
                            return (query, request);
                        }
                    case TrackingKind.HelixLogs:
                        {
                            var request = new SearchHelixLogsRequest(modelTrackingIssue.SearchQuery);
                            request.ParseQueryString(extraQuery);
                            UpdateRequest(request);
                            var query = request.Filter(context.ModelTestResults).Select(x => x.ModelBuildAttempt).Distinct();
                            return (query, request);
                        }
                    default:
                        throw new InvalidOperationException($"Invalid kind {modelTrackingIssue.TrackingKind}");
                }

                void UpdateRequest(SearchRequestBase requestBase)
                {
                    if (modelTrackingIssue.ModelBuildDefinition is { } definition)
                    {
                        requestBase.Definition = definition.DefinitionNumber.ToString();
                    }

                    if (requestBase.BuildResult is null)
                    {
                        requestBase.BuildResult = new BuildResultRequestValue(ModelBuildResult.Succeeded, EqualsKind.NotEquals);
                    }

                    if (requestBase.Started is null)
                    {
                        throw new InvalidOperationException($"Must provide a start date");
                    }
                }
            }
        }
    }
}
