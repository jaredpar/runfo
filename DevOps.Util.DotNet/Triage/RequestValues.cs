using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DevOps.Util.DotNet.Triage
{
    public interface IRequestValue<TComparison>
        where TComparison: struct, Enum
    {
        string GetQueryValue(TComparison? defaultKind);
    }

    public enum EqualsKind
    {
        Equals,
        NotEquals,
    }

    public enum RelationalKind
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
    }

    public enum StringRelationalKind
    {
        Contains,
        Equals,
        NotEquals
    }

    internal static class RequestValueUtil
    {
        internal static char GetPrefix(EqualsKind kind) => kind switch
        {
            EqualsKind.Equals => '=',
            EqualsKind.NotEquals => '!',
            _ => throw new Exception($"Invalid value {kind}"),
        };

        internal static (string Data, EqualsKind Kind) Parse(string data, EqualsKind defaultKind)
        {
            if (data.Length == 0)
            {
                return (data, defaultKind);
            }

            return data[0] switch
            {
                '=' => (data.Substring(1), EqualsKind.Equals),
                '!' => (data.Substring(1), EqualsKind.NotEquals),
                _ => (data, defaultKind),
            };
        }

        internal static char GetPrefix(RelationalKind kind) => kind switch
        {
            RelationalKind.Equals => '=',
            RelationalKind.NotEquals => '!',
            RelationalKind.GreaterThan => '>',
            RelationalKind.LessThan => '<',
            _ => throw new Exception($"Invalid value {kind}"),
        };

        internal static (string Data, RelationalKind Kind) Parse(string data, RelationalKind defaultKind)
        {
            if (data.Length == 0)
            {
                return (data, defaultKind);
            }

            return data[0] switch
            {
                '=' => (data.Substring(1), RelationalKind.Equals),
                '!' => (data.Substring(1), RelationalKind.NotEquals),
                '>' => (data.Substring(1), RelationalKind.GreaterThan),
                '<' => (data.Substring(1), RelationalKind.LessThan),
                _ => (data, defaultKind),
            };
        }

        internal static char GetPrefix(StringRelationalKind kind) => kind switch
        {
            StringRelationalKind.Equals => '=',
            StringRelationalKind.NotEquals => '!',
            StringRelationalKind.Contains => '%',
            _ => throw new Exception($"Invalid value {kind}"),
        };

        internal static (string Data, StringRelationalKind Kind) Parse(string data, StringRelationalKind defaultKind)
        {
            if (data.Length == 0)
            {
                return (data, defaultKind);
            }

            return data[0] switch
            {
                '=' => (data.Substring(1), StringRelationalKind.Equals),
                '!' => (data.Substring(1), StringRelationalKind.NotEquals),
                '%' => (data.Substring(1), StringRelationalKind.Contains),
                _ => (data, defaultKind),
            };
        }
    }

    public readonly struct DateRequestValue : IRequestValue<RelationalKind>
    {
        public DateTime DateTime { get; }
        public RelationalKind Kind { get; }
        public int? DayQuery { get; }

        public DateRequestValue(DateTime dateTime, RelationalKind kind)
        {
            DateTime = dateTime;
            Kind = kind;
            DayQuery = null;
        }

        public DateRequestValue(int dayQuery, RelationalKind kind = RelationalKind.GreaterThan)
        {
            DateTime = System.DateTime.UtcNow - TimeSpan.FromDays(dayQuery);
            Kind = kind;
            DayQuery = dayQuery;
        }

        public string GetQueryValue(RelationalKind? defaultKind = null)
        {
            var prefix = Kind == defaultKind ? (char?)null : RequestValueUtil.GetPrefix(Kind);
            if (DayQuery is { } days)
            {
                return $"{prefix}~{days}";
            }

            return $"{prefix}{DateTime.ToLocalTime().ToString("yyyy-MM-dd")}";
        }

        public static DateRequestValue Parse(string data, RelationalKind defaultKind)
        {
            RelationalKind kind;
            (data, kind) = RequestValueUtil.Parse(data, defaultKind);

            if (string.IsNullOrEmpty(data))
            {
                throw new Exception($"Invalid date format {data}");
            }

            if (data[0] == '~')
            {
                var days = int.Parse(data.Substring(1));
                return new DateRequestValue(days, kind);
            }

            var dt = System.DateTime.ParseExact(data, "yyyy-M-d", CultureInfo.InvariantCulture);
            dt = System.DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return new DateRequestValue(dt.ToUniversalTime(), kind);
        }
    }

    public readonly struct BuildKindRequestValue : IRequestValue<EqualsKind>
    {
        public ModelBuildKind BuildKind { get;  }
        public EqualsKind Kind { get; }
        public string? BuildTypeName { get; }

        public BuildKindRequestValue(ModelBuildKind buildKind, EqualsKind kind, string? buildTypeName = null)
        {
            BuildKind = buildKind;
            Kind = kind;
            BuildTypeName = buildTypeName;
        }

        public string GetQueryValue(EqualsKind? defaultKind = null)
        {
            var prefix = Kind == defaultKind ? (char?)null : RequestValueUtil.GetPrefix(Kind);
            var name = BuildTypeName ?? BuildKind.ToString();
            return $"{prefix}{name}";
        }

        public static BuildKindRequestValue Parse(string data, EqualsKind defaultKind)
        {
            EqualsKind kind;
            (data, kind) = RequestValueUtil.Parse(data, defaultKind);
            var buildType = data.ToLower() switch
            {
                "all" => ModelBuildKind.All,
                "rolling" => ModelBuildKind.Rolling,
                "pullrequest" => ModelBuildKind.PullRequest,
                "pr" => ModelBuildKind.PullRequest,
                "mergedpullrequest" => ModelBuildKind.MergedPullRequest,
                "mpr" => ModelBuildKind.MergedPullRequest,
                _ => throw new Exception($"Invalid build type {data}"),
            };

            return new BuildKindRequestValue(buildType, kind, data);
        }
    }

    public readonly struct BuildResultRequestValue : IRequestValue<EqualsKind>
    {
        public ModelBuildResult BuildResult { get; }
        public EqualsKind Kind { get; }
        public string? BuildResultName { get; }

        public BuildResultRequestValue(ModelBuildResult buildResult, EqualsKind kind, string? buildResultName = null)
        {
            BuildResult = buildResult;
            Kind = kind;
            BuildResultName = buildResultName;
        }

        public string GetQueryValue(EqualsKind? defaultKind = null)
        {
            var prefix = Kind == defaultKind ? (char?)null : RequestValueUtil.GetPrefix(Kind);
            var name = BuildResultName ?? BuildResult.ToString();
            return $"{prefix}{name}";
        }

        public static BuildResultRequestValue Parse(string data, EqualsKind defaultKind)
        {
            EqualsKind kind;
            (data, kind) = RequestValueUtil.Parse(data, defaultKind);
            var buildResult = data.ToLower() switch
            {
                "failed" => ModelBuildResult.Failed,
                "canceled" => ModelBuildResult.Canceled,
                "succeeded" => ModelBuildResult.Succeeded,
                "partiallysucceeded" => ModelBuildResult.PartiallySucceeded,
                _ => throw new Exception($"Invalid result {data}")
            };

            return new BuildResultRequestValue(buildResult, kind, data);
        }
    }

    public readonly struct StringRequestValue : IRequestValue<StringRelationalKind>
    {
        public string Text { get; }
        public StringRelationalKind Kind { get; }

        public StringRequestValue(string text, StringRelationalKind kind)
        {
            Text = text;
            Kind = kind;
        }

        public string GetQueryValue(StringRelationalKind? defaultValue = null)
        {
            var prefix = Kind == defaultValue ? (char?)null : RequestValueUtil.GetPrefix(Kind);
            if (Text.Contains(" "))
            {
                return prefix + '"' + Text + '"';
            }

            return prefix + Text;
        }

        public static StringRequestValue Parse(string data, StringRelationalKind defaultKind)
        {
            StringRelationalKind kind;
            (data, kind) = RequestValueUtil.Parse(data, defaultKind);

            data = data.Trim('"');
            if (string.IsNullOrEmpty(data))
            {
                throw new Exception("Must have text");
            }

            return new StringRequestValue(data, kind);
        }
    }
}
