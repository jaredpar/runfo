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
        public string? Definition { get; set; }
        public BuildTypeRequest? BuildType { get; set; }
        public string? Repository { get; set; }
        public DateRequest? Started { get; set; }
        public DateRequest? Finished { get; set; }
        public DateRequest? Queued { get; set; }
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

            if (Queued is { } queued)
            {
                query = queued.Kind switch
                {
                    DateRequestKind.GreaterThan => query.Where(convertPredicateFunc(x => x.QueueTime >= queued.DateTime)),
                    DateRequestKind.LessThan => query.Where(convertPredicateFunc(x => x.QueueTime <= queued.DateTime)),
                    _ => query
                };
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

            if (BuildType is { } buildType)
            {
                query = (buildType.BuildType, buildType.Kind) switch
                {
                    (ModelBuildKind.MergedPullRequest, BuildTypeRequestKind.Equals) => query.Where(convertPredicateFunc(x => x.IsMergedPullRequest)),
                    (ModelBuildKind.MergedPullRequest, BuildTypeRequestKind.NotEquals) => query.Where(convertPredicateFunc(x => !x.IsMergedPullRequest)),
                    (ModelBuildKind.PullRequest, BuildTypeRequestKind.Equals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber.HasValue)),
                    (ModelBuildKind.PullRequest, BuildTypeRequestKind.NotEquals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber == null || x.IsMergedPullRequest)),
                    (ModelBuildKind.Rolling, BuildTypeRequestKind.Equals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber == null)),
                    (ModelBuildKind.Rolling, BuildTypeRequestKind.NotEquals) => query.Where(convertPredicateFunc(x => x.PullRequestNumber != null)),
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
                Append($"kind:{buildType.GetQueryValue(BuildTypeRequestKind.Equals)}");
            }

            if (Started is { } startTime)
            {
                Append($"started:{startTime.GetQueryValue(DateRequestKind.GreaterThan)}");
            }

            if (Finished is { } finishTime)
            {
                Append($"finished:{finishTime.GetQueryValue(DateRequestKind.GreaterThan)}");
            }

            if (Queued is { } queued)
            {
                Append($"queued:{queued.GetQueryValue(DateRequestKind.GreaterThan)}");
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
                    case "queued":
                        Queued = DateRequest.Parse(tuple.Value.Trim('"'), DateRequestKind.GreaterThan);
                        break;
                    case "targetbranch":
                        TargetBranch = StringRequest.Parse(tuple.Value, StringRequestKind.Contains);
                        break;
                    case "kind":
                        BuildType = BuildTypeRequest.Parse(tuple.Value, BuildTypeRequestKind.Equals);
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
