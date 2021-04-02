using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable 8618

namespace DevOps.Util.DotNet.Triage
{
    // There are query elements that are the same between a number of entity items and this 
    // file helps keep them all in sync between the different entity types

    public partial class ModelBuild
    {
        public DateTime StartTime { get; set; }

        public BuildResult BuildResult { get; set; }

        public BuildKind BuildKind { get; set; }

        /// <summary>
        /// This represents the target branch of the Build. For most builds this is the branch that was being built, 
        /// for pull requests this is the branch the code will be merged into. 
        /// 
        /// It is possible for this to be null. There are some types of builds for which there is not a logical target
        /// branch
        /// </summary>
        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionId { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }
    }

    public partial class ModelBuildAttempt
    {
        public DateTime StartTime { get; set; }

        public BuildResult BuildResult { get; set; }

        public BuildKind BuildKind { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionId { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }
    }

    public partial class ModelTimelineIssue
    {
        public DateTime StartTime { get; set; }

        public BuildResult BuildResult { get; set; }

        public BuildKind BuildKind { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionId { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }
    }

    public partial class ModelTestResult
    {
        public DateTime StartTime { get; set; }

        public BuildResult BuildResult { get; set; }

        public BuildKind BuildKind { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionId { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }
    }
}