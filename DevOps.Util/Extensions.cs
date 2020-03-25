#nullable enable

using System;

namespace DevOps.Util
{
    public static class DevOpsUtilExtensions
    {
        public static DateTimeOffset? ConvertRestTime(string? time)
        {
            if (time is null || !DateTime.TryParse(time, out var dateTime))
            {
                return null;
            }

            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            return new DateTimeOffset(dateTime);
        }

        public static DateTimeOffset? GetStartTime(this Build build) => ConvertRestTime(build.StartTime);
        public static DateTimeOffset? GetQueueTime(this Build build) => ConvertRestTime(build.QueueTime);
        public static DateTimeOffset? GetFinishTime(this Build build) => ConvertRestTime(build.FinishTime);

        public static int? GetByteSize(this BuildArtifact buildArtifact) => DevOpsUtil.GetArtifactByteSize(buildArtifact);
    }

}