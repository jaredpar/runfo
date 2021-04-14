using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TrackingIssueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssues_IsActive_Id",
                table: "ModelTrackingIssues",
                columns: new[] { "IsActive", "Id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTrackingIssues_IsActive_Id",
                table: "ModelTrackingIssues");
        }
    }
}
