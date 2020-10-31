using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class DeNormalizeDefinitionInfo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BuildResult",
                table: "ModelBuilds",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionId",
                table: "ModelBuilds",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionName",
                table: "ModelBuilds",
                type: "nvarchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DefinitionName",
                table: "ModelBuildDefinitions",
                type: "nvarchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_BuildResult",
                table: "ModelBuilds",
                column: "BuildResult");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_DefinitionName",
                table: "ModelBuilds",
                column: "DefinitionName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime",
                table: "ModelBuilds",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_BuildResult",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_DefinitionName",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime",
                table: "ModelBuilds");

            migrationBuilder.DropIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "ModelBuilds");

            migrationBuilder.DropColumn(
                name: "DefinitionName",
                table: "ModelBuilds");

            migrationBuilder.AlterColumn<string>(
                name: "BuildResult",
                table: "ModelBuilds",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefinitionName",
                table: "ModelBuildDefinitions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldNullable: true);
        }
    }
}
