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
    public sealed class RunfoController : ControllerBase
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
            [FromQuery]string query = null)
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
    }
}