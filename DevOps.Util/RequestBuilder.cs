using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevOps.Util
{
    internal sealed class RequestBuilder
    {
        internal string Organization { get; }
        internal string Project { get; }

        /// <summary>
        /// The API path inside the query. Example is /builds/build
        /// </summary>
        internal string ApiPath { get; }

        internal string ContinuationToken { get; set; }

        internal string ApiVersion { get; set; } = "5.0";

        /// <summary>
        /// The query parameters for the request excluding the API version and the continuation token.
        /// </summary>
        internal StringBuilder QueryBuilder { get; } = new StringBuilder();

        internal RequestBuilder(string organization, string project, string apiPath)
        {
            Organization = organization;
            Project = project;
            ApiPath = apiPath;
            if (ApiPath?[0] == '/')
            {
                ApiPath = ApiPath.Substring(1);
            }

            if (ApiPath.EndsWith('/'))
            {
                ApiPath = ApiPath.Substring(0, ApiPath.Length - 1);
            }
        }

        internal void AppendList<T>(string name, IEnumerable<T> values)
        {
            if (values is null || !values.Any())
            {
                return;
            }

            QueryBuilder.Append($"{name}=");
            var first = true;
            foreach (var value in values)
            {
                if (!first)
                {
                    QueryBuilder.Append(",");
                }
                QueryBuilder.Append(value);
                first = false;
            }
            QueryBuilder.Append("&");
        }

        internal void AppendString(string name, string value, bool escape = true)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (escape)
                {
                    value = Uri.EscapeDataString(value);
                }

                QueryBuilder.Append($"{name}={value}&");
            }
        }

        internal void AppendUri(string name, Uri uri)
        {
            if (uri is object)
            {
                var data = Uri.EscapeDataString(uri.ToString());
                QueryBuilder.Append($"{name}={data}&");
            }
        }

        internal void AppendInt(string name, int? value)
        {
            if (value.HasValue)
            {
                QueryBuilder.Append($"{name}={value.Value}&");
            }
        }

        internal void AppendBool(string name, bool? value)
        {
            if (value.HasValue)
            {
                QueryBuilder.Append($"{name}={value}&");
            }
        }

        internal void AppendDateTime(string name, DateTimeOffset? value)
        {
            if (value.HasValue)
            {
                QueryBuilder.Append($"{name}=");
                QueryBuilder.Append(value.Value.UtcDateTime.ToString("o"));
                QueryBuilder.Append("&");
            }
        }

        internal void AppendEnum<T>(string name, T? value) where T: struct, Enum
        {
            if (value.HasValue)
            {
                var lowerValue = value.Value.ToString();
                lowerValue = char.ToLower(lowerValue[0]) + lowerValue.Substring(1);
                QueryBuilder.Append($"{name}={lowerValue}&");
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("https://dev.azure.com/");
            if (Project is object)
            {
                builder.Append($"{Organization}/{Project}/_apis/{ApiPath}");
            }
            else
            {
                builder.Append($"{Organization}/_apis/{ApiPath}");
            }

            builder.Append("?");
            if (QueryBuilder.Length > 0)
            {
                builder.Append(QueryBuilder.ToString());
            }

            if (!string.IsNullOrEmpty(ContinuationToken))
            {
                builder.Append($"continuationToken={ContinuationToken}&");
            }

            builder.Append($"api-version={ApiVersion}");
            return builder.ToString();
        }
    }
}
