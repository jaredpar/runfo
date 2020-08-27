using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public class SearchTimelinesRequest
    {
        public string? Text { get; set; }

        public IQueryable<ModelTimelineIssue> GetQuery(
            TriageContextUtil triageContextUtil,
            IQueryable<ModelBuild> buildQuery)
        {
            IQueryable<ModelTimelineIssue> query = buildQuery
                .Join(
                    triageContextUtil.Context.ModelTimelineIssues,
                    b => b.Id,
                    t => t.ModelBuildId,
                    (b, t) => t);

            if (Text is object)
            {
                var text = Text.Replace('*', '%');
                text = '%' + text.Trim('%') + '%';
                query = query.Where(t => EF.Functions.Like(t.Message, text));
            }

            return query;
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
