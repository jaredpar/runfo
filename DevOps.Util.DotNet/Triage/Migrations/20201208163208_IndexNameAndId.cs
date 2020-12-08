using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class IndexNameAndId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionName_StartTime",
                table: "ModelBuilds",
                columns: new[] { "DefinitionName", "StartTime" })
                .Annotation("SqlServer:Include", new[] { "BuildNumber", "BuildResult", "PullRequestNumber", "GitHubRepository" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildNumber", "BuildResult", "PullRequestNumber", "GitHubRepository" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionName_StartTime",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds");
        }
    }
}
