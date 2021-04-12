using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class IndexStartOnly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime",
                table: "ModelTimelineIssues",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime",
                table: "ModelBuilds",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime",
                table: "ModelBuildAttempts",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_StartTime",
                table: "ModelBuildAttempts");
        }
    }
}
