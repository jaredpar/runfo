using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ReverseIndexOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionName",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionNumber",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionName",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionNumber",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionNumber",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionName",
                table: "ModelBuildAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionNumber",
                table: "ModelBuildAttempts");

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

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_DefinitionName_StartTime",
                table: "ModelTestResults",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_DefinitionNumber_StartTime",
                table: "ModelTestResults",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionName_StartTime",
                table: "ModelBuilds",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionNumber_StartTime",
                table: "ModelBuilds",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_DefinitionName_StartTime",
                table: "ModelBuildAttempts",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_DefinitionNumber_StartTime",
                table: "ModelBuildAttempts",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_DefinitionName_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_DefinitionNumber_StartTime",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_DefinitionName_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_DefinitionNumber_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionName_StartTime",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionNumber_StartTime",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_DefinitionName_StartTime",
                table: "ModelBuildAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_DefinitionNumber_StartTime",
                table: "ModelBuildAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionName",
                table: "ModelTimelineIssues",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionNumber",
                table: "ModelTimelineIssues",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionName",
                table: "ModelTestResults",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionNumber",
                table: "ModelTestResults",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionNumber",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionName",
                table: "ModelBuildAttempts",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionNumber",
                table: "ModelBuildAttempts",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });
        }
    }
}
