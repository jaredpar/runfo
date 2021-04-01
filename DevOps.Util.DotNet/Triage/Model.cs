using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable 8618

namespace DevOps.Util.DotNet.Triage
{
    public class TriageContext : DbContext
    {
        public DbSet<ModelBuild> ModelBuilds { get; set; }

        public DbSet<ModelBuildAttempt> ModelBuildAttempts { get; set; }

        public DbSet<ModelBuildDefinition> ModelBuildDefinitions { get; set; }

        public DbSet<ModelOsxDeprovisionRetry> ModelOsxDeprovisionRetry { get; set; }

        public DbSet<ModelTimelineIssue> ModelTimelineIssues { get; set; }

        public DbSet<ModelTestRun> ModelTestRuns { get; set; }

        public DbSet<ModelTestResult> ModelTestResults { get; set; }

        public DbSet<ModelTrackingIssue> ModelTrackingIssues { get; set; }

        public DbSet<ModelTrackingIssueMatch> ModelTrackingIssueMatches { get; set; }

        public DbSet<ModelTrackingIssueResult> ModelTrackingIssueResults { get; set; }

        public DbSet<ModelGitHubIssue> ModelGitHubIssues { get; set; }

        public TriageContext(DbContextOptions<TriageContext> options)
            : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelBuild>()
                .Property(x => x.IsMergedPullRequest)
                .HasDefaultValue(false);

            modelBuilder.Entity<ModelBuild>()
                .Property(x => x.BuildResult)
                .HasConversion<string>();

