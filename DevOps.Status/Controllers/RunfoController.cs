using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;
using System;
using DevOps.Status.Util;
using Microsoft.AspNetCore.Authentication;
using DevOps.Util.Triage;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Status.Controllers
{
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public sealed partial class RunfoController : ControllerBase
    {
        public TriageContextUtil TriageContextUtil { get; }
        public DotNetQueryUtilFactory QueryUtilFactory { get; }

        public RunfoController(DotNetQueryUtilFactory factory, TriageContextUtil triageContextUtil)
        {
            QueryUtilFactory = factory;
            TriageContextUtil = triageContextUtil;
        }

        [HttpGet]
        [Route("api/runfo/builds")]
        public async Task<IActionResult> Builds(
            [FromQuery]string? query = null)
        {
            if (query is object)
            {
                var searchBuildsRequest = new SearchBuildsRequest();
                searchBuildsRequest.ParseQueryString(query);
                var builds = await searchBuildsRequest
                    .FilterBuilds(TriageContextUtil.Context.ModelBuilds)
                    .OrderByDescending(x => x.BuildNumber)
                    .Include(x => x.ModelBuildDefinition)
                    .Take(100)
                    .ToListAsync();
                var list = builds
                    .Select(x =>
                    {
                        var buildInfo = x.GetBuildResultInfo();
                        return new BuildStatusRestInfo()
                        {
                            Result = (x.BuildResult ?? BuildResult.None).ToString(),
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
            var searchBuildsRequest = new SearchBuildsRequest()
            { 
                Definition = definition,
            };
            if (query is object)
            {
                searchBuildsRequest.ParseQueryString(query);
            }

            var builds = await searchBuildsRequest
                .FilterBuilds(TriageContextUtil.Context.ModelBuilds)
                .OrderByDescending(x => x.BuildNumber)
                .Include(x => x.ModelBuildDefinition)
                .Take(100)
                .ToListAsync();

            var queryUtil = await QueryUtilFactory.CreateDotNetQueryUtilForUserAsync();
            var timelines = builds
                .AsParallel()
                .Select(async x => await queryUtil.Server.GetTimelineAsync(x.ModelBuildDefinition.AzureProject, x.BuildNumber));
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