using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class DbRecommendedIndexs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_Attempt_ModelBuildId",
                table: "ModelTimelineIssues",
                columns: new[] { "Attempt", "ModelBuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionId_PullRequestNumber_StartTime",
                table: "ModelBuilds",
                columns: new[] { "DefinitionId", "PullRequestNumber", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildNumber", "BuildResult", "GitHubRepository" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelTimelineIssues_Attempt_ModelBuildId",
                table: "ModelTimelineIssues");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionId_PullRequestNumber_StartTime",
                table: "ModelBuilds");
        }
    }
}
