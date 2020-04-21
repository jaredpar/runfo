using Microsoft.EntityFrameworkCore.Migrations;

namespace triage.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelBuilds",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    AzureOrganization = table.Column<string>(nullable: true),
                    AzureProject = table.Column<string>(nullable: true),
                    BuildNumber = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineQueries",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GitHubOrganization = table.Column<string>(nullable: false),
                    GitHubRepository = table.Column<string>(nullable: false),
                    IssueId = table.Column<int>(nullable: false),
                    SearchText = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineItems",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimelineRecordName = table.Column<string>(nullable: true),
                    Line = table.Column<string>(nullable: true),
                    ModelBuildId = table.Column<string>(nullable: true),
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

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineItems_ModelBuildId",
                table: "ModelTimelineItems",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineItems_ModelTimelineQueryId",
                table: "ModelTimelineItems",
                column: "ModelTimelineQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineQueries_GitHubOrganization_GitHubRepository_IssueId",
                table: "ModelTimelineQueries",
                columns: new[] { "GitHubOrganization", "GitHubRepository", "IssueId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTimelineItems");

            migrationBuilder.DropTable(
                name: "ModelBuilds");

            migrationBuilder.DropTable(
                name: "ModelTimelineQueries");
        }
    }
}
