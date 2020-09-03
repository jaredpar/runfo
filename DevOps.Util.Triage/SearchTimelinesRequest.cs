using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public class SearchTimelinesRequest : ISearchRequest
    {
        public string? Text { get; set; }

        public async Task<List<ModelTimelineIssue>> GetResultsAsync(
            TriageContextUtil triageContextUtil,
            IQueryable<ModelBuild> buildQuery,
            bool includeBuild)
        {
            var list = new List<ModelTimelineIssue>();
            await foreach (var issue in EnumerateResultsAsync(triageContextUtil, buildQuery, includeBuild).ConfigureAwait(false))
            {
                list.Add(issue);
            }

            return list;
        }

        public async IAsyncEnumerable<ModelTimelineIssue> EnumerateResultsAsync(
            TriageContextUtil triageContextUtil,
            IQueryable<ModelBuild> buildQuery,
            bool includeBuild)
        {
            IQueryable<ModelTimelineIssue> query = buildQuery
                .SelectMany(x => x.ModelTimelineIssues);

            if (includeBuild)
            {
                query = query.Include(x => x.ModelBuild).ThenInclude(x => x.ModelBuildDefinition);
            }

            Regex? textRegex = null;
            if (Text is object)
            {
                textRegex = new Regex(Text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            var partition = 100;
            var skip = 0;
            do
            {
                var partitionQuery = query.Skip(skip).Take(partition);
                skip += partition;

                var partitionList = await partitionQuery.ToListAsync().ConfigureAwait(false);
                if (partitionList.Count == 0)
                {
                    break;
                }

                foreach (var issue in partitionList)
                {
                    if (textRegex is null || textRegex.IsMatch(issue.Message))
                    {
                        yield return issue;
                    }
                }
            }
            while (true);
        }

        public string GetQueryString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Text))
            {
                Append($"text:\"{Text}\"");
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
            if (!userQuery.Contains(":"))
            {
                Text = userQuery.Trim('"');
                return;
            }

            foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(userQuery))
            {
                switch (tuple.Name.ToLower())
                {
                    case "text":
                        Text = tuple.Value.Trim('"');
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
