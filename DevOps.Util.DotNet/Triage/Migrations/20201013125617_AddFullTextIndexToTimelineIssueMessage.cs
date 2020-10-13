using Azure.Storage.Queues.Models;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class AddFullTextIndexToTimelineIssueMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                sql: "CREATE FULLTEXT CATALOG ftMessage AS DEFAULT;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                sql: "CREATE FULLTEXT INDEX ON ModelTimelineIssues(Message) KEY INDEX PK_ModelTimelineIssues;",
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
        }
    }
}
