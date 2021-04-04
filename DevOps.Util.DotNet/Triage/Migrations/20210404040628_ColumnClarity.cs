using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ColumnClarity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionId",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildDefinitions_AzureOrganization_AzureProject_DefinitionId",
                table: "ModelBuildDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionId",
                table: "ModelBuildAttempts");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "ModelBuildDefinitions");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "ModelBuildAttempts");

            migrationBuilder.AddColumn<int>(
                name: "DefinitionNumber",
                table: "ModelTimelineIssues",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionNumber",
                table: "ModelTestResults",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionNumber",
                table: "ModelBuilds",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionNumber",
                table: "ModelBuildDefinitions",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionNumber",
                table: "ModelBuildAttempts",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionNumber",
                table: "ModelTimelineIssues",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionNumber",
                table: "ModelTestResults",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionNumber",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildDefinitions_AzureOrganization_AzureProject_DefinitionNumber",
                table: "ModelBuildDefinitions",
                columns: new[] { "AzureOrganization", "AzureProject", "DefinitionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionNumber",
                table: "ModelBuildAttempts",
                columns: new[] { "StartTime", "DefinitionNumber" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionNumber",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionNumber",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionNumber",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildDefinitions_AzureOrganization_AzureProject_DefinitionNumber",
                table: "ModelBuildDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionNumber",
                table: "ModelBuildAttempts");

            migrationBuilder.DropColumn(
                name: "DefinitionNumber",
                table: "ModelTimelineIssues");

            migrationBuilder.DropColumn(
                name: "DefinitionNumber",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "DefinitionNumber",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "DefinitionNumber",
                table: "ModelBuildDefinitions");

            migrationBuilder.DropColumn(
                name: "DefinitionNumber",
                table: "ModelBuildAttempts");

            migrationBuilder.AddColumn<int>(
                name: "DefinitionId",
                table: "ModelTimelineIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionId",
                table: "ModelTestResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionId",
                table: "ModelBuilds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionId",
                table: "ModelBuildDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionId",
                table: "ModelBuildAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionId",
                table: "ModelTimelineIssues",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionId",
                table: "ModelTestResults",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildDefinitions_AzureOrganization_AzureProject_DefinitionId",
                table: "ModelBuildDefinitions",
                columns: new[] { "AzureOrganization", "AzureProject", "DefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionId",
                table: "ModelBuildAttempts",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });
        }
    }
}
