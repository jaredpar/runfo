using DevOps.Util.DotNet.Triage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class DatabaseFixture : IDisposable
    {
        private readonly List<Action<string>> _loggerActions = new();

        public DbContextOptions<TriageContext> Options { get; }
        public TriageContext TriageContext { get; private set; }

        public DatabaseFixture()
        {
            var builder = new DbContextOptionsBuilder<TriageContext>();
            builder.UseSqlServer("Server=localhost;Database=runfo-test-db;User Id=sa;Password=password@0;");
            builder.EnableSensitiveDataLogging();
            builder.UseLoggerFactory(LoggerFactory.Create(builder =>
            {
                var options = new DatabaseLoggerOptions(LoggerAction);
                builder.AddConfiguration();
                builder.Services.TryAddEnumerable(
                    ServiceDescriptor.Singleton<ILoggerProvider, DatabaseLoggerProvider>());
                builder.Services.TryAddSingleton(options);

            }));
            // builder.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
            Options = builder.Options;
            TriageContext = new TriageContext(Options);
            TriageContext.Database.EnsureDeleted();
            TriageContext.Database.Migrate();
        }

        public void RegisterLoggerAction(Action<string> action) => _loggerActions.Add(action);

        public void UnregisterLoggerAction(Action<string> action) => _loggerActions.Remove(action);

        private void LoggerAction(string message)
        {
            foreach (var action in _loggerActions)
            {
                action(message);
            }
        }

        /*
        private static TriageContext CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<TriageContext>()
                .UseSqlServer()
                .UseSqlite(connection)
                .Options;
            var context = new TriageContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            return context;
        }
        */

        public void AssertEmpty()
        {
            Assert.Equal(0, TriageContext.ModelBuilds.Count());
            Assert.Equal(0, TriageContext.ModelBuildDefinitions.Count());
        }

        public void DetachAllEntities()
        {
            var changedEntriesCopy = TriageContext.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in changedEntriesCopy)
                entry.State = EntityState.Detached;
        }

        public void Dispose()
        {
            TriageContext.Database.EnsureDeleted();
            TriageContext.Dispose();
        }

        public void TestCompletion()
        {
            _loggerActions.Clear();
            TriageContext.ModelTrackingIssues.RemoveRange(TriageContext.ModelTrackingIssues);
            TriageContext.ModelTimelineIssues.RemoveRange(TriageContext.ModelTimelineIssues);
            TriageContext.ModelTestResults.RemoveRange(TriageContext.ModelTestResults);
            TriageContext.ModelTestRuns.RemoveRange(TriageContext.ModelTestRuns);
            TriageContext.ModelBuilds.RemoveRange(TriageContext.ModelBuilds);
            TriageContext.ModelBuildDefinitions.RemoveRange(TriageContext.ModelBuildDefinitions);
            TriageContext.SaveChanges();
            AssertEmpty();
        }
    }

    [CollectionDefinition(DatabaseCollection.Name)]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        public const string Name = "TriageContext Database Collection";
    }

}
