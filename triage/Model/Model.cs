using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Model
{
    public class TriageDbContext : DbContext
    {
        public DbSet<ProcessedBuild> ProcessedBuilds { get; set; }

        public DbSet<TimelineIssue> TimelineIssues { get; set; }

        public DbSet<TimelineEntry> TimelineEntries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(@"Data Source=C:\Users\jaredpar\AppData\Local\runfo\triage.db");
    }

    /// <summary>
    /// Represents a build that was already processed. This table is used for historical
    /// data as well a starting point for what the next auto-triage should be considering.
    /// </summary>
    public class ProcessedBuild
    {
        public int Id { get; set; }

        public string AzureOrganization { get; set; }

        public string AzureProject { get; set; }

        public int BuildNumber { get; set; }
    }

    public class TimelineIssue
    {
        public string Id { get; set; }

        [Required]
        public string GitHubOrganization { get; set; }

        [Required]
        public string GitHubRepository { get; set; }

        [Required]
        public int IssueId { get; set; }

        [Required]
        public string SearchText { get; set; }

        List<TimelineEntry> TimelineEntries { get; set; }
    }

    public class TimelineEntry
    {
        [Key]
        public string BuildKey { get; set; }

        [Required]
        public string AzureOrganization {get; set; }

        [Required]
        public string AzureProject {get; set; }

        [Required]
        public int BuildNumber { get; set; }

        public string TimelineRecordName { get; set; }

        public string Line { get; set; }

        public int TimelineIssueId { get; set; }

        public TimelineIssue TimelineIssue { get; set; }
    }
}