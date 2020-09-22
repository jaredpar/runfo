using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TestResults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelTestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzureOrganization = table.Column<string>(nullable: true),
                    AzureProject = table.Column<string>(nullable: true),
                    TestRunId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTestRuns_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelTestResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestFullName = table.Column<string>(nullable: true),
                    Outcome = table.Column<string>(nullable: true),
                    IsHelixTestResult = table.Column<bool>(nullable: false),
                    HelixConsoleUri = table.Column<string>(nullable: true),
                    HelixRunClientUri = table.Column<string>(nullable: true),
                    HelixCoreDumpUri = table.Column<string>(nullable: true),
                    HelixTestResultsUri = table.Column<string>(nullable: true),
                    ModelTestRunId = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTestResults_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelTestResults_ModelTestRuns_ModelTestRunId",
                        column: x => x.ModelTestRunId,
                        principalTable: "ModelTestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelTestRunId",
                table: "ModelTestResults",
                column: "ModelTestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_AzureOrganization_AzureProject_TestRunId",
                table: "ModelTestRuns",
                columns: new[] { "AzureOrganization", "AzureProject", "TestRunId" },
                unique: true,
                filter: "[AzureOrganization] IS NOT NULL AND [AzureProject] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTestResults");

            migrationBuilder.DropTable(
                name: "ModelTestRuns");
        }
    }
}
