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

            using var context = new TriageContext(Options);
            context.Database.EnsureDeleted();
            context.Database.Migrate();
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

        public void Dispose()
        {
            using var context = new TriageContext(Options);
            context.Database.EnsureDeleted();
        }

        public void TestCompletion()
        {
            _loggerActions.Clear();
            using (var context = new TriageContext(Options))
            {
                context.ModelBuilds.RemoveRange(context.ModelBuilds);
                context.SaveChanges();
            }

            using (var context = new TriageContext(Options))
            {
                context.ModelTrackingIssues.RemoveRange(context.ModelTrackingIssues);
                context.SaveChanges();
            }

            using (var context = new TriageContext(Options))
            {
                context.ModelBuildDefinitions.RemoveRange(context.ModelBuildDefinitions);
                context.SaveChanges();
            }
        }
    }

    [CollectionDefinition(DatabaseCollection.Name)]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        public const string Name = "TriageContext Database Collection";
    }

}
