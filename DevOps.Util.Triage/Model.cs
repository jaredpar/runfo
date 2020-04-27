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

        [Obsolete("Move to new model")]
        public DbSet<ModelTimelineQuery> ModelTimelineQueries { get; set; }

        [Obsolete("Move to new model")]
        public DbSet<ModelTimelineItem> ModelTimelineItems { get; set; }

        [Obsolete("Move to new model")]
        public DbSet<ModelTimelineQueryComplete> ModelTimelineQueryCompletes { get; set;}

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

            modelBuilder.Entity<ModelTimelineQuery>()
                .HasIndex(x => new { x.GitHubOrganization, x.GitHubRepository, x.IssueNumber })
                .IsUnique();

            modelBuilder.Entity<ModelTimelineQueryComplete>()
                .HasIndex(x => new { x.ModelTimelineQueryId, x.ModelBuildId })
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
    // TODO: include fields that will shape the report that we include in the actualy
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

    /* Tables to be deleted eventually */

    public class ModelTimelineQuery
    {
        public int Id { get; set; }

        [Required]
        public string GitHubOrganization { get; set; }

        [Required]
        public string GitHubRepository { get; set; }

        [Required]
        public int IssueNumber { get; set; }

        [Required]
        public string SearchText { get; set; }

        public List<ModelTimelineItem> ModelTimelineItems { get; set; }

        public List<ModelTimelineQueryComplete> ModelTimelineQueryCompletes { get; set; }
    }

    public class ModelTimelineQueryComplete
    {
        public int Id { get; set; }

        public int ModelTimelineQueryId { get; set; }

        public ModelTimelineQuery ModelTimelineQuery { get; set; }

        [Column(TypeName="nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }
    }

    /// <summary>
    /// Represents a result from a ModelTimelineQuery. These are not guaranteed to be 
    /// unique. It is possible for a build to have duplicate entries here for the same
    /// timeline entry in the log. 
    ///
    /// There is moderate gating done to ensure duplicate entries don't appear here 
    /// but they are not concrete
    /// </summary>
    public class ModelTimelineItem
    {
        public int Id { get; set; }

        public int BuildNumber { get; set; }

        public string TimelineRecordName { get; set; }

        public string Line { get; set; }

        [Column(TypeName="nvarchar(100)")]
        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }

        public int ModelTimelineQueryId { get; set; }

        public ModelTimelineQuery ModelTimelineQuery { get; set; }
    }
}