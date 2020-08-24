using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DevOps.Status.Pages.View
{
    public class BuildModel : PageModel
    {
        public sealed class TimelineData
        {
            public int Attempt { get; set; }
            public string? JobName { get; set;  }
            public string? Line { get; set;  }
        }

        public sealed class TestData
        {
            public string? TestFullName { get; set; }
            public string? TestRunName { get; set; }
        }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true)]
        public int? Number { get; set; }

        public int Attempts { get; set; }

        public List<TimelineData> TimelineDataList { get; } = new List<TimelineData>();

        public List<TestData> TestDataList { get; } = new List<TestData>();

        public BuildModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGet()
        {
            if (!(Number is { } number))
            {
                return;
            }

            await PopulateTimeline();
            await PopulateTests();

            async Task PopulateTimeline()
            {
                var query = TriageContextUtil
                    .Context
                    .ModelTimelineIssues
                    .Where(x =>
                        x.ModelBuild.BuildNumber == number &&
                        x.ModelBuild.ModelBuildDefinition.AzureOrganization == DotNetUtil.AzureOrganization &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == DotNetUtil.DefaultAzureProject);
                Attempts = 1;
                foreach (var modelTimeline in (await query.ToListAsync()).OrderBy(x => x.Attempt))
                {
                    TimelineDataList.Add(new TimelineData()
                    {
                        Attempt = modelTimeline.Attempt,
                        JobName = modelTimeline.JobName,
                        Line = modelTimeline.Message,
                    });

                    if (modelTimeline.Attempt > Attempts)
                    {
                        Attempts = modelTimeline.Attempt;
                    }
                }
            }

            async Task PopulateTests()
            {
                var query = TriageContextUtil
                    .Context
                    .ModelTestResults
                    .Where(x =>
                        x.ModelBuild.BuildNumber == number &&
                        x.ModelBuild.ModelBuildDefinition.AzureOrganization == DotNetUtil.AzureOrganization &&
                        x.ModelBuild.ModelBuildDefinition.AzureProject == DotNetUtil.DefaultAzureProject)
                    .Select(x => new TestData()
                    {
                        TestFullName = x.TestFullName,
                        TestRunName = x.ModelTestRun.Name,
                    });
                TestDataList.AddRange(await query.ToListAsync());
            }
        }
    }
}
