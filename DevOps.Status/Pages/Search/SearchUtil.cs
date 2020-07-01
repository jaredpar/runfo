#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Web;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Pages.Search
{
    /// <summary>
    /// This type is meant to be used with Razor model binding and represent all of the 
    /// permutations that queries can have.
    /// </summary>
    public class SearchInfo
    {
        internal const int DefaultCount = 5;

        internal static SearchInfo Default { get; } = new SearchInfo()
        {
            Definition = "runtime",
            Count = DefaultCount
        };

        [BindProperty(SupportsGet = true, Name = "definition")]
        public string? Definition { get; set; }

        [BindProperty(SupportsGet = true, Name = "count")]
        public int? Count { get; set; }

        [BindProperty(SupportsGet = true, Name = "testName")]
        public string? TestName { get; set; }

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? QueryString { get; set; }

        public string GetSearchText()
        {
            var builder = new StringBuilder();

            var count = Count ?? DefaultCount;
            builder.Append($"count:{count} ");

            if (!string.IsNullOrEmpty(Definition))
            {
                builder.Append($"definition:{Definition} ");
            }

            if (!string.IsNullOrEmpty(TestName))
            {
                builder.Append($"testName:{SearchUtil.NormalizeQuotes(TestName)}");
            }

            return builder.ToString();
        }

        public void ParseQueryString()
        {
            if (string.IsNullOrEmpty(QueryString))
            {
                return;
            }

            foreach (var token in DotNetQueryUtil.TokenizeQuery(QueryString))
            {
                var both = token.Split(':', count: 2);
                var name = both[0].ToLower();
                var value = both.Length > 1 ? both[1] : "";

                switch (name)
                {
                    case "definition": 
                        Definition = value;
                        break;
                    case "count":
                        Count = int.Parse(value);
                        break;
                    case "testname":
                        TestName = value.Trim('"');
                        break;
                    default:
                        throw new Exception($"Invalid query string item {name}");
                }
            }
        }

        public string CreatePrettyQueryString()
        {
            var builder = new StringBuilder();

            if (Count != DefaultCount)
            {
                MaybeAddItem();
                builder.Append($"count={Count}");
            }

            if (!string.IsNullOrEmpty(Definition))
            {
                MaybeAddItem();
                builder.Append($"definition={HttpUtility.UrlEncode(Definition)}");
            }

            if (!string.IsNullOrEmpty(TestName))
            {
                MaybeAddItem();
                builder.Append($"testName={TestName}");
            }

            return builder.ToString();

            void MaybeAddItem()
            {
                if (builder.Length == 0)
                {
                    builder.Append("?");
                }
                else
                {
                    builder.Append("&");
                }
            }
        }

        public BuildSearchOptionSet CreateBuildSearchOptionSet()
        {
            var optionSet = new BuildSearchOptionSet()
            {
                SearchCount = Count ?? DefaultCount,
            };

            if (!string.IsNullOrEmpty(Definition))
            {
                optionSet.Definitions.Add(Definition);
            }

            return optionSet;
        }
    }

    internal static class SearchUtil
    {
        internal static bool ContainsWhiteSpace(string s) => s.Any(Char.IsWhiteSpace);

        internal static string NormalizeQuotes(string s)
        {
            if (ContainsWhiteSpace(s))
            {
                if (s[0] == '"' && s[^1] == '"')
                {
                    return s;
                }

                return $"\"{s}\"";
            }
            else
            {
                return s.Trim('"');
            }
        }
    }
}