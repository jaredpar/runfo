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
    public class SearchTestsRequest : ISearchRequest
    {
        public string? Name { get; set; }
        public string? JobName { get; set; }

        public IQueryable<ModelTestResult> FilterTestResults(IQueryable<ModelTestResult> query)
        {
            if (!string.IsNullOrEmpty(JobName))
            {
                query = query.Where(x => x.ModelTestRun.Name.Contains(JobName));
            }

            if (!string.IsNullOrEmpty(Name))
            {
                query = query.Where(x => x.TestFullName.Contains(Name));
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
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
