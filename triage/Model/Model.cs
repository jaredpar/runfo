using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Model
{
    public class TriageDbContext : DbContext
    {
        public DbSet<ModelBuild> ModelBuilds { get; set; }

        public DbSet<ModelTimelineQuery> ModelTimelineQueries { get; set; }

        public DbSet<ModelTimelineItem> ModelTimelineItems { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(@"Data Source=C:\Users\jaredpar\AppData\Local\runfo\triage.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelTimelineQuery>()
                .HasIndex(x => new { x.GitHubOrganization, x.GitHubRepository, x.IssueId })
                .IsUnique();
        }
    }
    public class ModelBuild
    {
        public string Id { get; set; }

        public string AzureOrganization { get; set; }

        public string AzureProject { get; set; }

        public int BuildNumber { get; set; }
    }

    public class ModelTimelineQuery
    {
        public int Id { get; set; }

        [Required]
        public string GitHubOrganization { get; set; }

        [Required]
        public string GitHubRepository { get; set; }

        [Required]
        public int IssueId { get; set; }

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