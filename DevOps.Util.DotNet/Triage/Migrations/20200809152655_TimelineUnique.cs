using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.Triage.Migrations
{
    public partial class TimelineUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_Attempt_ModelBuildId",
                table: "ModelBuildAttempts",
                columns: new[] { "Attempt", "ModelBuildId" },
                unique: true,
                filter: "[ModelBuildId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuildAttempts_Attempt_ModelBuildId",
                table: "ModelBuildAttempts");
        }
    }
}
