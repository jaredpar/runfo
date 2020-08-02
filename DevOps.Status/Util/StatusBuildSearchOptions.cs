#nullable enable

using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    public class StatusBuildSearchOptions
    {
        public string? Definition { get; set; }

        public int Count { get; set; } = 10;

        public int? DefinitionId
        {
            get
            {
                if (!string.IsNullOrEmpty(Definition))
                {
                    if (DotNetUtil.TryGetDefinitionId(Definition, out var _, out var id))
                    {
                        return id;
                    }

                    if (int.TryParse(Definition, out id))
                    {
                        return id;
                    }
                }

                return null;
            }
        }

        public ModelBuildKind Kind { get; set; } = ModelBuildKind.All;

        public IQueryable<ModelBuild> GetModelBuildsQuery(TriageContext triageContext) =>
            GetModelBuildsQuery(new TriageContextUtil(triageContext));

        public IQueryable<ModelBuild> GetModelBuildsQuery(TriageContextUtil triageContextUtil)
        {
            var definitionId = DefinitionId;
            string? definitionName = definitionId is null
                ? Definition
                : null;
            var count = Count;
            return triageContextUtil.GetModelBuildsQuery(
                definitionId: definitionId,
                definitionName: definitionName,
                count: count,
                kind: Kind);
        }

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
                    case "kind":
                        Kind = tuple.Value.ToLower() switch
                        {
                            "all" => ModelBuildKind.All,
                            "rolling" => ModelBuildKind.Rolling,
                            "pullrequest" => ModelBuildKind.PullRequest,
                            "pr" => ModelBuildKind.PullRequest,
                            "mergedpullrequest" => ModelBuildKind.MergedPullRequest,
                            "mpr" => ModelBuildKind.MergedPullRequest,
                            _ => throw new Exception($"Invalid build kind {tuple.Value}")
                        };
                        break;
                    default:
                        throw new Exception($"Invalid option {tuple.Name}");
                }
            }
        }
    }
}
