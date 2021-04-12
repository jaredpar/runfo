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

    public enum ModelBuildKind
    {
        All,
        Rolling,
        PullRequest,
        MergedPullRequest
    }

    /// <summary>
    /// The model representation of <see cref="BuildResult"/>. Using a separate type as the 
    /// DB is using numeric storage and the <see cref="BuildResult"/> type is part of a JSON
    /// API that is string versioned.
    /// </summary>
    public enum ModelBuildResult
    {
        None,
        Canceled,
        Failed,
        PartiallySucceeded,
        Succeeded
    }

    public partial class TriageContext : DbContext
    {
        private void OnModelCreatingQuery(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.DefinitionNumber, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch });

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.DefinitionName, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch });

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => x.StartTime)
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch });

            modelBuilder.Entity<ModelBuild>()
                .Property(x => x.BuildResult)
                .HasConversion<int>();

            modelBuilder.Entity<ModelBuild>()
                .Property(x => x.BuildKind)
                .HasConversion<int>();

            modelBuilder.Entity<ModelBuildAttempt>()
                .HasIndex(x => new { x.DefinitionNumber, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch });

            modelBuilder.Entity<ModelBuildAttempt>()
                .HasIndex(x => new { x.DefinitionName, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch });

            modelBuilder.Entity<ModelBuildAttempt>()
                .HasIndex(x => x.StartTime)
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch });

            modelBuilder.Entity<ModelBuildAttempt>()
                .Property(x => x.BuildResult)
                .HasConversion<int>();

            modelBuilder.Entity<ModelBuildAttempt>()
                .Property(x => x.BuildKind)
                .HasConversion<int>();

            modelBuilder.Entity<ModelTestResult>()
                .HasIndex(x => new { x.DefinitionNumber, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch, x.TestFullName, x.TestRunName, x.IsHelixTestResult });

            modelBuilder.Entity<ModelTestResult>()
                .HasIndex(x => new { x.DefinitionName, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch, x.TestFullName, x.TestRunName, x.IsHelixTestResult });

            modelBuilder.Entity<ModelTestResult>()
                .HasIndex(x => x.StartTime)
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch, x.TestFullName, x.TestRunName, x.IsHelixTestResult });

            modelBuilder.Entity<ModelTestResult>()
                .Property(x => x.BuildResult)
                .HasConversion<int>();

            modelBuilder.Entity<ModelTestResult>()
                .Property(x => x.BuildKind)
                .HasConversion<int>();

            modelBuilder.Entity<ModelTimelineIssue>()
                .HasIndex(x => new { x.DefinitionNumber, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch, x.IssueType, x.JobName, x.TaskName, x.RecordName});

            modelBuilder.Entity<ModelTimelineIssue>()
                .HasIndex(x => new { x.DefinitionName, x.StartTime })
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch, x.IssueType, x.JobName, x.TaskName, x.RecordName});

            modelBuilder.Entity<ModelTimelineIssue>()
                .HasIndex(x => x.StartTime)
                .IncludeProperties(x => new { x.BuildResult, x.BuildKind, x.GitHubTargetBranch, x.IssueType, x.JobName, x.TaskName, x.RecordName});

            modelBuilder.Entity<ModelTimelineIssue>()
                .Property(x => x.BuildResult)
                .HasConversion<int>();

            modelBuilder.Entity<ModelTimelineIssue>()
                .Property(x => x.BuildKind)
                .HasConversion<int>();
        }
    }

    public partial class ModelBuild
    {
        public DateTime StartTime { get; set; }

        public ModelBuildResult BuildResult { get; set; }

        public ModelBuildKind BuildKind { get; set; }

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

        public int DefinitionNumber { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }
    }

    public partial class ModelBuildAttempt
    {
        public DateTime StartTime { get; set; }

        public ModelBuildResult BuildResult { get; set; }

        public ModelBuildKind BuildKind { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionNumber { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }
    }

    public partial class ModelTimelineIssue
    {
        public DateTime StartTime { get; set; }

        public ModelBuildResult BuildResult { get; set; }

        public ModelBuildKind BuildKind { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionNumber { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }
    }

    public partial class ModelTestResult
    {
        public DateTime StartTime { get; set; }

        public ModelBuildResult BuildResult { get; set; }

        public ModelBuildKind BuildKind { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionNumber { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        [Column(TypeName=ModelConstants.GitHubBranchName)]
        public string? GitHubTargetBranch { get; set; }
    }
}