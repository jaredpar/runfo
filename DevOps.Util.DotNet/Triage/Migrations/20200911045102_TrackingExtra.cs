using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TrackingExtra : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelTrackingIssueMatches",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelTrackingIssueId = table.Column<int>(nullable: false),
                    ModelBuildAttemptId = table.Column<int>(nullable: false),
                    ModelTestResultId = table.Column<int>(nullable: true),
                    ModelTimelineIssueId = table.Column<int>(nullable: true),
                    HelixLogUri = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTrackingIssueMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssueMatches_ModelBuildAttempts_ModelBuildAttemptId",
                        column: x => x.ModelBuildAttemptId,
                        principalTable: "ModelBuildAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssueMatches_ModelTestResults_ModelTestResultId",
                        column: x => x.ModelTestResultId,
                        principalTable: "ModelTestResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssueMatches_ModelTimelineIssues_ModelTimelineIssueId",
                        column: x => x.ModelTimelineIssueId,
                        principalTable: "ModelTimelineIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssueMatches_ModelTrackingIssues_ModelTrackingIssueId",
                        column: x => x.ModelTrackingIssueId,
                        principalTable: "ModelTrackingIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueMatches_ModelBuildAttemptId",
                table: "ModelTrackingIssueMatches",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueMatches_ModelTestResultId",
                table: "ModelTrackingIssueMatches",
                column: "ModelTestResultId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueMatches_ModelTimelineIssueId",
                table: "ModelTrackingIssueMatches",
                column: "ModelTimelineIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueMatches_ModelTrackingIssueId",
                table: "ModelTrackingIssueMatches",
                column: "ModelTrackingIssueId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTrackingIssueMatches");
        }
    }
}
