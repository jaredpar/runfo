using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class MoreBuildInfo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AzureOrganization",
                table: "ModelBuilds",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureProject",
                table: "ModelBuilds",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubTargetBranch",
                table: "ModelBuilds",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueueTime",
                table: "ModelBuilds",
                type: "smalldatetime",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzureOrganization",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "AzureProject",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "GitHubTargetBranch",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "QueueTime",
                table: "ModelBuilds");
        }
    }
}
