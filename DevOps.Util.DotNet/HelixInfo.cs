using System;

namespace DevOps.Util.DotNet
{
    public readonly struct HelixInfo : IEquatable<HelixInfo>
    {
        public string JobId { get; }
        public string WorkItemName { get; }

        public HelixInfo(string jobId, string workItemName)
        {
            JobId = jobId;
            WorkItemName = workItemName;
        }

        public static bool operator==(HelixInfo left, HelixInfo right) => left.Equals(right);
        public static bool operator!=(HelixInfo left, HelixInfo right) => !left.Equals(right);
        public bool Equals(HelixInfo other) => JobId == other.JobId && WorkItemName == other.WorkItemName;
        public override int GetHashCode() => HashCode.Combine(JobId, WorkItemName);
        public override bool Equals(object obj) => obj is HelixInfo info && Equals(info);
        public override string ToString() => $"Job Id: {JobId} Work Item Name: {WorkItemName}";
    }

}