using Microsoft.EntityFrameworkCore.Migrations;

namespace runfo.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TriageBuilds",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Organization = table.Column<string>(nullable: true),
                    Project = table.Column<string>(nullable: true),
                    BuildNumber = table.Column<int>(nullable: false),
                    IsComplete = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageBuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriageReasons",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Reason = table.Column<string>(nullable: false),
                    IssueUri = table.Column<string>(nullable: true),
                    TriageBuildId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageReasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriageReasons_TriageBuilds_TriageBuildId",
                        column: x => x.TriageBuildId,
                        principalTable: "TriageBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriageReasons_TriageBuildId",
                table: "TriageReasons",
                column: "TriageBuildId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriageReasons");

            migrationBuilder.DropTable(
                name: "TriageBuilds");
        }
    }
}
