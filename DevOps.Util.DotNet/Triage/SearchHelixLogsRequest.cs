﻿using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Triage
{
    public class SearchHelixLogsRequest : SearchRequestBase, ISearchQueryRequest<ModelTestResult>
    {
        public const int DefaultLimit = 5_000; 

        public List<HelixLogKind> HelixLogKinds { get; set; } = new List<HelixLogKind>();

        /// <summary>
        /// The text in the log to search for.
        /// 
        /// Note: this does not actually filter the builds returned from <see cref="Filter(IQueryable{ModelTestResult})"/> 
        /// but is used in subsequent searches
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// The number of logs to actually search. 
        ///
        /// Note: this does not actually filter the builds returned from <see cref="Filter(IQueryable{ModelTestResult})"/> 
        /// but is used in subsequent searches
        /// </summary>
        public int Limit { get; set; } = DefaultLimit;

        public SearchHelixLogsRequest(string queryString)
        {
            ParseQueryString(queryString);
        }

        public SearchHelixLogsRequest()
        {

        }

        public IQueryable<ModelTestResult> Filter(IQueryable<ModelTestResult> query)
        {
            query = FilterCore(query);
            query = query.Where(x => x.IsHelixTestResult);

            foreach (var kind in HelixLogKinds)
            {
                query = kind switch
                {
                    HelixLogKind.Console => query.Where(x => x.HelixConsoleUri != null),
                    HelixLogKind.RunClient => query.Where(x => x.HelixRunClientUri != null),
                    HelixLogKind.TestResults => query.Where(x => x.HelixTestResultsUri != null),
                    HelixLogKind.CoreDump => query,
                    _ => throw new Exception($"Invalid kind '{kind}'"),
                };
            }

            return query;
        }

        public string GetQueryString()
        {
            var builder = new StringBuilder();
            GetQueryStringCore(builder);

            foreach (var helixLogKind in HelixLogKinds)
            {
                switch (helixLogKind)
                {
                    case HelixLogKind.Console:
                        Append("logKind:console");
                        break;
                    case HelixLogKind.RunClient:
                        Append("logKind:runclient");
                        break;
                    case HelixLogKind.TestResults:
                        Append("logKind:testresults");
                        break;
                }
            }

            if (!string.IsNullOrEmpty(Text))
            {
                Append($"text:\"{Text}\"");
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
                    case "logkind":
                        switch (tuple.Value)
                        {
                            case "console":
                                MaybeAdd(HelixLogKind.Console);
                                break;
                            case "runclient":
                                MaybeAdd(HelixLogKind.RunClient);
                                break;
                            case "testresults":
                                MaybeAdd(HelixLogKind.TestResults);
                                break;
                        }
                        break;
                    case "text":
                        Text = tuple.Value.Trim('"');
                        break;
                    case "limit":
                        Limit = int.Parse(tuple.Value);
                        break;
                    default:
                        if (!ParseQueryStringTuple(tuple.Name, tuple.Value))
                        {
                            throw new Exception($"Invalid option {tuple.Name}");
                        }
                        break;
                }
            }

            void MaybeAdd(HelixLogKind kind)
            {
                if (!HelixLogKinds.Contains(kind))
                {
                    HelixLogKinds.Add(kind);
                }
            }
        }

        public static bool TryCreate(
            string queryString,
            [NotNullWhen(true)] out SearchHelixLogsRequest? request,
            [NotNullWhen(false)] out string? errorMessage)
        {
            try
            {
                request = new SearchHelixLogsRequest();
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
