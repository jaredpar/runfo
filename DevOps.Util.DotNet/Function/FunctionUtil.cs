using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevOps.Util.DotNet.Function
{
    public sealed class FunctionUtil
    {
        public async Task OnPullRequestMergedAsync(
            DevOpsServer server,
            TriageContextUtil triageContextUtil,
            GitHubPullRequestKey prKey,
            string project,
            CancellationToken cancellationToken = default)
        {
            // Pull requests can trigger builds in multiple definitions. Need to calculate the merged PR build
            // for each of them
            var allBuilds = await server.ListPullRequestBuildsAsync(prKey, project).ConfigureAwait(false);
            foreach (var group in allBuilds.GroupBy(x => x.Definition.Id))
            {
                var mergedBuild = group
                    .Where(x => x.Status == BuildStatus.Completed && x.Result != BuildResult.Canceled)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();
                if (mergedBuild is object)
                {
                    var modelBuild = await triageContextUtil.EnsureBuildAsync(mergedBuild.GetBuildResultInfo()).ConfigureAwait(false);
                    modelBuild.IsMergedPullRequest = true;
                    await triageContextUtil.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
