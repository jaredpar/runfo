using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ModelTimelineIssuesIncludeColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId",
                table: "ModelTimelineIssues");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId",
                table: "ModelTimelineIssues",
                column: "ModelBuildId")
                .Annotation("SqlServer:Include", new[] { "JobName", "TaskName", "RecordName", "IssueType", "Attempt", "Message" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId",
                table: "ModelTimelineIssues");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId",
                table: "ModelTimelineIssues",
                column: "ModelBuildId");
        }
    }
}
