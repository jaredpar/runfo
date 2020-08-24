using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Pages.Triage
{
    public class NewTriageIssueModel : PageModel
    {
        public TriageContext TriageContext { get; set; }

        [BindProperty]
        public TriageIssueKind TriageIssueKind { get; set; }

        [BindProperty]
        public SearchKind SearchKind { get; set; }

        [BindProperty]
        public string Text { get; set; } = "";

        public NewTriageIssueModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public IActionResult OnPost()
        {
            if (TriageIssueKind == TriageIssueKind.Unknown ||
                SearchKind == SearchKind.Unknown ||
                string.IsNullOrEmpty(Text))
            {
                throw new Exception("Bad request");
            }

            var util = new TriageContextUtil(TriageContext);
            var modelTriageIssue = util.EnsureTriageIssue(TriageIssueKind, SearchKind, Text);
            return RedirectToPage(
                "./Issue",
                new { id = modelTriageIssue.Id });
        }
    }
}
