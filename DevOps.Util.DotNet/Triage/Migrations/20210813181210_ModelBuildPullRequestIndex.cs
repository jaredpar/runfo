using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ModelBuildPullRequestIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_GitHubOrganization_GitHubRepository_PullRequestNumber",
                table: "ModelBuilds",
                columns: new[] { "GitHubOrganization", "GitHubRepository", "PullRequestNumber" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_GitHubOrganization_GitHubRepository_PullRequestNumber",
                table: "ModelBuilds");
        }
    }
}
