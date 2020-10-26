using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace DevOps.Util.DotNet.Triage
{
    public sealed class SearchTrackingIssuesRequest : ISearchQueryRequest<ModelTrackingIssue>
    {
        public string? Definition { get; set; }

        public IQueryable<ModelTrackingIssue> Filter(IQueryable<ModelTrackingIssue> query)
        {
            if (!string.IsNullOrEmpty(Definition))
            {
                query = query.Where(x => x.ModelBuildDefinition.DefinitionName == Definition);
            }

            return query;
        }

        public string GetQueryString()
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(Definition))
            {
                Append($"definition:{Definition}");
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
                    case "definition":
                        Definition = tuple.Value;
                        break;
                    default:
                        throw new Exception("Invalid option: '{tuple.Name}'");
                }
            }
        }
    }
}
