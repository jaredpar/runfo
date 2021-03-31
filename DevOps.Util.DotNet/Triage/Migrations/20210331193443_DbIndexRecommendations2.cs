using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class DbIndexRecommendations2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_Attempt_ModelBuildId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId_Attempt",
                table: "ModelTimelineIssues",
                columns: new[] { "ModelBuildId", "Attempt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId")
                .Annotation("SqlServer:Include", new[] { "AzureOrganization", "AzureProject", "TestRunId", "Name" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId_Attempt",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_Attempt_ModelBuildId",
                table: "ModelTimelineIssues",
                columns: new[] { "Attempt", "ModelBuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId");
        }
    }
}
