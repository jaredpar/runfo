using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class CascadeDelete2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelOsxDeprovisionRetry_ModelBuilds_ModelBuildId",
                table: "ModelOsxDeprovisionRetry");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestResults_ModelTestRuns_ModelTestRunId",
                table: "ModelTestResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestRuns_ModelBuilds_ModelBuildId",
                table: "ModelTestRuns");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelOsxDeprovisionRetry_ModelBuilds_ModelBuildId",
                table: "ModelOsxDeprovisionRetry",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestResults_ModelTestRuns_ModelTestRunId",
                table: "ModelTestResults",
                column: "ModelTestRunId",
                principalTable: "ModelTestRuns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestRuns_ModelBuilds_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelOsxDeprovisionRetry_ModelBuilds_ModelBuildId",
                table: "ModelOsxDeprovisionRetry");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestResults_ModelTestRuns_ModelTestRunId",
                table: "ModelTestResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestRuns_ModelBuilds_ModelBuildId",
                table: "ModelTestRuns");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelOsxDeprovisionRetry_ModelBuilds_ModelBuildId",
                table: "ModelOsxDeprovisionRetry",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestResults_ModelTestRuns_ModelTestRunId",
                table: "ModelTestResults",
                column: "ModelTestRunId",
                principalTable: "ModelTestRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestRuns_ModelBuilds_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
