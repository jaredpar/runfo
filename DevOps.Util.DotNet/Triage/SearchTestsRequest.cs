﻿using DevOps.Util.DotNet;
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
    public class SearchTestsRequest : ISearchQueryRequest<ModelTestResult>
    {
        public string? Name { get; set; }
        public string? JobName { get; set; }
        public string? Message { get; set; }

        public IQueryable<ModelTestResult> Filter(IQueryable<ModelTestResult> query)
        {
            if (!string.IsNullOrEmpty(JobName))
            {
                query = query.Where(x => x.JobName.Contains(JobName));
            }

            if (!string.IsNullOrEmpty(Name))
            {
                query = query.Where(x => x.TestFullName.Contains(Name));
            }

            // Keep this in sync with logic in SearchTimelineRequest
            if (!string.IsNullOrEmpty(Message))
            {
                var c = Message[0];
                query = c switch
                {
                    '#' => query = query.Where(x => x.ErrorMessage.Contains(Message.Substring(1))),
                    '*' => GetFullText(query, Message.Substring(1)),
                    _ => GetFullText(query, Message)
                };

                static IQueryable<ModelTestResult> GetFullText(IQueryable<ModelTestResult> query, string text)
                {
                    if (text.Contains(' '))
                    {
                        text = '"' + text + '"';
                    }

                    return query.Where(x => EF.Functions.Contains(x.ErrorMessage, text));
                }
            }

            return query;
        }

        public string GetQueryString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Name))
            {
                Append($"name:\"{Name}\"");
            }

            if (!string.IsNullOrEmpty(JobName))
            {
                Append($"jobName:\"{JobName}\"");
            }

            if (!string.IsNullOrEmpty(Message))
            {
                Append($"message:\"{Message}\"");
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
                Name = userQuery.Trim('"');
                return;
            }

            foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(userQuery))
            {
                switch (tuple.Name.ToLower())
                {
                    case "name":
                        Name = tuple.Value.Trim('"');
                        break;
                    case "jobname":
                        JobName = tuple.Value.Trim('"');
                        break;
                    case "message":
                        Message = tuple.Value.Trim('"');
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }

        public static bool TryCreate(
            string queryString,
            [NotNullWhen(true)] out SearchTestsRequest? request,
            [NotNullWhen(false)] out string? errorMessage)
        {
            try
            {
                request = new SearchTestsRequest();
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
