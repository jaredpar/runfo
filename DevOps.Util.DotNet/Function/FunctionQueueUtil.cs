using Azure.Storage.Queues;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DevOps.Util.DotNet.Function.FunctionConstants;

namespace DevOps.Util.DotNet.Function
{
    public sealed class FunctionQueueUtil
    {
        private readonly string _connectionString;

        public FunctionQueueUtil(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task EnsureAllQueues(CancellationToken cancellationToken = default)
        {
            foreach (var name in AllQueueNames)
            {
                var client = new QueueClient(_connectionString, name);
                await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task QueueTriageBuildAsync(BuildKey buildKey)
        {
            var buildMessage = new BuildMessage(buildKey);
            var text = JsonConvert.SerializeObject(buildMessage);
            var queue = new QueueClient(_connectionString, QueueNameTriageBuild);
            await queue.SendMessageEncodedAsync(text).ConfigureAwait(false);
        }

        public async Task QueueTriageBuildsAsync(ModelTrackingIssue modelTrackingIssue, IEnumerable<BuildKey> buildKeys)
        {
            var message = new TriageTrackingIssueRangeMessage()
            {
                ModelTrackingIssueId = modelTrackingIssue.Id,
                BuildMessages = buildKeys.Select(x => new BuildMessage(x)).ToArray(),
            };
            var text = JsonConvert.SerializeObject(message);
            var queue = new QueueClient(_connectionString, QueueNameTriageTrackingIssueRange);
            await queue.SendMessageEncodedAsync(text).ConfigureAwait(false);
        }

        public async Task QueueUpdateIssueAsync(ModelTrackingIssue modelTrackingIssue, TimeSpan? delay)
        {
            var updateMessage = new IssueUpdateManualMessage(modelTrackingIssue.Id);
            var text = JsonConvert.SerializeObject(updateMessage);
            var queue = new QueueClient(_connectionString, QueueNameIssueUpdateManual);
            await queue.SendMessageEncodedAsync(text, visibilityTimeout: delay).ConfigureAwait(false);
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
        public async Task<(int Queued, int Total)> QueueTriageBuildAttempts(
            TriageContext context,
            ModelTrackingIssue modelTrackingIssue,
            string extraQuery,
            int limit = 200)
        {
            var (query, request) = GetQueryData();

            var builds = await query
                .Select(x => new
                {
                    x.BuildNumber,
                    x.AzureOrganization,
                    x.AzureProject,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var buildKeys = builds.Select(x => new BuildKey(x.AzureOrganization, x.AzureProject, x.BuildNumber)).ToList();
            var total = buildKeys.Count;
            var queued = total >= limit ? limit : total;

            await QueueTriageBuildsAsync(modelTrackingIssue, buildKeys.Take(limit));

            return (queued, total);

            (IQueryable<ModelBuild> Query, SearchRequestBase SearchRequest) GetQueryData()
            {
                switch (modelTrackingIssue.TrackingKind)
                {
                    case TrackingKind.Timeline:
                        {
                            var request = new SearchTimelinesRequest(modelTrackingIssue.SearchQuery);
                            request.ParseQueryString(extraQuery);
                            UpdateRequest(request);
                            var query = request.Filter(context.ModelTimelineIssues).Select(x => x.ModelBuild);
                            return (query, request);
                        }
                    case TrackingKind.Test:
                        {
                            var request = new SearchTestsRequest(modelTrackingIssue.SearchQuery);
                            request.ParseQueryString(extraQuery);
                            UpdateRequest(request);
                            var query = request.Filter(context.ModelTestResults).Select(x => x.ModelBuild);
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
