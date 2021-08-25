
using System;

namespace DevOps.Util.DotNet
{
    public sealed class HelixJobTimelineInfo
    {
        public string JobId { get; }
        public MachineInfo MachineInfo { get; }
        public TimeSpan Duration { get; }

        public string AzureJobName => MachineInfo.JobName;

        public HelixJobTimelineInfo(string jobId, MachineInfo machineInfo, TimeSpan duration)
        {
            JobId = jobId;
            MachineInfo = machineInfo;
            Duration = duration;
        }
    }
}