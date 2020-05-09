
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Controllers
{
    public class HelixJobRestInfo
    {
        public string JobId { get; set; }

        public string TimelineCoreRecordName { get; set; }

        public string TimelineCoreRecordId { get; set; }

        public string TimelineJobRecordName { get; set; }

        public string TimelineJobRecordId { get; set; }
    }

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
        [Route("helix/jobs/{project}/{buildNumber}")]
        public async Task<List<HelixJobRestInfo>> Jobs(string project, int buildNumber)
        {
            var queryUtil = new DotNetQueryUtil(Server);
            var jobs = await queryUtil.ListHelixJobs(project, buildNumber);
            return jobs
                .Select(x =>
                {
                    return new HelixJobRestInfo()
                    {
                        JobId = x.Value,
                        TimelineCoreRecordId = x.Record.Id,
                        TimelineCoreRecordName = x.RecordName,
                        TimelineJobRecordId = x.JobRecord?.Id,
                        TimelineJobRecordName = x.JobName
                    };
                })
                .ToList();
        }
    }

}