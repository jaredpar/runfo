using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using DevOps.Util.DotNet.Triage.Migrations;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public class SearchTimelinesRequest : ISearchQueryRequest<ModelTimelineIssue>
    {
        public const int DefaultLimit = 50;

        public string? Text { get; set; }
        public string? JobName { get; set; }
        public IssueType? Type { get; set; }

        public IQueryable<ModelTimelineIssue> Filter(IQueryable<ModelTimelineIssue> query)
        {
            if (Type is { } type)
            {
                query = query.Where(x => x.IssueType == type);
            }

            if (!string.IsNullOrEmpty(JobName))
            {
                query = query.Where(x => x.JobName.Contains(JobName));
            }

            if (!string.IsNullOrEmpty(Text))
            {
                var c = Text[0];
                query = c switch
                {
                    '#' => query = query.Where(x => x.Message.Contains(Text.Substring(1))),
                    '*' => GetFullText(query, Text.Substring(1)),
                    _ => GetFullText(query, Text)
                };

                static IQueryable<ModelTimelineIssue> GetFullText(IQueryable<ModelTimelineIssue> query, string text)
                {
                    if (text.Contains(' '))
                    {
                        text = '"' + text + '"';
                    }

                    return query.Where(x => EF.Functions.Contains(x.Message, text));
                }
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

            if (!string.IsNullOrEmpty(JobName))
            {
                Append($"jobName:\"{JobName}\"");
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
                    case "jobname":
                        JobName = tuple.Value.Trim('"');
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

        public static bool TryCreate(
            string queryString,
            [NotNullWhen(true)] out SearchTimelinesRequest? request,
            [NotNullWhen(false)] out string? errorMessage)
        {
            try
            {
                request = new SearchTimelinesRequest();
                request.ParseQueryString(queryString);
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                request = null;
                return false;
            }
        }
    }
}
