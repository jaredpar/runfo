using DevOps.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet
{
    public readonly struct BranchName
    {
        public string FullName { get; }
        public string ShortName { get; }
        public bool IsPullRequest { get; }

        private BranchName(string fullName, string shortName, bool isPullRequest)
        {
            FullName = fullName;
            ShortName = shortName;
            IsPullRequest = isPullRequest;
        }

        public static bool TryParse(string fullName, out BranchName branchName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                branchName = default;
                return false;
            }

            if (fullName[0] == '/')
            {
                fullName = fullName.Substring(1);
            }

            var normalPrefix = "refs/heads/";
            var prPrefix = "refs/pull/";
            string shortName;
            bool isPullRequest;
            if (fullName.StartsWith(normalPrefix, StringComparison.OrdinalIgnoreCase))
            {
                shortName = fullName.Substring(normalPrefix.Length);
                isPullRequest = false;
            }
            else if (fullName.StartsWith(prPrefix, StringComparison.OrdinalIgnoreCase))
            {
                shortName = fullName.Split(new[] { '/' })[2];
                isPullRequest = true;
            }
            else
            {
                shortName = fullName;
                isPullRequest = false;
            }

            branchName = new BranchName(fullName, shortName, isPullRequest);
            return true;
        }

        public static BranchName Parse(string fullName)
        {
            if (!TryParse(fullName, out var branchName))
            {
                throw new Exception($"Invalid branch full name {fullName}");
            }

            return branchName;
        }

        public override string ToString() => FullName;
    }
}