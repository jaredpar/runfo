using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TrackingMatchNeedHelixLogKind : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelTrackingIssues_ModelGitHubIssues_ModelGitHubIssueId",
                table: "ModelTrackingIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTrackingIssues_ModelGitHubIssueId",
                table: "ModelTrackingIssues");

            migrationBuilder.DropColumn(
                name: "ModelGitHubIssueId",
                table: "ModelTrackingIssues");

            migrationBuilder.AddColumn<string>(
                name: "HelixLogKind",
                table: "ModelTrackingIssueMatches",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HelixLogKind",
                table: "ModelTrackingIssueMatches");

            migrationBuilder.AddColumn<int>(
                name: "ModelGitHubIssueId",
                table: "ModelTrackingIssues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssues_ModelGitHubIssueId",
                table: "ModelTrackingIssues",
                column: "ModelGitHubIssueId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTrackingIssues_ModelGitHubIssues_ModelGitHubIssueId",
                table: "ModelTrackingIssues",
                column: "ModelGitHubIssueId",
                principalTable: "ModelGitHubIssues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
