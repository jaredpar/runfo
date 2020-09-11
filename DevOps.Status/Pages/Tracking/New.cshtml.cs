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
        public TriageContext TriageContext { get; }

        public TriageContextUtil TriageContextUtil { get; }

        [BindProperty]
        public TrackingKind TrackingKind { get; set; }

        [BindProperty]
        public string? Text { get; set; }

        [BindProperty]
        public string? Definition { get; set; }

        public string? ErrorMessage { get; set; }

        public NewTrackingIssueModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
            TriageContextUtil = new TriageContextUtil(triageContext);
        }

        public async Task<IActionResult> OnPost()
        {
            if (TrackingKind == TrackingKind.Unknown)
            {
                ErrorMessage = "Invalid Tracking Kind";
                return Page();
            }

            if (string.IsNullOrEmpty(Text))
            {
                ErrorMessage = "Must provide search text";
                return Page();
            }

            ModelBuildDefinition? modelBuildDefinition = null;
            if (!string.IsNullOrEmpty(Definition))
            {
                modelBuildDefinition = await TriageContextUtil.FindModelBuildDefinitionAsync(Definition);
                if (modelBuildDefinition is null)
                {
                    ErrorMessage = $"Cannot find build definition with name or ID: {Definition}";
                    return Page();
                }
            }

            var modelTrackingIssue = new ModelTrackingIssue()
            {
                IsActive = true,
                TrackingKind = TrackingKind,
                SearchRegexText = Text,
                ModelBuildDefinition = modelBuildDefinition,
            };
            TriageContext.ModelTrackingIssues.Add(modelTrackingIssue);
            await TriageContext.SaveChangesAsync();
            return RedirectToPage(
                "./Issue",
                new { id = modelTrackingIssue.Id });
        }
    }
}
