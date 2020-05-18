#nullable enable

namespace DevOps.Util.DotNet
{
    public sealed class MachineInfo
    {
        public const string UnknownContainerQueueName = "Unknown Container Host";
        public const string UnknownHelixQueueName = "Unknown Helix Queue";

        public string QueueName { get; set; }

        public string JobName { get; set; }

        public string? ContainerName { get; set; }

        public bool IsHelixSubmission { get; set; }

        public MachineInfo(
            string queueName,
            string jobName,
            string? containerName,
            bool isHelixSubmission)
        {
            QueueName = queueName;
            JobName = jobName;
            ContainerName = containerName;
            IsHelixSubmission = isHelixSubmission;
        }

        public override string ToString() => QueueName;
    }
}

