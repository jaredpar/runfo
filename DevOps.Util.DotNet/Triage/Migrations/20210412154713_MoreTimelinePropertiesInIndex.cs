using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class MoreTimelinePropertiesInIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_DefinitionName_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_DefinitionNumber_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime",
                table: "ModelTimelineIssues",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "IssueType", "JobName", "TaskName", "RecordName" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_DefinitionName_StartTime",
                table: "ModelTimelineIssues",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "IssueType", "JobName", "TaskName", "RecordName" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_DefinitionNumber_StartTime",
                table: "ModelTimelineIssues",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "IssueType", "JobName", "TaskName", "RecordName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_DefinitionName_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_DefinitionNumber_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime",
                table: "ModelTimelineIssues",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_DefinitionName_StartTime",
                table: "ModelTimelineIssues",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_DefinitionNumber_StartTime",
                table: "ModelTimelineIssues",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });
        }
    }
}
