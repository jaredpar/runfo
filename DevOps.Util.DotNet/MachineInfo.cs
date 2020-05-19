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

        public string? ContainerImage { get; set; }

        public bool IsHelixSubmission { get; set; }

        public string FriendlyName => ContainerName ?? ContainerImage ?? QueueName;

        public bool IsContainer => ContainerImage is object;

        public MachineInfo(
            string queueName,
            string jobName,
            string? containerName,
            string? containerImage,
            bool isHelixSubmission)
        {
            QueueName = queueName;
            JobName = jobName;
            ContainerName = containerName;
            ContainerImage = containerImage;
            IsHelixSubmission = isHelixSubmission;
        }

        public override string ToString() => QueueName;
    }
}

