using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevOps.Util.UnitTests
{
    internal static class Extensions
    {
        internal static string TrimNewlines(this string str) => str.Trim('\r', '\n');

        internal static void Clear<T>(this DbSet<T> dbSet)
            where T : class
        {
            dbSet.RemoveRange(dbSet);
        }

        internal static void DetachAllEntities(this TriageContext context)
        {
            var changedEntriesCopy = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in changedEntriesCopy)
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
