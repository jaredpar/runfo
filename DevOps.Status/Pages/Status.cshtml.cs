using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Status.Pages
{
    public class StatusModel : PageModel
    {
        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty(SupportsGet = true)]
        public int TotalCount { get; set; }
        [BindProperty(SupportsGet = true)]
        public int TodayCount { get; set; }
        [BindProperty(SupportsGet = true)]
        public int ExpiredCount { get; set; }

        public StatusModel(TriageContextUtil triageContextUtil)
        {
            TriageContextUtil = triageContextUtil;
        }

        public async Task OnGetAsync()
        {
            var context = TriageContextUtil.Context;
            TotalCount = await context.ModelBuilds.CountAsync();

            var todayLimit = DateTime.UtcNow - TimeSpan.FromDays(1);
            TodayCount = await context.ModelBuilds.Where(x => x.StartTime > todayLimit).CountAsync();
            var expiredLimit = DateTime.UtcNow - TimeSpan.FromDays(30);
            ExpiredCount = await context.ModelBuilds.Where(x => x.StartTime < expiredLimit).CountAsync();
        }
    }
}
