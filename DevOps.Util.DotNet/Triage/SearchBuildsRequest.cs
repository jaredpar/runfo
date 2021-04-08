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
    public class SearchBuildsRequest : SearchRequestBase, ISearchQueryRequest<ModelBuild>
    {
        public string? Repository { get; set; }
        public DateRequestValue? Finished { get; set; }
        public DateRequestValue? Queued { get; set; }
        public bool? HasIssues { get; set; }

        public SearchBuildsRequest()
        {

        }

        public SearchBuildsRequest(string query)
        {
            ParseQueryString(query);
        }

        [Obsolete]
        public IQueryable<ModelTimelineIssue> Filter(IQueryable<ModelTimelineIssue> query) =>
            Filter(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTimelineIssue, ModelBuild>(x, nameof(ModelTimelineIssue.ModelBuild)));

        [Obsolete]
        public IQueryable<ModelTestResult> Filter(IQueryable<ModelTestResult> query) =>
            Filter(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelTestResult, ModelBuild>(x, nameof(ModelTestResult.ModelBuild)));

        [Obsolete]
        public IQueryable<ModelBuildAttempt> Filter(IQueryable<ModelBuildAttempt> query) =>
            Filter(
                query,
                x => PredicateRewriter.ComposeContainerProperty<ModelBuildAttempt, ModelBuild>(x, nameof(ModelBuildAttempt.ModelBuild)));

        public IQueryable<ModelBuild> Filter(IQueryable<ModelBuild> query) =>
            Filter(FilterCore(query), x => x);

        private IQueryable<T> Filter<T>(
            IQueryable<T> query,
            Func<Expression<Func<ModelBuild, bool>>, Expression<Func<T, bool>>> convertPredicateFunc)
        {
            string? gitHubRepository = string.IsNullOrEmpty(Repository)
                ? null
                : Repository.ToLower();
            string? gitHubOrganization = gitHubRepository is null
                ? null
                : DotNetConstants.GitHubOrganization;

            if (Queued is { } queued)
            {
                query = queued.Kind switch
                {
                    RelationalKind.GreaterThan => query.Where(convertPredicateFunc(x => x.QueueTime >= queued.DateTime.Date)),
                    RelationalKind.LessThan => query.Where(convertPredicateFunc(x => x.QueueTime <= queued.DateTime.Date)),
                    _ => query
                };
            }

            if (Finished is { } finished)
            {
                query = finished.Kind switch
                {
                    RelationalKind.GreaterThan => query.Where(convertPredicateFunc(x => x.FinishTime >= finished.DateTime.Date)),
                    RelationalKind.LessThan => query.Where(convertPredicateFunc(x => x.FinishTime <= finished.DateTime.Date)),
                    _ => query
                };
            }

            if (gitHubOrganization is object)
            {
                query = query.Where(convertPredicateFunc(x => x.GitHubOrganization == gitHubOrganization));
            }

            if (gitHubRepository is object)
            {
                query = query.Where(convertPredicateFunc(x => x.GitHubRepository == gitHubRepository));
            }

            if (HasIssues is { } hasIssues)
            {
                if (hasIssues)
                {
                    query = query.Where(convertPredicateFunc(x => x.ModelGitHubIssues.Any()));
                }
                else
                {
                    query = query.Where(convertPredicateFunc(x => !x.ModelGitHubIssues.Any()));
                }
            }

            return query;
        }

        public string GetQueryString()
        {
            var builder = new StringBuilder();
            GetQueryStringCore(builder);

            if (!string.IsNullOrEmpty(Repository))
            {
                Append($"repository:{Repository}");
            }

            if (Finished is { } finishTime)
            {
                Append($"finished:{finishTime.GetQueryValue(RelationalKind.GreaterThan)}");
            }

            if (Queued is { } queued)
            {
                Append($"queued:{queued.GetQueryValue(RelationalKind.GreaterThan)}");
            }

            if (HasIssues is { } hasIssues)
            {
                Append($"issues:{hasIssues.ToString().ToLower()}");
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
                    case "repository":
                        Repository = tuple.Value;
                        break;
                    case "finished":
                        Finished = DateRequestValue.Parse(tuple.Value.Trim('"'), RelationalKind.GreaterThan);
                        break;
                    case "queued":
                        Queued = DateRequestValue.Parse(tuple.Value.Trim('"'), RelationalKind.GreaterThan);
                        break;
                    case "issues":
                        HasIssues = bool.Parse(tuple.Value);
                        break;
                    default:
                        if (!ParseQueryStringTuple(tuple.Name, tuple.Value))
                        {
                            throw new Exception($"Invalid option {tuple.Name}");
                        }
                        break;
                }
            }
        }

        public static bool TryCreate(
            string queryString,
            [NotNullWhen(true)] out SearchBuildsRequest? request,
            [NotNullWhen(false)] out string? errorMessage)
        {
            try
            {
                request = new SearchBuildsRequest();
                request.ParseQueryString(queryString);
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                request = null;
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
