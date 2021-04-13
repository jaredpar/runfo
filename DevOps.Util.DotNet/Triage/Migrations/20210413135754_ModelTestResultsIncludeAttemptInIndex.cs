using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ModelTestResultsIncludeAttemptInIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildId_Attempt",
                table: "ModelTestResults",
                columns: new[] { "ModelBuildId", "Attempt" })
                .Annotation("SqlServer:Include", new[] { "TestFullName", "TestRunName", "IsHelixTestResult" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelBuildId_Attempt",
                table: "ModelTestResults");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId")
                .Annotation("SqlServer:Include", new[] { "TestFullName", "TestRunName", "IsHelixTestResult" });
        }
    }
}
