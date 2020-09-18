using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace DevOps.Util.Triage
{
    public enum DateRequestKind
    {
        GreaterThan,
        LessThan,
    }

    public readonly struct DateRequest
    {
        public DateTimeOffset DateTime { get; }
        public DateRequestKind Kind { get; }
        private int? DayQuery { get; }

        public DateRequest(DateTimeOffset dateTime, DateRequestKind kind)
        {
            DateTime = dateTime;
            Kind = kind;
            DayQuery = null;
        }

        public DateRequest(int dayQuery, DateRequestKind kind = DateRequestKind.GreaterThan)
        {
            DateTime = System.DateTimeOffset.UtcNow - TimeSpan.FromDays(dayQuery);
            Kind = kind;
            DayQuery = dayQuery;
        }

        public string GetQueryValue(DateRequestKind? defaultKind = null)
        {
            var prefix = "";
            if (defaultKind != Kind)
            {
                prefix = Kind switch
                {
                    DateRequestKind.LessThan => "<",
                    DateRequestKind.GreaterThan => ">",
                    _ => throw new InvalidOperationException($"{Kind}")
                };
            }

            if (DayQuery is { } days)
            {
                return $"{prefix}~{days}";
            }

            return $"{prefix}{DateTime.ToLocalTime().ToString("yyyy-MM-dd")}";
        }

        public static DateRequest Parse(string data, DateRequestKind defaultKind)
        {
            var kind = defaultKind;
            if (string.IsNullOrEmpty(data))
            {
                throw GetException();
            }

            switch (data[0])
            {
                case '<':
                    kind = DateRequestKind.LessThan;
                    data = data.Substring(1);
                    break;
                case '>':
                    kind = DateRequestKind.GreaterThan;
                    data = data.Substring(1);
                    break;
            }

            if (string.IsNullOrEmpty(data))
            {
                throw GetException();
            }

            if (data[0] == '~')
            {
                var days = int.Parse(data.Substring(1));
                return new DateRequest(days, kind);
            }

            var dt = System.DateTime.ParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            dt = System.DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return new DateRequest(new DateTimeOffset(dt.ToUniversalTime()), kind);

            Exception GetException() => new Exception($"Invalid format {data}");
        }
    }
}
