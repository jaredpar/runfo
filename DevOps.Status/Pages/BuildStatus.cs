using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages
{
    public class BuildStatus
    {
        public string? Result { get; set; }

        public int BuildNumber { get; set; }

        public string? BuildUri { get; set; }
    }
}