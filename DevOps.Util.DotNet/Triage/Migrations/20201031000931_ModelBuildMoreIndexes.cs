using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class ModelBuildMoreIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionId",
                table: "ModelBuilds",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionId",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds");
        }
    }
}
