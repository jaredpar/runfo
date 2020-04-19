using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using DevOps.Util;

namespace Model
{
    public static class RuntimeInfoModelUtil
    {
        public static string GetTriageBuildKey(BuildKey key) => GetTriageBuildKey(key.Organization, key.Project, key.Id);
        public static string GetTriageBuildKey(string organization, string project, int buildNumber) => 
            $"{organization}-{project}-{buildNumber}";

        public static string GetTriageBuildKey(Build build) => 
            GetTriageBuildKey(DevOpsUtil.GetBuildKey(build));
    }
}