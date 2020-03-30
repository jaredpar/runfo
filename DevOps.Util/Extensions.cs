#nullable enable

using System;

namespace DevOps.Util
{
    public static class DevOpsUtilExtensions
    {
        public static DateTimeOffset? GetStartTime(this Build build) => DevOpsUtil.ConvertRestTime(build.StartTime);

        public static DateTimeOffset? GetQueueTime(this Build build) => DevOpsUtil.ConvertRestTime(build.QueueTime);

        public static DateTimeOffset? GetFinishTime(this Build build) => DevOpsUtil.ConvertRestTime(build.FinishTime);

        public static int? GetByteSize(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactByteSize(buildArtifact);

        public static BuildArtifactKind GetKind(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactKind(buildArtifact);
    }
}