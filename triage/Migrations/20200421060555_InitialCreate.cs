using Microsoft.EntityFrameworkCore.Migrations;

namespace triage.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AzureOrganization = table.Column<string>(nullable: true),
                    AzureProject = table.Column<string>(nullable: true),
                    BuildNumber = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedBuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimelineIssues",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    GitHubOrganization = table.Column<string>(nullable: false),
                    GitHubRepository = table.Column<string>(nullable: false),
                    IssueId = table.Column<int>(nullable: false),
                    SearchText = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimelineEntries",
                columns: table => new
                {
                    BuildKey = table.Column<string>(nullable: false),
                    AzureOrganization = table.Column<string>(nullable: false),
                    AzureProject = table.Column<string>(nullable: false),
                    BuildNumber = table.Column<int>(nullable: false),
                    TimelineRecordName = table.Column<string>(nullable: true),
                    Line = table.Column<string>(nullable: true),
                    TimelineIssueId = table.Column<int>(nullable: false),
                    TimelineIssueId1 = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineEntries", x => x.BuildKey);
                    table.ForeignKey(
                        name: "FK_TimelineEntries_TimelineIssues_TimelineIssueId1",
                        column: x => x.TimelineIssueId1,
                        principalTable: "TimelineIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineEntries_TimelineIssueId1",
                table: "TimelineEntries",
                column: "TimelineIssueId1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedBuilds");

            migrationBuilder.DropTable(
                name: "TimelineEntries");

            migrationBuilder.DropTable(
                name: "TimelineIssues");
        }
    }
}
