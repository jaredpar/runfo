using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public abstract class SearchRequestBase
    {
        public static DateRequestValue StartedDefault => new DateRequestValue(dayQuery: 7, kind: RelationalKind.GreaterThan);

        public DateRequestValue? Started { get; set; }
        public string? Definition { get; set; }
        public BuildResultRequestValue? BuildResult { get; set; }
        public BuildKindRequestValue? BuildKind { get; set; }
        public StringRequestValue? TargetBranch { get; set; }

        public bool HasDefinition => !string.IsNullOrEmpty(Definition);

        public int? DefinitionId
        {
            get
            {
                if (!string.IsNullOrEmpty(Definition) && int.TryParse(Definition, out int id))
                {
                    return id;
                }

                return null;
            }
        }

        protected string GetQueryStringCore(StringBuilder builder)
        {
            var started = Started ?? StartedDefault;
            Append($"started:{started.GetQueryValue(RelationalKind.GreaterThan)}");

            if (!string.IsNullOrEmpty(Definition))
            {
                Append($"definition:{Definition}");
            }

            if (BuildResult is { } buildResult)
            {
                Append($"result:{buildResult.GetQueryValue(EqualsKind.Equals)}");
            }

            if (BuildKind is { } buildKind)
            {
                Append($"kind:{buildKind.GetQueryValue(EqualsKind.Equals)}");
            }

            if (TargetBranch is { } targetBranch)
            {
                Append($"targetBranch:{targetBranch.GetQueryValue(StringRelationalKind.Contains)}");
            }

            return builder.ToString();

            void Append(string message)
            {
                if (builder.Length != 0)
                {
                    builder.Append(" ");
                }

                builder.Append(message);
            }
        }

        protected bool ParseQueryStringTuple(string name, string value)
        {
            value = value.Trim();
            switch (name.ToLower())
            {
                case "started":
                    Started = DateRequestValue.Parse(value, RelationalKind.GreaterThan);
                    return true;
                case "definition":
                    Definition = value;
                    return true;
                case "result":
                    BuildResult = BuildResultRequestValue.Parse(value, EqualsKind.Equals);
                    return true;
                case "kind":
                    BuildKind = BuildKindRequestValue.Parse(value, EqualsKind.Equals);
                    return true;
                case "targetbranch":
                    TargetBranch = StringRequestValue.Parse(value, StringRelationalKind.Contains);
                    return true;
                default:
                    return false;
            }
        }

        #region Duplicated Filter Code

        protected IQueryable<ModelBuild> FilterCore(IQueryable<ModelBuild> query)
        {
            var started = Started ?? StartedDefault;
            query = started.Kind switch
            {
                RelationalKind.GreaterThan => query.Where(x => x.StartTime >= started.DateTime.Date),
                RelationalKind.LessThan => query.Where(x => x.StartTime <= started.DateTime.Date),
                _ => query
            };

            if (DefinitionId is { } definitionId)
            {
                query = query.Where(x => x.DefinitionNumber == definitionId);
            }
            else if (Definition is object)
            {
                query = query.Where(x => x.DefinitionName == Definition);
            }

            if (BuildResult is { } buildResult)
            {
                var r = buildResult.BuildResult;
                query = buildResult.Kind switch
                {
                    EqualsKind.Equals => query.Where(x => x.BuildResult == r),
                    EqualsKind.NotEquals => query.Where(x => x.BuildResult != r),
                    _ => query,
                };
            }

            if (BuildKind is { } buildKind)
            {
                var k = buildKind.BuildKind;
                query = (k, buildKind.Kind) switch
                {
                    (ModelBuildKind.All, _) => query,
                    (_, EqualsKind.Equals) => query.Where(x => x.BuildKind == k),
                    (_, EqualsKind.NotEquals) => query.Where(x => x.BuildKind != k),
                    _ => query,
                };
            }

            if (TargetBranch is { } targetBranch)
            {
                query = targetBranch.Kind switch
                {
                    StringRelationalKind.Contains => query.Where(x => x.GitHubTargetBranch != null && x.GitHubTargetBranch.Contains(targetBranch.Text)),
                    StringRelationalKind.Equals => query.Where(x => x.GitHubTargetBranch == targetBranch.Text),
                    StringRelationalKind.NotEquals => query.Where(x => x.GitHubTargetBranch != targetBranch.Text),
                    _ => query,
                };
            }

            return query;
        }

        protected IQueryable<ModelBuildAttempt> FilterCore(IQueryable<ModelBuildAttempt> query)
        {
            var started = Started ?? StartedDefault;
            query = started.Kind switch
            {
                RelationalKind.GreaterThan => query.Where(x => x.StartTime >= started.DateTime.Date),
                RelationalKind.LessThan => query.Where(x => x.StartTime <= started.DateTime.Date),
                _ => query
            };

            if (DefinitionId is { } definitionId)
            {
                query = query.Where(x => x.DefinitionNumber == definitionId);
            }
            else if (Definition is object)
            {
                query = query.Where(x => x.DefinitionName == Definition);
            }

            if (BuildResult is { } buildResult)
            {
                var r = buildResult.BuildResult;
                query = buildResult.Kind switch
                {
                    EqualsKind.Equals => query.Where(x => x.BuildResult == r),
                    EqualsKind.NotEquals => query.Where(x => x.BuildResult != r),
                    _ => query,
                };
            }

            if (BuildKind is { } buildKind)
            {
                var k = buildKind.BuildKind;
                query = (k, buildKind.Kind) switch
                {
                    (ModelBuildKind.All, _) => query,
                    (_, EqualsKind.Equals) => query.Where(x => x.BuildKind == k),
                    (_, EqualsKind.NotEquals) => query.Where(x => x.BuildKind != k),
                    _ => query,
                };
            }

            if (TargetBranch is { } targetBranch)
            {
                query = targetBranch.Kind switch
                {
                    StringRelationalKind.Contains => query.Where(x => x.GitHubTargetBranch != null && x.GitHubTargetBranch.Contains(targetBranch.Text)),
                    StringRelationalKind.Equals => query.Where(x => x.GitHubTargetBranch == targetBranch.Text),
                    StringRelationalKind.NotEquals => query.Where(x => x.GitHubTargetBranch != targetBranch.Text),
                    _ => query,
                };
            }

            return query;
        }

        protected IQueryable<ModelTimelineIssue> FilterCore(IQueryable<ModelTimelineIssue> query)
        {
            var started = Started ?? StartedDefault;
            query = started.Kind switch
            {
                RelationalKind.GreaterThan => query.Where(x => x.StartTime >= started.DateTime.Date),
                RelationalKind.LessThan => query.Where(x => x.StartTime <= started.DateTime.Date),
                _ => query
            };

            if (DefinitionId is { } definitionId)
            {
                query = query.Where(x => x.DefinitionNumber == definitionId);
            }
            else if (Definition is object)
            {
                query = query.Where(x => x.DefinitionName == Definition);
            }

            if (BuildResult is { } buildResult)
            {
                var r = buildResult.BuildResult;
                query = buildResult.Kind switch
                {
                    EqualsKind.Equals => query.Where(x => x.BuildResult == r),
                    EqualsKind.NotEquals => query.Where(x => x.BuildResult != r),
                    _ => query,
                };
            }

            if (BuildKind is { } buildKind)
            {
                var k = buildKind.BuildKind;
                query = (k, buildKind.Kind) switch
                {
                    (ModelBuildKind.All, _) => query,
                    (_, EqualsKind.Equals) => query.Where(x => x.BuildKind == k),
                    (_, EqualsKind.NotEquals) => query.Where(x => x.BuildKind != k),
                    _ => query,
                };
            }

            if (TargetBranch is { } targetBranch)
            {
                query = targetBranch.Kind switch
                {
                    StringRelationalKind.Contains => query.Where(x => x.GitHubTargetBranch != null && x.GitHubTargetBranch.Contains(targetBranch.Text)),
                    StringRelationalKind.Equals => query.Where(x => x.GitHubTargetBranch == targetBranch.Text),
                    StringRelationalKind.NotEquals => query.Where(x => x.GitHubTargetBranch != targetBranch.Text),
                    _ => query,
                };
            }

            return query;
        }

        protected IQueryable<ModelTestResult> FilterCore(IQueryable<ModelTestResult> query)
        {
            var started = Started ?? StartedDefault;
            query = started.Kind switch
            {
                RelationalKind.GreaterThan => query.Where(x => x.StartTime >= started.DateTime.Date),
                RelationalKind.LessThan => query.Where(x => x.StartTime <= started.DateTime.Date),
                _ => query
            };

            if (DefinitionId is { } definitionId)
            {
                query = query.Where(x => x.DefinitionNumber == definitionId);
            }
            else if (Definition is object)
            {
                query = query.Where(x => x.DefinitionName == Definition);
            }

            if (BuildResult is { } buildResult)
            {
                var r = buildResult.BuildResult;
                query = buildResult.Kind switch
                {
                    EqualsKind.Equals => query.Where(x => x.BuildResult == r),
                    EqualsKind.NotEquals => query.Where(x => x.BuildResult != r),
                    _ => query,
                };
            }

            if (BuildKind is { } buildKind)
            {
                var k = buildKind.BuildKind;
                query = (k, buildKind.Kind) switch
                {
                    (ModelBuildKind.All, _) => query,
                    (_, EqualsKind.Equals) => query.Where(x => x.BuildKind == k),
                    (_, EqualsKind.NotEquals) => query.Where(x => x.BuildKind != k),
                    _ => query,
                };
            }

            if (TargetBranch is { } targetBranch)
            {
                query = targetBranch.Kind switch
                {
                    StringRelationalKind.Contains => query.Where(x => x.GitHubTargetBranch != null && x.GitHubTargetBranch.Contains(targetBranch.Text)),
                    StringRelationalKind.Equals => query.Where(x => x.GitHubTargetBranch == targetBranch.Text),
                    StringRelationalKind.NotEquals => query.Where(x => x.GitHubTargetBranch != targetBranch.Text),
                    _ => query,
                };
            }

            return query;
        }
    }
    #endregion
}
