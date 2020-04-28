#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;

internal static class Extensions
{
    internal static async Task<BuildTestInfoCollection> ListBuildTestInfosAsync(this DotNetQueryUtil queryUtil, BuildSearchOptionSet optionSet, bool includeAllTests = false)
    {
        TestOutcome[]? outcomes = includeAllTests
            ? null
            : new[] { TestOutcome.Failed, TestOutcome.Aborted };

        var list = new List<BuildTestInfo>();
        foreach (var build in await queryUtil.ListBuildsAsync(optionSet).ConfigureAwait(false))
        {
            try
            {
                var collection = await queryUtil.ListDotNetTestRunsAsync(build, outcomes);
                var buildTestInfo = new BuildTestInfo(build, collection.SelectMany(x => x.TestCaseResults).ToList());
                list.Add(buildTestInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot get test info for {build.Id} {DevOpsUtil.GetBuildUri(build)}");
                Console.WriteLine(ex.Message);
            }
        }

        return new BuildTestInfoCollection(new ReadOnlyCollection<BuildTestInfo>(list));
    }

}