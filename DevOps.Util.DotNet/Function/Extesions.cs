using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Function
{
    public static class Extesions
    {
        /// <summary>
        /// Work around the fact that the SDK took a breaking change around whether or not messages were 
        /// encoded by default. This will base 64 encode them before enqueueing which fixes compatibility 
        /// issues with Azure Functions
        /// https://github.com/Azure/azure-sdk-for-net/issues/10242
        /// </summary>
        public static async Task<SendReceipt> SendMessageEncodedAsync(this QueueClient queueClient, string messageText)
        {
            var bytes = Encoding.UTF8.GetBytes(messageText);
            var encodedMessageText = Convert.ToBase64String(bytes);
            return await queueClient.SendMessageAsync(encodedMessageText).ConfigureAwait(false);
        }
    }
}
