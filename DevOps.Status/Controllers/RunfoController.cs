#nullable enable

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
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public sealed partial class RunfoController : ControllerBase
    {
        public DevOpsServer Server { get; }

        public DotNetQueryUtil QueryUtil { get; }

        public RunfoController(DevOpsServer server)
        {
            Server = server;
            QueryUtil = new DotNetQueryUtil(server);
        }

        [HttpGet]
        [Route("api/runfo/builds")]
        public async Task<IActionResult> Builds(
            [FromQuery]string? query = null)
        {
            if (query is object)
            {
                var builds = await QueryUtil.ListBuildsAsync(query);
                var list = builds
                    .Select(x =>
                    {
                        var buildInfo = x.GetBuildInfo();
                        return new BuildStatusRestInfo()
                        {
                            Result = x.Result.ToString(),
                            BuildNumber = buildInfo.Number,
                            BuildUri = buildInfo.BuildUri
                        };
                    })
                    .ToList();
                return Ok(list);
            }

            return BadRequest();
        }

        [HttpGet]
        [Route("api/runfo/jobs/{definition}")]
        public async Task<IActionResult> Builds(
            string definition,
            [FromQuery]string? query = null)
        {
            query = $"-d {definition} {query}";
            var builds = await QueryUtil.ListBuildsAsync(query);
            var timelines = builds
                .AsParallel()
                .Select(async x => await Server.GetTimelineAsync(x.Project.Name, x.Id));
            var trees = new List<TimelineTree>();
            foreach (var task in timelines)
            {
                try
                {
                    var timeline = await task;
                    if (timeline is null)
                    {
                        continue;
                    }

                    trees.Add(TimelineTree.Create(timeline));
                }
                catch
                {

                }
            }

            var jobs = new List<JobStatusInfo>();
            foreach (var group in trees.SelectMany(x => x.JobNodes).GroupBy(x => x.Name))
            {
                var passed = group.Where(x => x.TimelineRecord.IsAnySuccess()).Count();
                var failed = group.Count() - passed;
                var total = passed + failed;
                jobs.Add(new JobStatusInfo()
                {
                    JobName = group.Key,
                    Passed = passed,
                    Failed = failed,
                    PassRate = (double)passed / (total),
                    Total = total
                });
            }

            return Ok(jobs);
        }
    }
}