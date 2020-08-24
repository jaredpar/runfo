
using System;

namespace DevOps.Util.DotNet
{
    public sealed class HelixJobTimelineInfo
    {
        public string JobId { get; }

        public MachineInfo MachineInfo { get; }

        public HelixJobTimelineInfo(string jobId, MachineInfo machineInfo)
        {
            JobId = jobId;
            MachineInfo = machineInfo;
        }
    }
}