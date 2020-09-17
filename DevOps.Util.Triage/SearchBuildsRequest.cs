using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public class SearchBuildsRequest : ISearchRequest
    {
        public const ModelBuildKind DefaultKind = ModelBuildKind.All;

        public string? Definition { get; set; }
        public ModelBuildKind Kind { get; set; } = DefaultKind;
        public string? Repository { get; set; }
        public DateRequest? Started { get; set; }
        public DateRequest? Finished { get; set; }
        public StringRequest? TargetBranch { get; set; }

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

        public IQueryable<ModelTimelineIssue> FilterBuilds(IQueryable<ModelTimelineIssue> query) =>
            FilterBuilds(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTimelineIssue, ModelBuild>(x, nameof(ModelTimelineIssue.ModelBuild)));

        public IQueryable<ModelTestResult> FilterBuilds(IQueryable<ModelTestResult> query) =>
            FilterBuilds(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTestResult, ModelBuild>(x, nameof(ModelTestResult.ModelBuild)));

        public IQueryable<ModelBuild> FilterBuilds(IQueryable<ModelBuild> query) =>
            FilterBuilds(query, x => x);

        private IQueryable<T> FilterBuilds<T>(
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

            if (Started is { } started)
            {
                query = started.Kind switch
                {
                    DateRequestKind.GreaterThan => query.Where(convertPredicateFunc(x => x.StartTime >= started.DateTime)),
                    DateRequestKind.LessThan => query.Where(convertPredicateFunc(x => x.StartTime <= started.DateTime)),
                    _ => query
                };
            }

            if (Finished is { } finished)
            {
                query = finished.Kind switch
                {
                    DateRequestKind.GreaterThan => query.Where(convertPredicateFunc(x => x.FinishTime >= finished.DateTime)),
                    DateRequestKind.LessThan => query.Where(convertPredicateFunc(x => x.FinishTime <= finished.DateTime)),
                    _ => query
                };
            }

            if (TargetBranch is { } targetBranch)
            {
                query = targetBranch.Kind switch
                {
                    StringRequestKind.Contains => query.Where(convertPredicateFunc(x => x.GitHubTargetBranch.Contains(targetBranch.Text))),
                    StringRequestKind.Equals => query.Where(convertPredicateFunc(x => x.GitHubTargetBranch == targetBranch.Text)),
                    _ => query,
                };
            }

            switch (Kind)
            {
                case ModelBuildKind.All:
                    // Nothing to filter
                    break;
                case ModelBuildKind.MergedPullRequest:
                    query = query.Where(convertPredicateFunc(x => x.IsMergedPullRequest));
                    break;
                case ModelBuildKind.PullRequest:
                    query = query.Where(convertPredicateFunc(x => x.PullRequestNumber.HasValue));
                    break;
                case ModelBuildKind.Rolling:
                    query = query.Where(convertPredicateFunc(x => x.PullRequestNumber == null));
                    break;
                default:
                    throw new InvalidOperationException($"Invalid kind {Kind}");
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

            if (Kind != DefaultKind)
            {
                var kind = Kind switch
                {
                    ModelBuildKind.All => "all",
                    ModelBuildKind.MergedPullRequest => "mpr",
                    ModelBuildKind.PullRequest => "pr",
                    ModelBuildKind.Rolling => "rolling",
                    _ => throw new InvalidOperationException($"Invalid kind {Kind}"),
                };
                Append($"kind:{kind}");
            }

            if (Started is { } startTime)
            {
                Append($"started:{startTime.GetQueryValue()}");
            }

            if (Finished is { } finishTime)
            {
                Append($"finished:{finishTime.GetQueryValue()}");
            }

            if (TargetBranch is { } targetBranch)
            {
                Append($"targetBranch:{targetBranch.GetQueryValue()}");
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
                        Started = DateRequest.Parse(tuple.Value.Trim('"'), DateRequestKind.GreaterThan);
                        break;
                    case "finished":
                        Finished = DateRequest.Parse(tuple.Value.Trim('"'), DateRequestKind.GreaterThan);
                        break;
                    case "targetbranch":
                        TargetBranch = StringRequest.Parse(tuple.Value, StringRequestKind.Contains);
                        break;
                    case "kind":
                        Kind = tuple.Value.ToLower() switch
                        {
                            "all" => ModelBuildKind.All,
                            "rolling" => ModelBuildKind.Rolling,
                            "pullrequest" => ModelBuildKind.PullRequest,
                            "pr" => ModelBuildKind.PullRequest,
                            "mergedpullrequest" => ModelBuildKind.MergedPullRequest,
                            "mpr" => ModelBuildKind.MergedPullRequest,
                            _ => throw new Exception($"Invalid build kind {tuple.Value}")
                        };
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
