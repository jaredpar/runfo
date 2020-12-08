using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class SuggestedModelTestResultIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults");

            migrationBuilder.AddColumn<string>(
                name: "JobName",
                table: "ModelTestResults",
                type: "nvarchar(200)",
                nullable: true,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId")
                .Annotation("SqlServer:Include", new[] { "TestFullName", "JobName", "IsHelixTestResult" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "JobName",
                table: "ModelTestResults");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId");
        }
    }
}
