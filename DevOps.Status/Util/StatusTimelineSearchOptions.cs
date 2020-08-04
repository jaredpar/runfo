#nullable enable

using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    public class StatusTimelineSearchOptions
    {
        public string? Value { get; set; }

        public string GetUserQueryString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(Value))
            {
                Append($"value:\"{Value}\"");
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

        public void Parse(string userQuery)
        {
            if (!userQuery.Contains(":"))
            {
                Value = userQuery.Trim('"');
                return;
            }

            foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(userQuery))
            {
                switch (tuple.Name.ToLower())
                {
                    case "value":
                        Value = tuple.Value.Trim('"');
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
