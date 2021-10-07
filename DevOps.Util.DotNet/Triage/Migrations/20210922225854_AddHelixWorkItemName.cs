using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class AddHelixWorkItemName : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "HelixWorkItemName",
                table: "ModelTestResults",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime",
                table: "ModelTestResults",
                column: "StartTime")
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "TestFullName", "TestRunName", "IsHelixTestResult", "HelixWorkItemName" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_DefinitionName_StartTime",
                table: "ModelTestResults",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "TestFullName", "TestRunName", "IsHelixTestResult", "HelixWorkItemName" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_DefinitionNumber_StartTime",
                table: "ModelTestResults",
                columns: new[] { "DefinitionNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch", "TestFullName", "TestRunName", "IsHelixTestResult", "HelixWorkItemName" });
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

            migrationBuilder.DropColumn(
                name: "HelixWorkItemName",
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
    }
}
