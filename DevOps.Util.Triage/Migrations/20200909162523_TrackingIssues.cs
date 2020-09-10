using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.Triage.Migrations
{
    public partial class TrackingIssues : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTimelineMissing",
                table: "ModelBuildAttempts",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ModelTrackingIssues",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackingKind = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    SearchRegexText = table.Column<string>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    GitHubOrganization = table.Column<string>(nullable: true),
                    GitHubRepository = table.Column<string>(nullable: true),
                    GitHubIssueNumber = table.Column<int>(nullable: true),
                    ModelBuildDefinitionId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTrackingIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssues_ModelBuildDefinitions_ModelBuildDefinitionId",
                        column: x => x.ModelBuildDefinitionId,
                        principalTable: "ModelBuildDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelTrackingIssueResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsPresent = table.Column<bool>(nullable: false),
                    ModelTrackingIssueId = table.Column<int>(nullable: false),
                    ModelBuildAttemptId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTrackingIssueResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssueResults_ModelBuildAttempts_ModelBuildAttemptId",
                        column: x => x.ModelBuildAttemptId,
                        principalTable: "ModelBuildAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelTrackingIssueResults_ModelTrackingIssues_ModelTrackingIssueId",
                        column: x => x.ModelTrackingIssueId,
                        principalTable: "ModelTrackingIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueResults_ModelBuildAttemptId",
                table: "ModelTrackingIssueResults",
                column: "ModelBuildAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssueResults_ModelTrackingIssueId_ModelBuildAttemptId",
                table: "ModelTrackingIssueResults",
                columns: new[] { "ModelTrackingIssueId", "ModelBuildAttemptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelTrackingIssues_ModelBuildDefinitionId",
                table: "ModelTrackingIssues",
                column: "ModelBuildDefinitionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelTrackingIssueResults");

            migrationBuilder.DropTable(
                name: "ModelTrackingIssues");

            migrationBuilder.DropColumn(
                name: "IsTimelineMissing",
                table: "ModelBuildAttempts");
        }
    }
}
