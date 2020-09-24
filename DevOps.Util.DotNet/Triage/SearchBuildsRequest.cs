using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public class SearchBuildsRequest : ISearchQueryRequest<ModelBuild>
    {
        public string? Definition { get; set; }
        public BuildTypeRequestValue? BuildType { get; set; }
        public string? Repository { get; set; }
        public DateRequestValue? Started { get; set; }
        public DateRequestValue? Finished { get; set; }
        public DateRequestValue? Queued { get; set; }
        public StringRequestValue? TargetBranch { get; set; }

        public bool HasDefinition => !string.IsNullOrEmpty(Definition);

        public int? DefinitionId
        {
            get
            {
                if (!string.IsNullOrEmpty(Definition))
                {
                    if (DotNetUtil.TryGetDefinitionId(Definition, out var _, out var id))
                    {
                        return id;
                    }

                    if (int.TryParse(Definition, out id))
                    {
                        return id;
                    }
                }

                return null;
            }
        }

        public IQueryable<ModelTimelineIssue> Filter(IQueryable<ModelTimelineIssue> query) =>
            Filter(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTimelineIssue, ModelBuild>(x, nameof(ModelTimelineIssue.ModelBuild)));

        public IQueryable<ModelTestResult> Filter(IQueryable<ModelTestResult> query) =>
            Filter(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTestResult, ModelBuild>(x, nameof(ModelTestResult.ModelBuild)));

        public IQueryable<ModelBuildAttempt> Filter(IQueryable<ModelBuildAttempt> query) =>
            Filter(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelBuildAttempt, ModelBuild>(x, nameof(ModelBuildAttempt.ModelBuild)));

        public IQueryable<ModelBuild> Filter(IQueryable<ModelBuild> query) =>
            Filter(query, x => x);

        private IQueryable<T> Filter<T>(
            IQueryable<T> query,
            Func<Expression<Func<ModelBuild, bool>>, Expression<Func<T, bool>>> convertPredicateFunc)
        {
            var definitionId = DefinitionId;
            string? definitionName = definitionId is null
                ? Definition
                : null;
            string? gitHubRepository = string.IsNullOrEmpty(Repository)
                ? null
                : Repository.ToLower();
            string? gitHubOrganization = gitHubRepository is null
                ? null
                : DotNetUtil.GitHubOrganization;

            if (definitionId is object && definitionName is object)
            {
                throw new Exception($"Cannot specify {nameof(definitionId)} and {nameof(definitionName)}");
            }

            if (definitionId is { } d)
            {
                query = query.Where(convertPredicateFunc(x => x.ModelBuildDefinition.DefinitionId == definitionId));
            }
            else if (definitionName is object)
            {
                query = query.Where(convertPredicateFunc(x => x.ModelBuildDefinition.DefinitionName == definitionName));
            }

            if (gitHubOrganization is object)
            {
                query = query.Where(convertPredicateFunc(x => x.GitHubOrganization == gitHubOrganization));
            }

            if (gitHubRepository is object)
            {
                query = query.Where(convertPredicateFunc(x => x.GitHubRepository == gitHubRepository));
            }

            if (Queued is { } queued)
            {
                query = queued.Kind switch
                {
                    RelationalKind.GreaterThan => query.Where(convertPredicateFunc(x => x.QueueTime >= queued.DateTime)),
                    RelationalKind.LessThan => query.Where(convertPredicateFunc(x => x.QueueTime <= queued.DateTime)),
                    _ => query
                };
            }

            if (Started is { } started)
            {
                query = started.Kind switch
                {
                    RelationalKind.GreaterThan => query.Where(convertPredicateFunc(x => x.StartTime >= started.DateTime)),
                    RelationalKind.LessThan => query.Where(convertPredicateFunc(x => x.StartTime <= started.DateTime)),
                    _ => query
                };
            }

            if (Finished is { } finished)
            {
                query = finished.Kind switch
                {
                    RelationalKind.GreaterThan => query.Where(convertPredicateFunc(x => x.FinishTime >= finished.DateTime)),
                    RelationalKind.LessThan => query.Where(convertPredicateFunc(x => x.FinishTime <= finished.DateTime)),
                    _ => query
                };
            }

            if (TargetBranch is { } targetBranch)
            {
                query = targetBranch.Kind switch
                {
                    StringRelationalKind.Contains => query.Where(convertPredicateFunc(x => x.GitHubTargetBranch.Contains(targetBranch.Text))),
                    StringRelationalKind.Equals => query.Where(convertPredicateFunc(x => x.GitHubTargetBranch == targetBranch.Text)),
                    StringRelationalKind.NotEquals => query.Where(convertPredicateFunc(x => x.GitHubTargetBranch != targetBranch.Text)),
                    _ => query,
                };
            }

            if (BuildType is { } buildType)
            {
                query = (buildType.BuildType, buildType.Kind) switch
                {
                    (ModelBuildKind.MergedPullRequest, EqualsKind.Equals) => query.Where(convertPredicateFunc(x => x.IsMergedPullRequest)),
                    (ModelBuildKind.MergedPullRequest, EqualsKind.NotEquals) => query.Where(convertPredicateFunc(x => !x.IsMergedPullRequest)),
                    (ModelBuildKind.PullRequest, EqualsKind.Equals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber.HasValue)),
                    (ModelBuildKind.PullRequest, EqualsKind.NotEquals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber == null || x.IsMergedPullRequest)),
                    (ModelBuildKind.Rolling, EqualsKind.Equals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber == null)),
                    (ModelBuildKind.Rolling, EqualsKind.NotEquals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber != null)),
                    (ModelBuildKind.All, _) => query,
                    _ => query,
                };
            }

            return query;
        }

        public string GetQueryString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Definition))
            {
                Append($"definition:{Definition} ");
            }

            if (!string.IsNullOrEmpty(Repository))
            {
                Append($"repository:{Repository}");
            }

            if (BuildType is { } buildType)
            {
                Append($"kind:{buildType.GetQueryValue(EqualsKind.Equals)}");
            }

            if (Started is { } startTime)
            {
                Append($"started:{startTime.GetQueryValue(RelationalKind.GreaterThan)}");
            }

            if (Finished is { } finishTime)
            {
                Append($"finished:{finishTime.GetQueryValue(RelationalKind.GreaterThan)}");
            }

            if (Queued is { } queued)
            {
                Append($"queued:{queued.GetQueryValue(RelationalKind.GreaterThan)}");
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

        public void ParseQueryString(string userQuery)
        {
            foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(userQuery))
            {
                switch (tuple.Name.ToLower())
                {
                    case "definition":
                        Definition = tuple.Value;
                        break;
                    case "repository":
                        Repository = tuple.Value;
                        break;
                    case "started":
                        Started = DateRequestValue.Parse(tuple.Value.Trim('"'), RelationalKind.GreaterThan);
                        break;
                    case "finished":
                        Finished = DateRequestValue.Parse(tuple.Value.Trim('"'), RelationalKind.GreaterThan);
                        break;
                    case "queued":
                        Queued = DateRequestValue.Parse(tuple.Value.Trim('"'), RelationalKind.GreaterThan);
                        break;
                    case "targetbranch":
                        TargetBranch = StringRequestValue.Parse(tuple.Value, StringRelationalKind.Contains);
                        break;
                    case "kind":
                        BuildType = BuildTypeRequestValue.Parse(tuple.Value, EqualsKind.Equals);
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
