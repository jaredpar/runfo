#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DevOps.Util.DotNet;
using Microsoft.EntityFrameworkCore;

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

            modelBuilder.Entity<ModelBuildAttempt>()
                .HasIndex(x => new { x.Attempt, x.ModelBuildId })
                .IsUnique();

            modelBuilder.Entity<ModelBuildDefinition>()
                .HasIndex(x => new { x.AzureOrganization, x.AzureProject, x.DefinitionId })
                .IsUnique();

            modelBuilder.Entity<ModelTestRun>()
                .HasIndex(x => new { x.AzureOrganization, x.AzureProject, x.TestRunId })
                .IsUnique();

            modelBuilder.Entity<ModelTestRun>()
                .Property(x => x.Attempt)
                .HasDefaultValue(1);

            modelBuilder.Entity<ModelTimelineIssue>()
                .Property(x => x.IssueType)
                .HasConversion<string>()
                .HasDefaultValue(IssueType.Warning);

            modelBuilder.Entity<ModelTrackingIssue>()
                .Property(x => x.TrackingKind)
                .HasConversion<string>();

            modelBuilder.Entity<ModelTrackingIssueResult>()
                .HasIndex(x => new { x.ModelTrackingIssueId, x.ModelBuildAttemptId })
                .IsUnique();
        }
    }

    public class ModelBuildDefinition
    {
        public int Id { get; set; }

        public string AzureOrganization { get; set; }

        public string AzureProject { get; set; }

        public string DefinitionName { get; set; }

        public int DefinitionId { get; set; }
    }

    public class ModelBuild
    {
        [Column(TypeName="nvarchar(100)")]
        public string Id { get; set; }

        public int BuildNumber { get; set; }

        public string AzureOrganization { get; set; }

        public string AzureProject { get; set; }

        public string GitHubOrganization { get; set; }

        public string GitHubRepository { get; set; }

        public int? PullRequestNumber { get; set; }

        /// <summary>
        /// This represents the target branch of the Build. For most builds this is the branch that was being built, 
        /// for pull requests this is the branch the code will be merged into. 
        /// 
        /// It is possible for this to be null. There are some types of builds for which there is not a logical target
        /// branch
        /// </summary>
        public string GitHubTargetBranch { get; set; }

        public bool IsMergedPullRequest { get; set; }

        /// <summary>
        /// The queue time of the build stored in UTC
        /// </summary>
        [Column(TypeName="smalldatetime")]
        public DateTime? QueueTime { get; set; }

        /// <summary>
        /// The result of the most recent build attempt
        /// </summary>
        public BuildResult? BuildResult { get; set; }

        /// <summary>
        /// The start time of the build stored in UTC
        /// </summary>
        [Column(TypeName="smalldatetime")]
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The finish time of the build stored in UTC
        /// </summary>
        [Column(TypeName="smalldatetime")]
        public DateTime? FinishTime { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        public List<ModelTestResult> ModelTestResults { get; set; }

        public List<ModelTimelineIssue> ModelTimelineIssues { get; set; }

        public List<ModelBuildAttempt> ModelBuildAttempts { get; set; }
    }

    public class ModelOsxDeprovisionRetry
    {
        public int Id { get; set; }

        public int OsxJobFailedCount { get; set; }

        public int JobFailedCount { get; set; }

        [Column(TypeName="nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public class ModelBuildAttempt
    {
        public int Id { get; set; }

        public int Attempt { get; set; }

        public bool IsTimelineMissing { get; set; }

        [Column(TypeName="smalldatetime")]
        public DateTime? StartTime { get; set; }

        [Column(TypeName="smalldatetime")]
        public DateTime? FinishTime { get; set; }

        public BuildResult BuildResult { get; set; }

        [Column(TypeName="nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public class ModelTimelineIssue
    {
        public int Id { get; set; }

        public int Attempt { get; set; }

        public string JobName { get; set; }

        public string RecordName { get; set; }

        public string RecordId { get; set; }

        public string Message { get; set; }

        [Column(TypeName = "nvarchar(12)")]
        public IssueType IssueType { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }

        public int ModelBuildAttemptId { get; set; }

        public ModelBuildAttempt ModelBuildAttempt { get; set; } 
    }

    public class ModelTestRun
    {
        public int Id { get; set; }

        public string AzureOrganization { get; set; }

        public string AzureProject { get; set; }

        public int TestRunId { get; set; }

        public int Attempt { get; set; }

        public string Name { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public class ModelTestResult
    {
        public int Id { get; set; }

        public string TestFullName { get; set; }

        public string Outcome { get; set; }

        public bool IsHelixTestResult { get; set; }

        public string HelixConsoleUri { get; set; }

        public string HelixRunClientUri { get; set; }

        public string HelixCoreDumpUri { get; set; }

        public string HelixTestResultsUri { get; set; }

        public int ModelTestRunId { get; set; }

        public ModelTestRun ModelTestRun { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    public enum TrackingKind
    {
        Unknown = 0,

        Test,

        Timeline,

        HelixConsole,

        HelixRunClient,
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

        /// <summary>
        /// This is a terrible property name, it's not a regex. Need to rename this to 
        /// <see cref="SearchQuery"/>
        /// </summary>
        [Required]
        [Obsolete("Use SearchQuery instead")]
        public string SearchRegexText { get; set; }

#pragma warning disable 618
        [NotMapped]
        public string SearchQuery
        {
            get => SearchRegexText;
            set => SearchRegexText = value; 
        }
#pragma warning restore 618

        [Column(TypeName = "nvarchar(100)")]
        public string IssueTitle { get; set; }

        public bool IsActive { get; set; }

        /// <summary>
        /// GitHub organization the tracking issue exists in 
        /// </summary>
        public string GitHubOrganization { get; set; }

        /// <summary>
        /// GitHub repository the tracking issue exists in
        /// </summary>
        public string GitHubRepository { get; set; }

        /// <summary>
        /// GitHub issue number for the tracking issue
        /// </summary>
        public int? GitHubIssueNumber { get; set; }

        public int? ModelBuildDefinitionId { get; set; }

        /// <summary>
        /// When defined restrict the test failure tracking to the following build definitions
        /// </summary>
        public ModelBuildDefinition ModelBuildDefinition { get; set; }

        public List<ModelTrackingIssueMatch> ModelTrackingIssueMatches { get; set; }
    }

    /// <summary>
    /// Represents a match of a <see cref="ModelTrackingIssue"/> for a given <see cref="ModelBuildAttempt"/>.
    /// There can be many matches for a single build
    /// </summary>
    public class ModelTrackingIssueMatch
    {
        public int Id { get; set; }

        public string JobName { get; set; }

        public int ModelTrackingIssueId { get; set; }

        public ModelTrackingIssue ModelTrackingIssue { get; set; }

        public int ModelBuildAttemptId { get; set; }

        public ModelBuildAttempt ModelBuildAttempt { get; set; }

        public int? ModelTestResultId { get; set; }

        public ModelTestResult ModelTestResult { get; set; }

        public int? ModelTimelineIssueId { get; set; }

        public ModelTimelineIssue ModelTimelineIssue { get; set; }

        public string HelixLogUri { get; set; }
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
}