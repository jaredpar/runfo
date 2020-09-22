using Azure.Storage.Queues;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
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

        public async Task QueueTriageBuild(BuildKey buildKey)
        {
            var buildMessage = new BuildMessage(buildKey);
            var text = JsonConvert.SerializeObject(buildMessage);
            var queue = new QueueClient(_connectionString, QueueNameTriageBuild);
            await queue.SendMessageEncodedAsync(text).ConfigureAwait(false);
        }
    }
}
