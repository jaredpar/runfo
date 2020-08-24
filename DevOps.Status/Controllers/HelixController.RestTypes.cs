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
    public sealed partial class HelixController
    {
        public class HelixJobRestInfo
        {
            public string JobId { get; set; }

            public string TimelineCoreRecordName { get; set; }

            public string TimelineCoreRecordId { get; set; }

            public string? TimelineJobRecordName { get; set; }

            public string? TimelineJobRecordId { get; set; }
        }

        public class HelixWorkItemRestInfo
        {
            public string Job { get; set; }

            public string WorkItem { get; set; }

            public HelixLogRestInfo[] Logs { get; set; }
        }

        public class HelixLogRestInfo
        {
            public string Name { get; set; }
            
            public string Uri { get; set; }
        }
    }
}