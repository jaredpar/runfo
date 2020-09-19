using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TrackingCollections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobName",
                table: "ModelTrackingIssueMatches",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobName",
                table: "ModelTrackingIssueMatches");
        }
    }
}
