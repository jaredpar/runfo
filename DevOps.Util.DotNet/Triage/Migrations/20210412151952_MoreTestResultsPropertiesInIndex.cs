using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class MoreTestResultsPropertiesInIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_DefinitionName_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_DefinitionNumber_StartTime",
                table: "ModelTestResults");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "TestFullName", "TestRunName", "IsHelixTestResult" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_DefinitionName_StartTime",
                table: "ModelTestResults",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "TestFullName", "TestRunName", "IsHelixTestResult" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_DefinitionNumber_StartTime",
                table: "ModelTestResults",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "TestFullName", "TestRunName", "IsHelixTestResult" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_DefinitionName_StartTime",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_DefinitionNumber_StartTime",
                table: "ModelTestResults");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults",
                column: "StartTime")
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
        }
    }
}
