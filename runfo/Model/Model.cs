using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Model
{
    public class RuntimeInfoDbContext : DbContext
    {
        public DbSet<TriageBuild> TriageBuilds { get; set; }
        public DbSet<TriageReason> TriageReasons { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(@"Data Source=C:\Users\jaredpar\AppData\Local\runfo\triage.db");
    }

    public class TriageBuild
    {
        public string Id { get; set; }

        public string Organization { get; set; }

        public string Project { get; set; }

        public int BuildNumber { get; set; }

        public bool IsComplete { get; set; }

        public List<TriageReason> TriageReasons { get; set; }
    }

    public class TriageReason
    {
        public int Id { get; set; }

        [Required]
        public string Reason { get; set; }

        public string IssueUri { get; set; }

        public string TriageBuildId { get; set; }
        public TriageBuild TriageBuild { get; set; }
    }
}