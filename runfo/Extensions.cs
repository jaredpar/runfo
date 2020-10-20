using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.DotNet;
using static Runfo.OptionSetUtil;

namespace Runfo
{
    internal static class Extensions
    {
        internal static async Task<List<Build>> ListBuildsAsync(this DotNetQueryUtil queryUtil, BuildSearchOptionSet optionSet)
        {
            if (optionSet.BuildIds.Count > 0 && optionSet.Definitions.Count > 0)
            {
                OptionFailure("Cannot specify builds and definitions", optionSet);
                throw CreateBadOptionException();
            }

            var searchCount = optionSet.SearchCount ?? BuildSearchOptionSet.DefaultSearchCount;
            var repository = optionSet.Repository;
            var branch = optionSet.Branch;
            var before = optionSet.Before;
            var after = optionSet.After;

            if (branch is object && !branch.StartsWith("refs"))
            {
                branch = $"refs/heads/{branch}";
            }

            var builds = new List<Build>();
            if (optionSet.BuildIds.Count > 0)
            {
                if (optionSet.Repository is object)
                {
                    OptionFailure("Cannot specify builds and repository", optionSet);
                    throw CreateBadOptionException();
                }

                if (optionSet.Branch is object)
                {
                    OptionFailure("Cannot specify builds and branch", optionSet);
                    throw CreateBadOptionException();
                }

                if (optionSet.SearchCount is object)
                {
                    OptionFailure("Cannot specify builds and count", optionSet);
                    throw CreateBadOptionException();
                }

                foreach (var buildInfo in optionSet.BuildIds)
                {
                    if (!TryGetBuildId(optionSet, buildInfo, out var buildProject, out var buildId))
                    {
                        OptionFailure($"Cannot convert {buildInfo} to build id", optionSet);
                        throw CreateBadOptionException();
                    }

                    var build = await queryUtil.Server.GetBuildAsync(buildProject, buildId).ConfigureAwait(false);
                    builds.Add(build);
                }
            }
            else
            {
                var (project, definitions) = GetProjectAndDefinitions();
                var collection = await queryUtil.ListBuildsAsync(
                    searchCount,
                    project,
                    definitions: definitions,
                    repositoryId: repository,
                    branch: branch,
                    includePullRequests: optionSet.IncludePullRequests,
                    before: optionSet.Before,
                    after: optionSet.After);
                builds.AddRange(collection);
            }

            // Exclude out the builds that are complicating results
            foreach (var excludedBuildId in optionSet.ExcludedBuildIds)
            {
                builds = builds.Where(x => x.Id != excludedBuildId).ToList();
            }

            return builds;

            (string Project, int[] Definitions) GetProjectAndDefinitions()
            {
                if (optionSet.Definitions.Count == 0)
                {
                    return (optionSet.Project ?? DotNetUtil.DefaultAzureProject, Array.Empty<int>());
                }

                string? project = null;
                var list = new List<int>();
                foreach (var definition in optionSet.Definitions)
                {
                    if (!DotNetUtil.TryGetDefinitionId(definition, out var definitionProject, out var definitionId))
                    {
                        OptionFailureDefinition(definition, optionSet);
                        throw CreateBadOptionException();
                    }

                    if (definitionProject is object)
                    {
                        if (project is null)
                        {
                            project = definitionProject;
                        }
                        else if (!StringComparer.OrdinalIgnoreCase.Equals(definitionProject, project))
                        {
                            throw new InvalidOperationException($"Conflicting project names {project} and {definitionProject}");
                        }
                    }

                    list.Add(definitionId);
                }

                project ??= DotNetUtil.DefaultAzureProject;
                return (project, list.ToArray());
            }

            static bool TryGetBuildId(BuildSearchOptionSet optionSet, string build, out string project, out int buildId)
            {
                var defaultProject = optionSet.Project ?? DotNetUtil.DefaultAzureProject;
                return DotNetQueryUtil.TryGetBuildId(build, defaultProject, out project!, out buildId);
            }
        }

        internal static async Task<BuildTestInfoCollection> ListBuildTestInfosAsync(this DotNetQueryUtil queryUtil, BuildSearchOptionSet optionSet, bool includeAllTests = false)
        {
            TestOutcome[] outcomes = includeAllTests
                ? Array.Empty<TestOutcome>()
                : DotNetUtil.FailedTestOutcomes;

            var list = new List<BuildTestInfo>();
            foreach (var build in await queryUtil.ListBuildsAsync(optionSet).ConfigureAwait(false))
            {
                try
                {
                    var collection = await queryUtil.ListDotNetTestRunsAsync(build, includeSubResults: true, outcomes);
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
}