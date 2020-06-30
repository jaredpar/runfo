
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Rest
{
    public sealed class BuildStatusRestInfo
    {
        public string Result { get; set; }

        public int BuildNumber { get; set; }

        public string BuildUri { get; set; }
    }

    public sealed class JobInfo
    {
        
    }
}
