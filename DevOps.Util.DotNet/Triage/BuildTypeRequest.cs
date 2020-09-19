using Microsoft.VisualBasic;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace DevOps.Util.Triage
{
    // TODO: Unify this into a single comparison type that can be used across all the values
    public enum BuildTypeRequestKind
    {
        Equals,
        NotEquals,
    }

    public readonly struct BuildTypeRequest
    {
        public ModelBuildKind BuildType { get;  }
        public BuildTypeRequestKind Kind { get; }
        public string? BuildTypeName { get; }

        public BuildTypeRequest(ModelBuildKind buildType, BuildTypeRequestKind kind, string? buildTypeName = null)
        {
            BuildType = buildType;
            Kind = kind;
            BuildTypeName = buildTypeName;
        }

        public string GetQueryValue(BuildTypeRequestKind? defaultKind = null)
        {
            var prefix = "";
            if (defaultKind != Kind)
            {
                prefix = Kind switch
                {
                    BuildTypeRequestKind.Equals => "=",
                    BuildTypeRequestKind.NotEquals => "!",
                    _ => throw new InvalidOperationException($"{Kind}")
                };
            }

            var name = BuildTypeName ?? BuildType.ToString();
            return $"{prefix}{name}";
        }

        public static BuildTypeRequest Parse(string data, BuildTypeRequestKind defaultKind)
        {
            var kind = defaultKind;
            if (data.Length > 0)
            {
                switch (data[0])
                {
                    case '=':
                        kind = BuildTypeRequestKind.Equals;
                        data = data.Substring(1);
                        break;
                    case '!':
                        kind = BuildTypeRequestKind.NotEquals;
                        data = data.Substring(1);
                        break;
                }
            }

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

            return new BuildTypeRequest(buildType, kind, data);
        }
    }
}
