using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Rest
{
    public class HelixJobRestInfo
    {
        public string JobId { get; set; }

        public string TimelineCoreRecordName { get; set; }

        public string TimelineCoreRecordId { get; set; }

        public string TimelineJobRecordName { get; set; }

        public string TimelineJobRecordId { get; set; }
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