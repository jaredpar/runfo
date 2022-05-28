
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using System;
using DevOps.Status.Util;

namespace DevOps.Status.Controllers
{
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public sealed partial class HelixController : ControllerBase
    {
        public DotNetQueryUtilFactory QueryUtilFactory { get; }

        public HelixController(DotNetQueryUtilFactory factory)
        {
            QueryUtilFactory = factory;
        }

        [HttpGet]
        [Route("api/helix/jobs/{project}/{buildNumber}")]
        public async Task<List<HelixJobRestInfo>> Jobs(string project, int buildNumber)
        {
            var queryUtil = await QueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var jobs = await queryUtil.ListHelixJobsAsync(project, buildNumber);
            return jobs
                .Select(x =>
                {
                    return new HelixJobRestInfo()
                    {
                        JobId = x.HelixJob.JobId,
                        TimelineCoreRecordId = x.Record.Record.Id,
                        TimelineCoreRecordName = x.Record.RecordName,
                        TimelineJobRecordId = x.Record.JobRecord?.Id,
                        TimelineJobRecordName = x.Record.JobName
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

            var queryUtil = await QueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var helixApi = QueryUtilFactory.CreateHelixServerForAnonymous().HelixApi;
            var build = await queryUtil.Server.GetBuildAsync(project, buildNumber);
            var helixInfos = await queryUtil.ListHelixWorkItemsAsync(build, DevOpsUtil.FailedTestOutcomes);
            var list = new List<HelixWorkItemRestInfo>();
            foreach (var helixInfo in helixInfos)
            {
                var restWorkItem = new HelixWorkItemRestInfo();
                restWorkItem.Job = helixInfo.JobId;
                restWorkItem.WorkItem = helixInfo.WorkItemName;

                var logs = new List<HelixLogRestInfo>();
                var logInfo = await HelixUtil.GetHelixLogInfoAsync(helixApi, helixInfo);
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