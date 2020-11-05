using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TimelineIssuesTaskName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskName",
                table: "ModelTimelineIssues",
                type: "nvarchar(100)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskName",
                table: "ModelTimelineIssues");
        }
    }
}
