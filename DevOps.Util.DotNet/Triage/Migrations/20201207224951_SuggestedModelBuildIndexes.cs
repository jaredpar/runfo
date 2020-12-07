using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class SuggestedModelBuildIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionId_StartTime",
                table: "ModelBuilds",
                columns: new[] { "DefinitionId", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildNumber", "BuildResult", "PullRequestNumber", "GitHubRepository" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildNumber", "BuildResult", "PullRequestNumber", "GitHubRepository" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionId_StartTime",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionName" });
        }
    }
}
