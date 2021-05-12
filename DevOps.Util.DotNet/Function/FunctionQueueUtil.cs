using Azure.Storage.Queues;
using DevOps.Util.DotNet.Triage;
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

        public async Task QueueTriageBuildAttemptsAsync(ModelTrackingIssue modelTrackingIssue, IEnumerable<BuildAttemptKey> attemptKeys)
        {
            var message = new TriageTrackingIssueRangeMessage()
            {
                ModelTrackingIssueId = modelTrackingIssue.Id,
                BuildAttemptMessages = attemptKeys.Select(x => new BuildAttemptMessage(x)).ToArray(),
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
    }
}
