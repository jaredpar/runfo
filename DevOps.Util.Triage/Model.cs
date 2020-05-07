using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Util.Triage
{
    public class TriageContext : DbContext
    {
        public DbSet<ModelBuild> ModelBuilds { get; set; }

        public DbSet<ModelBuildDefinition> ModelBuildDefinitions { get; set; }

        public DbSet<ModelTriageIssue> ModelTriageIssues { get; set; }

        public DbSet<ModelTriageIssueResult> ModelTriageIssueResults { get; set; }

        public DbSet<ModelTriageIssueResultComplete> ModelTriageIssueResultCompletes { get; set; }

        public DbSet<ModelTriageGitHubIssue> ModelTriageGitHubIssues { get; set; }

        public DbSet<ModelOsxDeprovisionRetry> ModelOsxDeprovisionRetry { get; set; }

        public TriageContext(DbContextOptions<TriageContext> options)
            : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelBuildDefinition>()
                .HasIndex(x => new { x.AzureOrganization, x.AzureProject, x.DefinitionId })
                .IsUnique();

            modelBuilder.Entity<ModelTriageIssue>()
                .HasIndex(x => new { x.SearchKind, x.SearchText })
                .IsUnique();

            modelBuilder.Entity<ModelTriageIssue>()
                .Property(x => x.SearchKind)
                .HasConversion<string>();

            modelBuilder.Entity<ModelTriageIssue>()
                .Property(x => x.TriageIssueKind)
                .HasConversion<string>();

            modelBuilder.Entity<ModelTriageGitHubIssue>()
                .HasIndex(x => new { x.Organization, x.Repository, x.IssueNumber })
                .IsUnique();

            modelBuilder.Entity<ModelTriageIssueResultComplete>()
                .HasIndex(x => new { x.ModelTriageIssueId, x.ModelBuildId })
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

        public string GitHubOrganization { get; set; }

        public string GitHubRepository { get; set; }

        public int? PullRequestNumber { get; set; }

        [Column(TypeName="smalldatetime")]
        public DateTime? StartTime { get; set; }

        [Column(TypeName="smalldatetime")]
        public DateTime? FinishTime { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }
    }

    public enum TriageIssueKind
    {
        Unknown = 0,

        Azure,

        Helix,

        NuGet,

        // General infrastructure owned by the .NET Team
        Infra,

        Build,

        Test,
    }

    public enum SearchKind
    {
        Unknown,

        SearchTimeline,

        SearchHelixRunClient,

        SearchHelixConsole,

        SearchHelixTestResults,

        SearchTest,
    }

    /// <summary>
    /// This is an issue that the tool is attempting to auto-triage as builds complete
    /// </summary>
    public class ModelTriageIssue
    {
        public int Id { get; set;}

        [Column(TypeName = "nvarchar(30)")]
        public TriageIssueKind TriageIssueKind { get; set; }

        [Column(TypeName = "nvarchar(30)")]
        public SearchKind SearchKind { get; set; }

        [Column(TypeName="nvarchar(400)")]
        public string SearchText { get; set; }

        public List<ModelTriageIssueResult> ModelTriageIssueResults { get; set; }

        public List<ModelTriageGitHubIssue> ModelTriageGitHubIssues { get; set; }
    }

    /// <summary>
    /// Represents an issue that needs to be updated for the associated triage issue 
    /// above
    /// </summary>
    // TODO: include fields that will shape the report that we include in the actually
    // issue here
    public class  ModelTriageGitHubIssue
    {
        public int Id { get; set; }

        [Required]
        public string Organization { get; set; }

        [Required]
        public string Repository { get; set; }

        [Required]
        public int IssueNumber { get; set; }

        // Whether or not to include BuildDefinitions in the report
        public bool IncludeDefinitions { get; set; }

        // The query to use to filter builds hitting the overall triage issue to this specific
        // GitHub issue. If empty it will filter to builds against the repository where this 
        // issue was defined
        public string BuildQuery { get; set; }

        [NotMapped]
        public GitHubIssueKey IssueKey => new GitHubIssueKey(Organization, Repository, IssueNumber);

        public int ModelTriageIssueId { get; set; }

        public ModelTriageIssue ModelTriageIssue { get; set; }
    }

    /// <summary>
    /// Represents a result from a ModelTriageIssue for a given build. 
    /// </summary>
    public class ModelTriageIssueResult
    {
        public int Id { get; set; }

        public int BuildNumber { get; set; }

        public string JobName { get; set; }

        public string TimelineRecordName { get; set; }

        public string Line { get; set; }

        public string HelixJobId { get; set; }

        public string HelixWorkItem { get; set; }

        [Column(TypeName="nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }

        public int ModelTriageIssueId { get; set; }

        public ModelTriageIssue ModelTriageIssue { get; set; }
    }

    public class ModelTriageIssueResultComplete
    {
        public int Id { get; set; }

        public int ModelTriageIssueId { get; set; }

        public ModelTriageIssue ModelTriageIssue { get; set; }

        [Column(TypeName="nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
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
}