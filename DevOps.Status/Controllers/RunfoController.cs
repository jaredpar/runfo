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
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Status.Controllers
{
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public sealed partial class RunfoController : ControllerBase
    {
        public TriageContextUtil TriageContextUtil { get; }

        public RunfoController(TriageContextUtil triageContextUtil)
        {
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
                    .Filter(TriageContextUtil.Context.ModelBuilds)
                    .OrderByDescending(x => x.BuildNumber)
                    .Take(100)
                    .ToListAsync();
                var list = builds
                    .Select(x =>
                    {
                        var buildInfo = x.GetBuildResultInfo();
                        return new BuildStatusRestInfo()
                        {
                            Result = x.BuildResult.ToString(),
                            BuildNumber = buildInfo.Number,
                            BuildUri = buildInfo.BuildUri
                        };
                    })
                    .ToList();
                return Ok(list);
            }

            return BadRequest();
        }
    }
}