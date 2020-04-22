using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DevOps.Util.Triage
{
    public class TriageDbContext : DbContext
    {
        public DbSet<ModelBuild> ModelBuilds { get; set; }

        public DbSet<ModelBuildDefinition> ModelBuildDefinitions { get; set; }

        public DbSet<ModelTimelineQuery> ModelTimelineQueries { get; set; }

        public DbSet<ModelTimelineItem> ModelTimelineItems { get; set; }

        public TriageDbContext(DbContextOptions<TriageDbContext> options)
            : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelTimelineQuery>()
                .HasIndex(x => new { x.GitHubOrganization, x.GitHubRepository, x.IssueNumber })
                .IsUnique();

            modelBuilder.Entity<ModelBuildDefinition>()
                .HasIndex(x => new { x.AzureOrganization, x.AzureProject, x.DefinitionId })
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
        public string Id { get; set; }

        public int BuildNumber { get; set; }

        public string GitHubOrganization { get; set; }

        public string GitHubRepository { get; set; }

        public int? PullRequestNumber { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? FinishTime { get; set; }

        public int ModelBuildDefinitionId { get; set; }

        public ModelBuildDefinition ModelBuildDefinition { get; set; }
    }

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

        public string ModelBuildId { get; set; }

        public ModelBuild ModelBuild { get; set; }

        public int ModelTimelineQueryId { get; set; }

        public ModelTimelineQuery ModelTimelineQuery { get; set; }
    }
}