using DevOps.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace DevOps.Util
{
    public static class DevOpsUtil
    {
        public static Uri GetBuildDefinitionUri(string organization, string project, int definitionId)
        {
            var uri = $"https://{organization}.visualstudio.com/{project}/_build?definitionId={definitionId}";
            return new Uri(uri);
        }
        public static string GetOrganization(Build build)
        {
            // TODO: this will fail when the URI is in the dev.azure.com form. Should fix.
            var uri = new Uri(build.Url);
            return uri.PathAndQuery.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[0];
        }

        /// <summary>
        /// Get a human readable build URI for the build
        /// </summary>
        public static Uri GetBuildUri(Build build)
        {
            var organization = GetOrganization(build);
            return GetBuildUri(organization, build.Project.Name, build.Id);
        }

        /// <summary>
        /// Get a human readable build URI for the build
        /// </summary>
        public static Uri GetBuildUri(string organization, string project, int buildId)
        {
            var uri = $"https://dev.azure.com/{organization}/{project}/_build/results?buildId={buildId}";
            return new Uri(uri);
        }

        public static string GetRepositoryName(Build build)
        {
            var both = build.Repository.Id.Split(new[] { '/' });
            return both[1];
        }


        public static string GetRepositoryOrganization(Build build)
        {
            var both = build.Repository.Id.Split(new[] { '/' });
            return both[0];
        }

        public static int? GetPullRequestNumber(Build build)
        {
            if (build.Reason != BuildReason.PullRequest)
            {
                return null;
            }

            try
            {
                var items = build.SourceBranch.Split('/');
                if (int.TryParse(items[2], out int number))
                {
                    return number;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
