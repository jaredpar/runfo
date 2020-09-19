using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.Triage.Migrations
{
    public partial class GitHubIssueQuery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildQuery",
                table: "ModelTriageGitHubIssues",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeDefinitions",
                table: "ModelTriageGitHubIssues",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildQuery",
                table: "ModelTriageGitHubIssues");

            migrationBuilder.DropColumn(
                name: "IncludeDefinitions",
                table: "ModelTriageGitHubIssues");
        }
    }
}
