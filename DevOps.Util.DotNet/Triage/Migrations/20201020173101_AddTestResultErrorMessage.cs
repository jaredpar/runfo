using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class AddTestResultErrorMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "ModelTestResults",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubResult",
                table: "ModelTestResults",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubResultContainer",
                table: "ModelTestResults",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "IsSubResult",
                table: "ModelTestResults");

            migrationBuilder.DropColumn(
                name: "IsSubResultContainer",
                table: "ModelTestResults");
        }
    }
}
