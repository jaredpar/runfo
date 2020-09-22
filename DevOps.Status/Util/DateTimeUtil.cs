using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
	public sealed class DateTimeUtil
    {
        public TimeZoneInfo TimeZoneInfo { get;  }

        public DateTimeUtil(TimeZoneInfo? timeZoneInfo = null)
        {
            try
            {
                TimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            }
            catch
            {
                TimeZoneInfo = TimeZoneInfo.Utc;
            }
        }

        public DateTime? ConvertDateTime(DateTime? dateTime) =>
            dateTime is { } dt ? ConvertDateTime(dt) : (DateTime?)null;

        public DateTime ConvertDateTime(DateTime dateTime)
        {
            // DateTimes are all stored in the SQL server as UTC but EF will create them with the 
            // Kind vaule of Unspecified. Fix that up to be UTC to start this off.
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, TimeZoneInfo);
        }
    }
}
