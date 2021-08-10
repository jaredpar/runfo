using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class HelixLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AreHelixLogsComplete",
                table: "ModelTestRuns",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHelixWorkItem",
                table: "ModelTestResults",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ModelHelixLogs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HelixLogKind = table.Column<string>(nullable: false),
                    IsContentTooLarge = table.Column<bool>(nullable: false),
                    JobId = table.Column<string>(type: "varchar(200)", nullable: false),
                    WorkItemName = table.Column<string>(type: "varchar(200)", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    LogUri = table.Column<string>(type: "varchar(1000)", nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false),
                    ModelBuildAttemptId = table.Column<int>(nullable: false),
                    ModelTestRunId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelHelixLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelHelixLogs_ModelBuildAttempts_ModelBuildAttemptId",
                        column: x => x.ModelBuildAttemptId,
                        principalTable: "ModelBuildAttempts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModelHelixLogs_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelHelixLogs_ModelTestRuns_ModelTestRunId",
                        column: x => x.ModelTestRunId,
                        principalTable: "ModelTestRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelHelixLogs_LogUri",
                table: "ModelHelixLogs",
                column: "LogUri",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelHelixLogs_ModelBuildAttemptId",
                table: "ModelHelixLogs",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelHelixLogs_ModelBuildId",
                table: "ModelHelixLogs",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelHelixLogs_ModelTestRunId",
                table: "ModelHelixLogs",
                column: "ModelTestRunId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelHelixLogs");

            migrationBuilder.DropColumn(
                name: "AreHelixLogsComplete",
                table: "ModelTestRuns");

            migrationBuilder.DropColumn(
                name: "IsHelixWorkItem",
                table: "ModelTestResults");
        }
    }
}
