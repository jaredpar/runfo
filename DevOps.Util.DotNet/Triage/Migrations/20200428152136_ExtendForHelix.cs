using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ExtendForHelix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HelixJobId",
                table: "ModelTriageIssueResults",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HelixWorkItem",
                table: "ModelTriageIssueResults",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HelixJobId",
                table: "ModelTriageIssueResults");

            migrationBuilder.DropColumn(
                name: "HelixWorkItem",
                table: "ModelTriageIssueResults");
        }
    }
}
