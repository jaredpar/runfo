# Triage

This is a tool I'm experimenting with for helping me auto-triage issues

# Modifying the database

Typically this is done through ef migrations.  First executed with the test database to create the migration and then update the actual test database.  As always, this is easier to describe through an example.
In the following scenario the goal was to add a new indexable column (HelixWorkItemName) to the ModelTestResult table to allow helix log queries to restrict results to a specific work item name.

## Development

1.  Create the new column in code as `public string? HelixWorkItemName` in the `ModelTestResult` table.  Where nullable indicates that the column is optional (older entries will not have this).
2.  Add the column to the appropriate indices.  In this case it was included as a property in the indices queried when looking up older builds to triage against the new query
(we check if it is a helix result + matching name, the regex log search is done later against the builds we find from this index).
3.  Next, we want to create an EF migration so that the table actually gets updated.  From the scratch directory, I ran `dotnet ef migrations add AddHelixWorkItemName --project ..\DevOps.Util.DotNet\DevOps.Util.DotNet.csproj`
This adds the migration code (see DevOps.Util.DotNet/Triage/Migrations/20210922225854_AddHelixWorkItemName.cs) to the project which can (eventually) be checked into source control.
4.  Finally, we perform a `dotnet ef database update` to actually update the _test_ database.

## Deploying

Once the change has been validated in testing, the production database needs to be updated with the migration first, then the code using the new indices merged and deployed to the production site.