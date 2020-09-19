using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.Triage.Migrations
{
    public partial class OsxDeprovision : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelOsxDeprovisionRetry",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OsxJobFailedCount = table.Column<int>(nullable: false),
                    JobFailedCount = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelOsxDeprovisionRetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelOsxDeprovisionRetry_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelOsxDeprovisionRetry_ModelBuildId",
                table: "ModelOsxDeprovisionRetry",
                column: "ModelBuildId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelOsxDeprovisionRetry");
        }
    }
}
