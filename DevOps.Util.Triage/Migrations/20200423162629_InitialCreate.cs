using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.Triage.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelBuildDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzureOrganization = table.Column<string>(nullable: true),
                    AzureProject = table.Column<string>(nullable: true),
                    DefinitionName = table.Column<string>(nullable: true),
                    DefinitionId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuildDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineQueries",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GitHubOrganization = table.Column<string>(nullable: false),
                    GitHubRepository = table.Column<string>(nullable: false),
                    IssueNumber = table.Column<int>(nullable: false),
                    SearchText = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelBuilds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    BuildNumber = table.Column<int>(nullable: false),
                    GitHubOrganization = table.Column<string>(nullable: true),
                    GitHubRepository = table.Column<string>(nullable: true),
                    PullRequestNumber = table.Column<int>(nullable: true),
                    StartTime = table.Column<DateTime>(type: "smalldatetime", nullable: true),
                    FinishTime = table.Column<DateTime>(type: "smalldatetime", nullable: true),
                    ModelBuildDefinitionId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuilds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelBuilds_ModelBuildDefinitions_ModelBuildDefinitionId",
                        column: x => x.ModelBuildDefinitionId,
                        principalTable: "ModelBuildDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineItems",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildNumber = table.Column<int>(nullable: false),
                    TimelineRecordName = table.Column<string>(nullable: true),
                    Line = table.Column<string>(nullable: true),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    ModelTimelineQueryId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTimelineItems_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelTimelineItems_ModelTimelineQueries_ModelTimelineQueryId",
                        column: x => x.ModelTimelineQueryId,
                        principalTable: "ModelTimelineQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineQueryCompletes",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelTimelineQueryId = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineQueryCompletes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTimelineQueryCompletes_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelTimelineQueryCompletes_ModelTimelineQueries_ModelTimelineQueryId",
                        column: x => x.ModelTimelineQueryId,
                        principalTable: "ModelTimelineQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildDefinitions_AzureOrganization_AzureProject_DefinitionId",
                table: "ModelBuildDefinitions",
                columns: new[] { "AzureOrganization", "AzureProject", "DefinitionId" },
                unique: true,
                filter: "[AzureOrganization] IS NOT NULL AND [AzureProject] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_ModelBuildDefinitionId",
                table: "ModelBuilds",
                column: "ModelBuildDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineItems_ModelBuildId",
                table: "ModelTimelineItems",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineItems_ModelTimelineQueryId",
                table: "ModelTimelineItems",
                column: "ModelTimelineQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineQueries_GitHubOrganization_GitHubRepository_IssueNumber",
                table: "ModelTimelineQueries",
                columns: new[] { "GitHubOrganization", "GitHubRepository", "IssueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineQueryCompletes_ModelBuildId",
                table: "ModelTimelineQueryCompletes",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineQueryCompletes_ModelTimelineQueryId_ModelBuildId",
                table: "ModelTimelineQueryCompletes",
                columns: new[] { "ModelTimelineQueryId", "ModelBuildId" },
                unique: true,
                filter: "[ModelBuildId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTimelineItems");

            migrationBuilder.DropTable(
                name: "ModelTimelineQueryCompletes");

            migrationBuilder.DropTable(
                name: "ModelBuilds");

            migrationBuilder.DropTable(
                name: "ModelTimelineQueries");

            migrationBuilder.DropTable(
                name: "ModelBuildDefinitions");
        }
    }
}
