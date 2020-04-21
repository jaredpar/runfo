using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using DevOps.Util;

namespace Model
{
    public static class TriageModelUtil
    {
        public static string GetBuildKeyId(BuildKey key) => GetBuildKeyId(key.Organization, key.Project, key.Id);

        public static string GetBuildKeyId(string organization, string project, int buildNumber) => 
            $"{organization}-{project}-{buildNumber}";

        public static string GetBuildKeyId(Build build) => 
            GetBuildKeyId(DevOpsUtil.GetBuildKey(build));
    }
}