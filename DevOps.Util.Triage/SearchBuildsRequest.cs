using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public class SearchBuildsRequest : ISearchRequest
    {
        public const int DefaultCount = 10;
        public const ModelBuildKind DefaultKind = ModelBuildKind.All;

        public string? Definition { get; set; }

        [Obsolete("Can't use this anymore")]
        public int Count { get; set; } = DefaultCount;
        public ModelBuildKind Kind { get; set; } = DefaultKind;
        public string? Repository { get; set; }

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

        public IQueryable<ModelBuild> GetQuery(TriageContext triageContext) =>
            GetQuery(new TriageContextUtil(triageContext));

        public IQueryable<ModelBuild> GetQuery(
            TriageContextUtil triageContextUtil,
            Func<IQueryable<ModelBuild>, IQueryable<ModelBuild>>? beforeCountFunc = null)
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
            var query = triageContextUtil.GetModelBuildsQuery(
                definitionId: definitionId,
                definitionName: definitionName,
                gitHubOrganization: gitHubOrganization,
                gitHubRepository: gitHubRepository,
                count: null,
                kind: Kind);

            if (beforeCountFunc is object)
            {
                query = beforeCountFunc(query);
            }

            return query.Take(Count);
        }

        public IQueryable<ModelTimelineIssue> FilterBuilds(IQueryable<ModelTimelineIssue> query) =>
            FilterBuilds(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTimelineIssue, ModelBuild>(x, nameof(ModelTimelineIssue.ModelBuild)));

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

            if (Count != DefaultCount)
            {
                Append($"count:{Count}");
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
                    case "count":
                        Count = int.Parse(tuple.Value);
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
