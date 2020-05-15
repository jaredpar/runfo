
#nullable enable

using System;

namespace DevOps.Util.DotNet
{
    public sealed class HelixJobTimelineInfo
    {
        public string JobId { get; }

        public string? QueueName { get; }

        public HelixJobTimelineInfo(string jobId, string? queueName)
        {
            JobId = jobId;
            QueueName = queueName;
        }
    }
}