
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DevOps.Status.Pages.Search
{
    public class BuildsModel : PageModel
    {
        public sealed class BuildSearchOptions
        {
            public string Definition { get; set; }

            public int Count { get; set; }

            public void Parse(string userQuery)
            {
                foreach (var tuple in DotNetQueryUtil.TokenizeQueryPairs(userQuery))
                {
                    switch (tuple.Name.ToLower())
                    {
                        case "definition":
                            Definition = tuple.Value;
                            break;
                        case "count":
                            Count = int.Parse(tuple.Value);
                            break;
                        default:
                            throw new Exception($"Invalid option {tuple.Name}");
                    }
                }
            }
        }

        public class BuildData
        {
            public string Result { get; set; }

            public int BuildNumber { get; set; }

            public string BuildUri { get; set; }
        }

        public TriageContext TriageContext { get; }

        [BindProperty(SupportsGet = true)]
        public string Query { get; set; }

        public List<BuildData> Builds { get; set; } = new List<BuildData>();

        public BuildsModel(TriageContext triageContext)
        {
            TriageContext = triageContext;
        }

        public async Task OnGet()
        {
            if (string.IsNullOrEmpty(Query))
            {
                Query = "definition:runtime count:10";
            }

            var options = new BuildSearchOptions();
            options.Parse(Query);

            IQueryable<ModelBuild> query = TriageContext
                .ModelBuilds
                .Include(x => x.ModelBuildDefinition)
                .OrderByDescending(x => x.BuildNumber);

            if (!string.IsNullOrEmpty(options.Definition))
            {
                var definitionId = DotNetUtil.GetDefinitionIdFromFriendlyName(options.Definition);
                query = query.Where(x => x.ModelBuildDefinition.DefinitionId == definitionId);
            }

            var count = options.Count < 5 ? 5 : options.Count;
            if (count > 500)
            {
                throw new Exception("Count too high");
            }
            query = query.Take(count);

            Builds = (await query.ToListAsync())
                .Select(x =>
                {
                    var buildInfo = TriageContextUtil.GetBuildInfo(x);
                    return new BuildData()
                    {
                        Result = x.BuildResult.ToString(),
                        BuildNumber = buildInfo.Number,
                        BuildUri = buildInfo.BuildUri
                    };
                })
                .ToList();
        }
    }
}