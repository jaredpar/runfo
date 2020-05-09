
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Controllers
{
    public class Example
    {
        public int Id { get; set; }
        public string Project { get; set; }
    }

    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public sealed class HelixController : ControllerBase
    {
        [HttpGet]
        public Example Jobs(int id, string project)
        {
            return new Example()
            {
                Id = id,
                Project = project
            };
        }
    }

}