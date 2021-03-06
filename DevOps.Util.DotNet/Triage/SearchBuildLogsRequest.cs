﻿using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public class SearchBuildLogsRequest : ISearchRequest
    {
        public const int DefaultLimit = 100;

        public string? LogName { get; set; } 
        public string? Text { get; set; }
        public int Limit { get; set; } = DefaultLimit;

        public string GetQueryString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(LogName))
            {
                Append($"logName:{LogName} ");
            }

            if (!string.IsNullOrEmpty(Text))
            {
                Append($"text:{Text}");
            }

            if (Limit != DefaultLimit)
            {
                Append($"limit:{Limit}");
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
                    case "logname":
                        LogName = tuple.Value.Trim('"');
                        break;
                    case "text":
                        Text = tuple.Value.Trim('"');
                        break;
                    case "limit":
                        Limit = int.Parse(tuple.Value);
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }

        public static bool TryCreate(
            string queryString,
            [NotNullWhen(true)] out SearchBuildLogsRequest? request,
            [NotNullWhen(false)] out string? errorMessage)
        {
            try
            {
                request = new SearchBuildLogsRequest();
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
