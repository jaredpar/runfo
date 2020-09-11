using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Pages.Tracking
{
    public class NewTrackingIssueModel : PageModel
    {
        public TriageContext TriageContext { get; set; }

        [BindProperty]
        public TrackingKind TrackingKind { get; set; }

        [BindProperty]
        public string Text { get; set; } = "";

        public NewTrackingIssueModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task<IActionResult> OnPost()
        {
            if (TrackingKind == TrackingKind.Unknown ||
                string.IsNullOrEmpty(Text))
            {
                throw new Exception("Bad request");
            }

            var modelTrackingIssue = new ModelTrackingIssue()
            {
                IsActive = true,
                TrackingKind = TrackingKind,
                SearchRegexText = Text,
            };
            TriageContext.ModelTrackingIssues.Add(modelTrackingIssue);
            await TriageContext.SaveChangesAsync();
            return RedirectToPage(
                "./Issue",
                new { id = modelTrackingIssue.Id });
        }
    }
}
