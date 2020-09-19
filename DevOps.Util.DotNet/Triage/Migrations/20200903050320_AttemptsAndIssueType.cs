using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class AttemptsAndIssueType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssueType",
                table: "ModelTimelineIssues",
                type: "nvarchar(12)",
                nullable: false,
                defaultValue: "Warning");

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ModelTestRuns",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssueType",
                table: "ModelTimelineIssues");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ModelTestRuns");
        }
    }
}
