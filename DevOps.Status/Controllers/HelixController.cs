
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using DevOps.Status.Rest;
using System;

namespace DevOps.Status.Controllers
{
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public sealed class HelixController : ControllerBase
    {
        public DevOpsServer Server { get; }

        public HelixController(DevOpsServer server)
        {
            Server = server;
        }

        [HttpGet]
        [Route("api/helix/jobs/{project}/{buildNumber}")]
        public async Task<List<HelixJobRestInfo>> Jobs(string project, int buildNumber)
        {
            var queryUtil = new DotNetQueryUtil(Server);
            var jobs = await queryUtil.ListHelixJobsAsync(project, buildNumber);
            return jobs
                .Select(x =>
                {
                    return new HelixJobRestInfo()
                    {
                        JobId = x.Value.JobId,
                        TimelineCoreRecordId = x.Record.Id,
                        TimelineCoreRecordName = x.RecordName,
                        TimelineJobRecordId = x.JobRecord?.Id,
                        TimelineJobRecordName = x.JobName
                    };
                })
                .ToList();
        }

        [HttpGet]
        [Route("api/helix/workItems/{project}/{buildNumber}")]
        public async Task<List<HelixWorkItemRestInfo>> FailedWorkItems(string project, int buildNumber, [FromQuery]bool failed = false)
        {
            if (!failed)
            {
                throw new Exception("Not supported");
            }

            var queryUtil = new DotNetQueryUtil(Server);
            var build = await Server.GetBuildAsync(project, buildNumber);
            var workItems = await queryUtil.ListHelixWorkItemsAsync(build, DotNetUtil.FailedTestOutcomes);
            var list = new List<HelixWorkItemRestInfo>();
            foreach (var workItem in workItems)
            {
                var restWorkItem = new HelixWorkItemRestInfo();
                restWorkItem.Job = workItem.JobId;
                restWorkItem.WorkItem = workItem.WorkItemName;

                var logs = new List<HelixLogRestInfo>();
                var logInfo = await HelixUtil.GetHelixLogInfoAsync(Server, workItem);
                foreach (var entry in logInfo.GetUris())
                {
                    logs.Add(new HelixLogRestInfo()
                    {
                        Name = entry.kind.ToString(),
                        Uri = entry.Uri,
                    });
                }

                restWorkItem.Logs = logs.ToArray();
                list.Add(restWorkItem);
            }

            return list;
        }
    }

}