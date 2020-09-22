using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class TimelineIssues : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelBuildAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Attempt = table.Column<int>(nullable: false),
                    StartTime = table.Column<DateTime>(type: "smalldatetime", nullable: true),
                    FinishTime = table.Column<DateTime>(type: "smalldatetime", nullable: true),
                    BuildResult = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuildAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelBuildAttempts_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineIssues",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Attempt = table.Column<int>(nullable: false),
                    JobName = table.Column<string>(nullable: true),
                    RecordName = table.Column<string>(nullable: true),
                    RecordId = table.Column<string>(nullable: true),
                    Message = table.Column<string>(nullable: true),
                    ModelBuildId = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    ModelBuildAttemptId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTimelineIssues_ModelBuildAttempts_ModelBuildAttemptId",
                        column: x => x.ModelBuildAttemptId,
                        principalTable: "ModelBuildAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelTimelineIssues_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_ModelBuildId",
                table: "ModelBuildAttempts",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildAttemptId",
                table: "ModelTimelineIssues",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId",
                table: "ModelTimelineIssues",
                column: "ModelBuildId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTimelineIssues");

            migrationBuilder.DropTable(
                name: "ModelBuildAttempts");
        }
    }
}
