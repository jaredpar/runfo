using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public class SearchTimelinesRequest : SearchRequestBase, ISearchQueryRequest<ModelTimelineIssue>
    {
        public const int DefaultLimit = 50;

        public string? Message { get; set; }
        public string? JobName { get; set; }
        public string? DisplayName { get; set; }
        public string? TaskName { get; set; }
        public ModelIssueType? Type { get; set; }

        public SearchTimelinesRequest()
        {

        }

        public SearchTimelinesRequest(string query)
        {
            ParseQueryString(query);
        }

        public IQueryable<ModelTimelineIssue> Filter(IQueryable<ModelTimelineIssue> query)
        {
            query = FilterCore(query);

            if (Type is { } type)
            {
                query = query.Where(x => x.IssueType == type);
            }

            if (!string.IsNullOrEmpty(JobName))
            {
                query = query.Where(x => x.JobName.Contains(JobName));
            }

            if (!string.IsNullOrEmpty(TaskName))
            {
                query = query.Where(x => x.TaskName.Contains(TaskName));
            }

            if (!string.IsNullOrEmpty(DisplayName))
            {
                query = query.Where(x => x.RecordName.Contains(DisplayName));
            }

            // Keep this in sync with logic in SearchTestsRequest
            if (!string.IsNullOrEmpty(Message))
            {
                var c = Message[0];
                query = c switch
                {
                    '#' => query = query.Where(x => x.Message.Contains(Message.Substring(1))),
                    '*' => GetFullText(query, Message.Substring(1)),
                    _ => GetFullText(query, Message)
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
            GetQueryStringCore(builder);

            if (!string.IsNullOrEmpty(Message))
            {
                Append($"message:\"{Message}\"");
            }

            if (!string.IsNullOrEmpty(JobName))
            {
                Append($"jobName:\"{JobName}\"");
            }

            if (!string.IsNullOrEmpty(DisplayName))
            {
                Append($"displayName:\"{DisplayName}\"");
            }

            if (!string.IsNullOrEmpty(TaskName))
            {
                Append($"taskName:\"{TaskName}\"");
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
                Message = userQuery.Trim('"');
                return;
            }

            foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(userQuery))
            {
                switch (tuple.Name.ToLower())
                {
                    case "text":
                    case "message":
                        Message = tuple.Value.Trim('"');
                        break;
                    case "jobname":
                        JobName = tuple.Value.Trim('"');
                        break;
                    case "displayname":
                        DisplayName = tuple.Value.Trim('"');
                        break;
                    case "taskname":
                        TaskName = tuple.Value.Trim('"');
                        break;
                    case "type":
                        Type = tuple.Value.ToLower() switch
                        {
                            "error" => ModelIssueType.Error,
                            "warning" => ModelIssueType.Warning,
                            _ => throw new Exception($"Invalid type {tuple.Value}")
                        };
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
