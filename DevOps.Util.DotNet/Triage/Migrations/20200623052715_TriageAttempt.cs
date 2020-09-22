using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TriageAttempt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ModelTriageIssueResults",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "JobRecordId",
                table: "ModelTriageIssueResults",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootRecordId",
                table: "ModelTriageIssueResults",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ModelTriageIssueResults");

            migrationBuilder.DropColumn(
                name: "JobRecordId",
                table: "ModelTriageIssueResults");

            migrationBuilder.DropColumn(
                name: "RootRecordId",
                table: "ModelTriageIssueResults");
        }
    }
}
