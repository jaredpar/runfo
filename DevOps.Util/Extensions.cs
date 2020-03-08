using System;

namespace DevOps.Util
{
    public static class DevOpsUtilExtensions
    {
        public static DateTime? GetStartTime(this Build build) => 
            build.StartTime is object && DateTime.TryParse(build.StartTime, out var dateTime) 
            ? dateTime
            : (DateTime?)null;
    }

}