            modelBuilder.Entity<ModelBuild>()
                .Property(x => x.DefinitionName)
                .HasDefaultValue("");

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => x.StartTime);

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => x.DefinitionId);

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => x.DefinitionName);

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => x.BuildResult);

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.StartTime, x.DefinitionId })
                .IncludeProperties(x => new { x.BuildNumber, x.BuildResult, x.PullRequestNumber, x.GitHubRepository });

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.DefinitionId, x.StartTime })
                .IncludeProperties(x => new { x.BuildNumber, x.BuildResult, x.PullRequestNumber, x.GitHubRepository });

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.StartTime, x.DefinitionName })
                .IncludeProperties(x => new { x.BuildNumber, x.BuildResult, x.PullRequestNumber, x.GitHubRepository });

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.DefinitionName, x.StartTime })
                .IncludeProperties(x => new { x.BuildNumber, x.BuildResult, x.PullRequestNumber, x.GitHubRepository });

            modelBuilder.Entity<ModelBuild>()
                .HasIndex(x => new { x.DefinitionId, x.PullRequestNumber, x.StartTime })
                .IncludeProperties(x => new { x.BuildNumber, x.BuildResult, x.GitHubRepository });

            modelBuilder.Entity<ModelBuildAttempt>()
                .HasIndex(x => new { x.Attempt, x.ModelBuildId })
                .IsUnique();

            modelBuilder.Entity<ModelBuildAttempt>()
                .HasOne(x => x.ModelBuild)
                .WithMany(x => x.ModelBuildAttempts)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelBuildDefinition>()
                .HasIndex(x => new { x.AzureOrganization, x.AzureProject, x.DefinitionId })
                .IsUnique();

            modelBuilder.Entity<ModelTestRun>()
                .HasIndex(x => new { x.AzureOrganization, x.AzureProject, x.TestRunId })
                .IsUnique();

            modelBuilder.Entity<ModelTestRun>()
                .HasIndex(x => x.ModelBuildId)
                .IncludeProperties(x => new { x.AzureOrganization, x.AzureProject, x.TestRunId, x.Name });

            modelBuilder.Entity<ModelTestRun>()
                .Property(x => x.Attempt)
                .HasDefaultValue(1);

            modelBuilder.Entity<ModelTestRun>()
                .HasOne(x => x.ModelBuild)
                .WithMany()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelTestResult>()
                .HasIndex(x => x.ModelBuildId)
                .IncludeProperties(x => new { x.TestFullName, x.TestRunName, x.IsHelixTestResult });

            modelBuilder.Entity<ModelTestResult>()
                .HasOne(x => x.ModelBuild)
                .WithMany(x => x.ModelTestResults)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelTestResult>()
                .HasOne(x => x.ModelTestRun)
                .WithMany()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ModelTimelineIssue>()
                .Property(x => x.IssueType)
                .HasConversion<string>()
                .HasDefaultValue(IssueType.Warning);

            modelBuilder.Entity<ModelTimelineIssue>()
                .HasIndex(x => x.ModelBuildId)
                .IncludeProperties(x => new { x.JobName, x.TaskName, x.RecordName, x.IssueType, x.Attempt, x.Message });

            modelBuilder.Entity<ModelTimelineIssue>()
                .HasIndex(x => new { x.ModelBuildId, x.Attempt });

            modelBuilder.Entity<ModelTimelineIssue>()
                .HasOne(x => x.ModelBuild)
                .WithMany(x => x.ModelTimelineIssues)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelTrackingIssue>()
                .Property(x => x.TrackingKind)
                .HasConversion<string>();

            modelBuilder.Entity<ModelTrackingIssueMatch>()
                .Property(x => x.HelixLogKind)
                .HasConversion<string>();

            modelBuilder.Entity<ModelTrackingIssueMatch>()
                .HasIndex(x => x.ModelTrackingIssueId);

            modelBuilder.Entity<ModelTrackingIssueMatch>()
                .HasOne(x => x.ModelBuildAttempt)
                .WithMany(x => x.ModelTrackingIssueMatches)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelTrackingIssueResult>()
                .HasIndex(x => new { x.ModelTrackingIssueId, x.ModelBuildAttemptId })
                .IsUnique();

            modelBuilder.Entity<ModelTrackingIssueResult>()
                .HasOne(x => x.ModelBuildAttempt)
                .WithMany(x => x.ModelTrackingIssueResults)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelGitHubIssue>()
                .HasIndex(x => new { x.Organization, x.Repository, x.Number, x.ModelBuildId })
                .IsUnique();

            modelBuilder.Entity<ModelGitHubIssue>()
                .HasIndex(x => new { x.Number, x.Organization, x.Repository });

            modelBuilder.Entity<ModelGitHubIssue>()
                .HasOne(x => x.ModelBuild)
                .WithMany(x => x.ModelGitHubIssues)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModelOsxDeprovisionRetry>()
                .HasOne(x => x.ModelBuild)
                .WithMany()
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public static class ModelConstants
    {
        public const string ModelBuildNameKeyTypeName = "nvarchar(100)";
        public const string BuildDefinitionNameTypeName = "nvarchar(100)";
        public const string GitHubOrganizationTypeName = "nvarchar(100)";
        public const string GitHubRepositoryTypeName = "nvarchar(100)";
        public const string AzureOrganizationTypeName = "nvarchar(100)";
        public const string AzureProjectTypeName = "nvarchar(100)";
        public const string JobNameTypeName = "nvarchar(200)";
    }

    public class ModelBuildDefinition
    {
        public int Id { get; set; }

        [Column(TypeName=ModelConstants.AzureOrganizationTypeName)]
        [Required]
        public string AzureOrganization { get; set; }

        [Column(TypeName=ModelConstants.AzureProjectTypeName)]
        [Required]
        public string AzureProject { get; set; }

        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        public int DefinitionId { get; set; }
    }

    public class ModelBuild
    {
        public int Id { get; set; }

        /// <summary>
        /// This is is a unique key that is generated by combining the organization name, project name
        /// and build number into a single string. 
        /// </summary>
        [Column(TypeName=ModelConstants.ModelBuildNameKeyTypeName)]
        [Required]
        public string NameKey { get; set; }

        public int BuildNumber { get; set; }

        [Column(TypeName=ModelConstants.AzureOrganizationTypeName)]
        [Required]
        public string AzureOrganization { get; set; }

        [Column(TypeName=ModelConstants.AzureOrganizationTypeName)]
        [Required]
        public string AzureProject { get; set; }

        [Column(TypeName=ModelConstants.GitHubOrganizationTypeName)]
        [Required]
        public string GitHubOrganization { get; set; }

        [Column(TypeName=ModelConstants.GitHubRepositoryTypeName)]
        [Required]
        public string GitHubRepository { get; set; }

        public int? PullRequestNumber { get; set; }

        /// <summary>
        /// This represents the target branch of the Build. For most builds this is the branch that was being built, 
        /// for pull requests this is the branch the code will be merged into. 
        /// 
        /// It is possible for this to be null. There are some types of builds for which there is not a logical target
        /// branch
        /// </summary>
        [Column(TypeName="nvarchar(100)")]
        public string? GitHubTargetBranch { get; set; }

        public bool IsMergedPullRequest { get; set; }

        /// <summary>
        /// The queue time of the build stored in UTC
        /// </summary>
        public DateTime? QueueTime { get; set; }

        /// <summary>
        /// The result of the most recent build attempt
        /// </summary>
        public BuildResult? BuildResult { get; set; }

        /// <summary>
        /// The start time of the build stored in UTC
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The finish time of the build stored in UTC
        /// </summary>
        public DateTime? FinishTime { get; set; }

        /// <summary>
        /// De-normalized <see cref="ModelBuildDefinition.DefinitionName"/>
        /// </summary>
        [Column(TypeName=ModelConstants.BuildDefinitionNameTypeName)]
        [Required]
        public string DefinitionName { get; set; }

        /// <summary>
        /// De-normalized <see cref="ModelBuildDefinition.DefinitionId"/>
        /// </summary>
        public int DefinitionId { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        public List<ModelTestResult> ModelTestResults { get; set; }

        public List<ModelTimelineIssue> ModelTimelineIssues { get; set; }

        public List<ModelBuildAttempt> ModelBuildAttempts { get; set; }

        public List<ModelGitHubIssue> ModelGitHubIssues { get; set; }
    }

    public class ModelOsxDeprovisionRetry
    {
        public int Id { get; set; }

        public int OsxJobFailedCount { get; set; }

        public int JobFailedCount { get; set; }

        public int ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public class ModelBuildAttempt
    {
        public int Id { get; set; }

        public int Attempt { get; set; }

        public bool IsTimelineMissing { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? FinishTime { get; set; }

        public BuildResult BuildResult { get; set; }

        [Column(TypeName=ModelConstants.ModelBuildNameKeyTypeName)]
        [Required]
        public string NameKey { get; set; }

        public int ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }

        public List<ModelTrackingIssueMatch> ModelTrackingIssueMatches { get; set; }

        public List<ModelTrackingIssueResult> ModelTrackingIssueResults { get; set; }
    }

    public class ModelTimelineIssue
    {
        public int Id { get; set; }

        public int Attempt { get; set; }

        [Column(TypeName = "nvarchar(200)")]
        [Required]
        public string JobName { get; set; }

        [Column(TypeName = "nvarchar(200)")]
        [Required]
        public string RecordName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        [Required]
        public string TaskName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? RecordId { get; set; }

        [Required]
        public string Message { get; set; }

        [Column(TypeName = "nvarchar(12)")]
        public IssueType IssueType { get; set; }

        public int ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public class ModelTestRun
    {
        public int Id { get; set; }

        [Column(TypeName=ModelConstants.AzureOrganizationTypeName)]
        public string? AzureOrganization { get; set; }

        [Column(TypeName=ModelConstants.AzureOrganizationTypeName)]
        public string? AzureProject { get; set; }

        public int TestRunId { get; set; }

        public int Attempt { get; set; }

        [Column(TypeName = "nvarchar(500)")]
        [Required]
        public string Name { get; set; }

        public int ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public class ModelTestResult
    {
        public int Id { get; set; }

        [Required]
        public string TestFullName { get; set; }

        /// <summary>
        /// <see cref="ModelTestRun.Name"/>
        /// </summary>
        [Required]
        public string TestRunName { get; set; }

        [Required]
        public string Outcome { get; set; }

        /// <summary>
        /// This is true when the value modeled here is a <see cref="TestSubResult"/> entry. For xUnit
        /// this represents iterations of a Theory entry. 
        /// </summary>
        public bool IsSubResult { get; set; }

        /// <summary>
        /// This is true when a <see cref="TestCaseResult"/> contains <see cref="TestSubResult"/> entries.
        /// This generally means it's a xUnit Theory and the actual result here is probably a bit meaningless
        /// because it's just a container. The actual data for the failure is in the <see cref="TestSubResult"/>
        /// entries.
        /// </summary>
        public bool IsSubResultContainer { get; set; }

        public bool IsHelixTestResult { get; set; }

        public string? HelixConsoleUri { get; set; }

        public string? HelixRunClientUri { get; set; }

        public string? HelixCoreDumpUri { get; set; }

        public string? HelixTestResultsUri { get; set; }

        [Required]
        public string ErrorMessage { get; set; }

        public int ModelTestRunId { get; set; }

        public ModelTestRun ModelTestRun { get; set; }

        public int ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public enum TrackingKind
    {
        Unknown = 0,

        Test,

        Timeline,

        HelixLogs,
    }

    /// <summary>
    /// This represents an infrastructure issue that is being tracked by the system
    /// </summary>
    public class ModelTrackingIssue
    {
        [NotMapped]
        public const int IssueTitleLengthLimit = 100;

        public int Id { get; set; }

        [Column(TypeName = "nvarchar(30)")]
        public TrackingKind TrackingKind { get; set; }

        [Required]
        public string SearchQuery { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        [Required]
        public string IssueTitle { get; set; }

        public bool IsActive { get; set; }

        /// <summary>
        /// GitHub organization the tracking issue exists in 
        /// </summary>
        [Column(TypeName=ModelConstants.GitHubOrganizationTypeName)]
        [Required]
        public string GitHubOrganization { get; set; }

        /// <summary>
        /// GitHub repository the tracking issue exists in
        /// </summary>
        [Column(TypeName=ModelConstants.GitHubRepositoryTypeName)]
        [Required]
        public string GitHubRepository { get; set; }

        /// <summary>
        /// GitHub issue number for the tracking issue
        /// </summary>
        public int? GitHubIssueNumber { get; set; }

        public int? ModelBuildDefinitionId { get; set; }

        /// <summary>
        /// When defined restrict the test failure tracking to the following build definitions
        /// </summary>
        public ModelBuildDefinition? ModelBuildDefinition { get; set; }

        public List<ModelTrackingIssueMatch> ModelTrackingIssueMatches { get; set; }
    }

    /// <summary>
    /// Represents a match of a <see cref="ModelTrackingIssue"/> for a given <see cref="ModelBuildAttempt"/>.
    /// There can be many matches for a single build
    /// </summary>
    public class ModelTrackingIssueMatch
    {
        public int Id { get; set; }

        [Required]
        public string JobName { get; set; }

        public int ModelTrackingIssueId { get; set; }

        public ModelTrackingIssue ModelTrackingIssue { get; set; }

        public int ModelBuildAttemptId { get; set; }

        public ModelBuildAttempt ModelBuildAttempt { get; set; }

        public int? ModelTestResultId { get; set; }

        public ModelTestResult? ModelTestResult { get; set; }

        public int? ModelTimelineIssueId { get; set; }

        public ModelTimelineIssue? ModelTimelineIssue { get; set; }

        public HelixLogKind HelixLogKind { get; set; }

        public string? HelixLogUri { get; set; }
    }

    /// <summary>
    /// Represents when a <see cref="ModelTrackingIssue"/> has been completely evaluated over a specific 
    /// <see cref="ModelBuildAttempt"/>
    /// </summary>
    public class ModelTrackingIssueResult
    {
        public int Id { get; set; }

        /// <summary>
        /// Whether or not the linked <see cref="ModelTrackingIssue"/> had a match in this <see cref="ModelBuildAttempt"/>
        /// </summary>
        public bool IsPresent { get; set; }

        public int ModelTrackingIssueId { get; set; }

        public ModelTrackingIssue ModelTrackingIssue { get; set; }

        public int ModelBuildAttemptId { get; set; }

        public ModelBuildAttempt ModelBuildAttempt { get; set; }
    }

    public class ModelGitHubIssue
    {
        public int Id { get; set; }

        [Column(TypeName=ModelConstants.GitHubOrganizationTypeName)]
        [Required]
        public string Organization { get; set; }

        [Column(TypeName=ModelConstants.GitHubRepositoryTypeName)]
        [Required]
        public string Repository { get; set; }

        public int Number { get; set; }

        public int ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }
}