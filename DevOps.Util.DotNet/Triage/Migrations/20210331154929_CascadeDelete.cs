using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class CascadeDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelBuildAttempts_ModelBuilds_ModelBuildId",
                table: "ModelBuildAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelGitHubIssues_ModelBuilds_ModelBuildId",
                table: "ModelGitHubIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestResults_ModelBuilds_ModelBuildId",
                table: "ModelTestResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuilds_ModelBuildId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_ModelBuildAttemptId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropColumn(
                name: "ModelBuildAttemptId",
                table: "ModelTimelineIssues");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelBuildAttempts_ModelBuilds_ModelBuildId",
                table: "ModelBuildAttempts",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelGitHubIssues_ModelBuilds_ModelBuildId",
                table: "ModelGitHubIssues",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestResults_ModelBuilds_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuilds_ModelBuildId",
                table: "ModelTimelineIssues",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelBuildAttempts_ModelBuilds_ModelBuildId",
                table: "ModelBuildAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelGitHubIssues_ModelBuilds_ModelBuildId",
                table: "ModelGitHubIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTestResults_ModelBuilds_ModelBuildId",
                table: "ModelTestResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuilds_ModelBuildId",
                table: "ModelTimelineIssues");

            migrationBuilder.AddColumn<int>(
                name: "ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                column: "ModelBuildAttemptId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelBuildAttempts_ModelBuilds_ModelBuildId",
                table: "ModelBuildAttempts",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelGitHubIssues_ModelBuilds_ModelBuildId",
                table: "ModelGitHubIssues",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTestResults_ModelBuilds_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuildAttempts_ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                column: "ModelBuildAttemptId",
                principalTable: "ModelBuildAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelTimelineIssues_ModelBuilds_ModelBuildId",
                table: "ModelTimelineIssues",
                column: "ModelBuildId",
                principalTable: "ModelBuilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
