// These are all REST types which are primarily constructed through reflection. 
// Hence this warning is suppressed. As members are discovered that are expected
// to be null they can be annotated here and provide value to the rest of the 
// program
#pragma warning disable 8618

using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using System;

namespace DevOps.Status.Controllers
{
    public sealed partial class RunfoController
    {
        public sealed class BuildStatusRestInfo
        {
            public string Result { get; set; }

            public int BuildNumber { get; set; }

            public string BuildUri { get; set; }
        }

        public sealed class JobStatusInfo
        {
            public string JobName { get; set; }

            public int Total { get; set; }

            public int Passed { get; set; }

            public int Failed { get; set; }

            public double PassRate { get; set; }
        }
    }
}