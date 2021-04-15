using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ResultIndexIsPresentAndTrackingIssue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueResults_IsPresent_ModelTrackingIssueId",
                table: "ModelTrackingIssueResults",
                columns: new[] { "IsPresent", "ModelTrackingIssueId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTrackingIssueResults_IsPresent_ModelTrackingIssueId",
                table: "ModelTrackingIssueResults");
        }
    }
}
