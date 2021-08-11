using DevOps.Util.DotNet.Triage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class DatabaseFixture : IDisposable
    {
        public TriageContext TriageContext { get; private set; }

        public DatabaseFixture()
        {
            TriageContext = CreateInMemoryDatabase();
        }

        private static TriageContext CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<TriageContext>()
                .UseSqlite(connection)
                .Options;
            var context = new TriageContext(options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            return context;
        }

        public void Dispose()
        {
            TriageContext.Dispose();
        }

        public void TestCompletion()
        {
            TriageContext.Dispose();
            TriageContext = CreateInMemoryDatabase();
        }
    }

    [CollectionDefinition(DatabaseCollection.Name)]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        public const string Name = "TriageContext Database Collection";
    }
}
