using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.Triage.Migrations
{
    public partial class DropOldTimelineTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTimelineItems");

            migrationBuilder.DropTable(
                name: "ModelTimelineQueryCompletes");

            migrationBuilder.DropTable(
                name: "ModelTimelineQueries");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelTimelineQueries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GitHubOrganization = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GitHubRepository = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssueNumber = table.Column<int>(type: "int", nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildNumber = table.Column<int>(type: "int", nullable: false),
                    Line = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    ModelTimelineQueryId = table.Column<int>(type: "int", nullable: false),
                    TimelineRecordName = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    ModelTimelineQueryId = table.Column<int>(type: "int", nullable: false)
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
    }
}
