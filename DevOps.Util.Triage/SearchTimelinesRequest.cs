using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public class SearchTimelinesRequest : ISearchRequest
    {
        public const int DefaultLimit = 50;

        public string? Text { get; set; }
        public IssueType? Type { get; set; }

        public IQueryable<ModelTimelineIssue> FilterTimelines(IQueryable<ModelTimelineIssue> query)
        {
            if (Type is { } type)
            {
                query = query.Where(x => x.IssueType == type);
            }

            if (!string.IsNullOrEmpty(Text))
            {
                query = query.Where(x => x.Message.Contains(Text));
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

            if (Type is { } type)
            {
                Append($"type:{type}");
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
                    case "type":
                        Type = tuple.Value.ToLower() switch
                        {
                            "error" => IssueType.Error,
                            "warning" => IssueType.Warning,
                            _ => throw new Exception($"Invalid type {tuple.Value}")
                        };
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
