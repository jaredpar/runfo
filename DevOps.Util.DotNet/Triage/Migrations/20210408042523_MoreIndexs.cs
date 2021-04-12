using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class MoreIndexs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelTestRunId",
                table: "ModelTestResults");

            migrationBuilder.AddColumn<int>(
                name: "ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModelBuildAttemptId",
                table: "ModelTestRuns",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ModelTestResults",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModelBuildAttemptId",
                table: "ModelTestResults",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildAttemptId",
                table: "ModelTestRuns",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildId_TestRunId",
                table: "ModelTestRuns",
                columns: new[] { "ModelBuildId", "TestRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildAttemptId",
                table: "ModelTestResults",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelTestRunId",
                table: "ModelTestResults",
                column: "ModelTestRunId")
                .Annotation("SqlServer:Include", new[] { "TestFullName", "TestRunName", "IsHelixTestResult" });

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestResults_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTestResults",
                column: "ModelBuildAttemptId",
                principalTable: "ModelBuildAttempts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestRuns_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTestRuns",
                column: "ModelBuildAttemptId",
                principalTable: "ModelBuildAttempts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                column: "ModelBuildAttemptId",
                principalTable: "ModelBuildAttempts",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestResults_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTestResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestRuns_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTestRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_ModelBuildAttemptId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestRuns_ModelBuildAttemptId",
                table: "ModelTestRuns");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestRuns_ModelBuildId_TestRunId",
                table: "ModelTestRuns");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelBuildAttemptId",
                table: "ModelTestResults");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestResults_ModelTestRunId",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "ModelBuildAttemptId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropColumn(
                name: "ModelBuildAttemptId",
                table: "ModelTestRuns");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "ModelBuildAttemptId",
                table: "ModelTestResults");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelTestRunId",
                table: "ModelTestResults",
                column: "ModelTestRunId");
        }
    }
}
