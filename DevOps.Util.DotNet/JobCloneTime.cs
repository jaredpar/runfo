using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.DotNet
{
    public readonly struct JobCloneTime
    {
        public string JobName { get; }
        public DateTimeOffset StartTime { get; }
        public TimeSpan Duration { get; }

        /// <summary>
        /// Size of the fetch operation specified in KiB
        /// </summary>
        public double? FetchSize { get; }

        /// <summary>
        /// Mix speed of the fetch operation specified in KiB
        /// </summary>
        public double? MinFetchSpeed { get; }

        /// <summary>
        /// Max speed of the fetch operation specified in KiB
        /// </summary>
        public double? MaxFetchSpeed { get; }

        /// <summary>
        /// Average speed of the fetch operation secified in KiB
        /// </summary>
        public double? AverageFetchSpeed { get; }

        public JobCloneTime(
            string jobName,
            DateTimeOffset startTime,
            TimeSpan duration,
            double? fetchSize = null,
            double? minFetchSpeed = null,
            double? maxFetchSpeed = null,
            double? averageFetchSpeed = null)
        {
            JobName = jobName;
            StartTime = startTime;
            Duration = duration;
            FetchSize = fetchSize;
            MinFetchSpeed = minFetchSpeed;
            MaxFetchSpeed = maxFetchSpeed;
            AverageFetchSpeed = averageFetchSpeed;
        }
    }
}
