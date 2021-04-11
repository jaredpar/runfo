Items:
- X Move to the numeric ID for ModelBuild
- X Delete the old migrations
- X Evaluate every string for a `[Required]` value
- X Change the date time type. Just use the C# type `DateTime` and EF Core will
generate `datetime2(7)`
- X Change the tables to have the columns we need for queries
- X what is using Org.BouncyCastle.Math.EC.Rfc7748?
- SearchTestsRequests need to alias job name and test run name. Same value.
- X rename  GetModelBuildId to GetModelBuildKeyName
- X ensure all query properties set 
    - X specifically the updating to merged pull request
- X Delete all the filter map code in SearchBuildsRequests
- X Ensure all enum use int conversion
- X Delete  GetModelBuildKind
- Delete the ModelMigration table and enum

Indexes:
- X ModelBuild 
    - key: namekey
- X ModelBuildAttempt
    - key: (NameKey, int Attempt)
    - key: (BuildId, int Attempt)
- X Universal query: ModelBuild, ModelTestResult, ModelTimelineIssues, ModelBuildAttempt
    - key: started, definition
    - included columns: result, kind, targetBranch


        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                sql: "CREATE FULLTEXT CATALOG ftMessage AS DEFAULT;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "CREATE FULLTEXT INDEX ON ModelTimelineIssues(Message) KEY INDEX PK_ModelTimelineIssues;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "CREATE FULLTEXT CATALOG ftTestResult AS DEFAULT;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "CREATE FULLTEXT INDEX ON ModelTestResults(ErrorMessage) KEY INDEX PK_ModelTestResults ON ftTestResult;",
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                sql: "DROP FULLTEXT INDEX ON ModelTimelineIssues;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "DROP FULLTEXT CATALOG ftMessage",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "DROP FULLTEXT INDEX ON ModelTestResults;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "DROP FULLTEXT CATALOG ftTestResult",
                suppressTransaction: true);
        }


t        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
