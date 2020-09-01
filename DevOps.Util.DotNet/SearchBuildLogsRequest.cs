using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.Triage
{
    public class SearchBuildLogsRequest : ISearchRequest
    {
        public string? LogName { get; set; } 
        public string? Text { get; set; } 

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
                        LogName = tuple.Value;
                        break;
                    case "text":
                        Text = tuple.Value;
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
