using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelBuildDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzureOrganization = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    AzureProject = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DefinitionName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DefinitionId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuildDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameKey = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    BuildNumber = table.Column<int>(nullable: false),
                    AzureOrganization = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    AzureProject = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    GitHubOrganization = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    GitHubRepository = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    PullRequestNumber = table.Column<int>(nullable: true),
                    QueueTime = table.Column<DateTime>(nullable: false),
                    FinishTime = table.Column<DateTime>(nullable: true),
                    StartTime = table.Column<DateTime>(nullable: false),
                    BuildResult = table.Column<int>(nullable: false),
                    BuildKind = table.Column<int>(nullable: false),
                    GitHubTargetBranch = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    DefinitionName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DefinitionId = table.Column<int>(nullable: false),
                    ModelBuildDefinitionId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuilds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelBuilds_ModelBuildDefinitions_ModelBuildDefinitionId",
                        column: x => x.ModelBuildDefinitionId,
                        principalTable: "ModelBuildDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelTrackingIssues",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackingKind = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    SearchQuery = table.Column<string>(nullable: false),
                    IssueTitle = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    GitHubOrganization = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    GitHubRepository = table.Column<string>(type: "nvarchar(100)", nullable: false),
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
                name: "ModelBuildAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Attempt = table.Column<int>(nullable: false),
                    IsTimelineMissing = table.Column<bool>(nullable: false),
                    FinishTime = table.Column<DateTime>(nullable: true),
                    NameKey = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false),
                    StartTime = table.Column<DateTime>(nullable: false),
                    BuildResult = table.Column<int>(nullable: false),
                    BuildKind = table.Column<int>(nullable: false),
                    DefinitionName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DefinitionId = table.Column<int>(nullable: false),
                    ModelBuildDefinitionId = table.Column<int>(nullable: false),
                    GitHubTargetBranch = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelBuildAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelBuildAttempts_ModelBuildDefinitions_ModelBuildDefinitionId",
                        column: x => x.ModelBuildDefinitionId,
                        principalTable: "ModelBuildDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModelBuildAttempts_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelGitHubIssues",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Organization = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Repository = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Number = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelGitHubIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelGitHubIssues_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelOsxDeprovisionRetry",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OsxJobFailedCount = table.Column<int>(nullable: false),
                    JobFailedCount = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelOsxDeprovisionRetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelOsxDeprovisionRetry_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelTestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestRunId = table.Column<int>(nullable: false),
                    Attempt = table.Column<int>(nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTestRuns_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelTimelineIssues",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Attempt = table.Column<int>(nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    RecordName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    Message = table.Column<string>(nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(12)", nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false),
                    StartTime = table.Column<DateTime>(nullable: false),
                    BuildResult = table.Column<int>(nullable: false),
                    BuildKind = table.Column<int>(nullable: false),
                    DefinitionName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DefinitionId = table.Column<int>(nullable: false),
                    ModelBuildDefinitionId = table.Column<int>(nullable: false),
                    GitHubTargetBranch = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTimelineIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTimelineIssues_ModelBuildDefinitions_ModelBuildDefinitionId",
                        column: x => x.ModelBuildDefinitionId,
                        principalTable: "ModelBuildDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModelTimelineIssues_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "ModelTestResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestFullName = table.Column<string>(nullable: false),
                    TestRunName = table.Column<string>(nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    IsSubResult = table.Column<bool>(nullable: false),
                    IsSubResultContainer = table.Column<bool>(nullable: false),
                    IsHelixTestResult = table.Column<bool>(nullable: false),
                    HelixConsoleUri = table.Column<string>(nullable: true),
                    HelixRunClientUri = table.Column<string>(nullable: true),
                    HelixCoreDumpUri = table.Column<string>(nullable: true),
                    HelixTestResultsUri = table.Column<string>(nullable: true),
                    ErrorMessage = table.Column<string>(nullable: false),
                    ModelTestRunId = table.Column<int>(nullable: false),
                    ModelBuildId = table.Column<int>(nullable: false),
                    StartTime = table.Column<DateTime>(nullable: false),
                    BuildResult = table.Column<int>(nullable: false),
                    BuildKind = table.Column<int>(nullable: false),
                    DefinitionName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DefinitionId = table.Column<int>(nullable: false),
                    ModelBuildDefinitionId = table.Column<int>(nullable: false),
                    GitHubTargetBranch = table.Column<string>(type: "nvarchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelTestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelTestResults_ModelBuildDefinitions_ModelBuildDefinitionId",
                        column: x => x.ModelBuildDefinitionId,
                        principalTable: "ModelBuildDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModelTestResults_ModelBuilds_ModelBuildId",
                        column: x => x.ModelBuildId,
                        principalTable: "ModelBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelTestResults_ModelTestRuns_ModelTestRunId",
                        column: x => x.ModelTestRunId,
                        principalTable: "ModelTestRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ModelTrackingIssueMatches",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    ModelTrackingIssueId = table.Column<int>(nullable: false),
                    ModelBuildAttemptId = table.Column<int>(nullable: false),
                    ModelTestResultId = table.Column<int>(nullable: true),
                    ModelTimelineIssueId = table.Column<int>(nullable: true),
                    HelixLogKind = table.Column<string>(nullable: false),
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
                name: "IX_ModelBuildAttempts_ModelBuildDefinitionId",
                table: "ModelBuildAttempts",
                column: "ModelBuildDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_ModelBuildId_Attempt",
                table: "ModelBuildAttempts",
                columns: new[] { "ModelBuildId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_NameKey_Attempt",
                table: "ModelBuildAttempts",
                columns: new[] { "NameKey", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionId",
                table: "ModelBuildAttempts",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildAttempts_StartTime_DefinitionName",
                table: "ModelBuildAttempts",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuildDefinitions_AzureOrganization_AzureProject_DefinitionId",
                table: "ModelBuildDefinitions",
                columns: new[] { "AzureOrganization", "AzureProject", "DefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_ModelBuildDefinitionId",
                table: "ModelBuilds",
                column: "ModelBuildDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_NameKey",
                table: "ModelBuilds",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionId",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelBuilds_StartTime_DefinitionName",
                table: "ModelBuilds",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelGitHubIssues_ModelBuildId",
                table: "ModelGitHubIssues",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelGitHubIssues_Number_Organization_Repository",
                table: "ModelGitHubIssues",
                columns: new[] { "Number", "Organization", "Repository" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelGitHubIssues_Organization_Repository_Number_ModelBuildId",
                table: "ModelGitHubIssues",
                columns: new[] { "Organization", "Repository", "Number", "ModelBuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelOsxDeprovisionRetry_ModelBuildId",
                table: "ModelOsxDeprovisionRetry",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildDefinitionId",
                table: "ModelTestResults",
                column: "ModelBuildDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelBuildId",
                table: "ModelTestResults",
                column: "ModelBuildId")
                .Annotation("SqlServer:Include", new[] { "TestFullName", "TestRunName", "IsHelixTestResult" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_ModelTestRunId",
                table: "ModelTestResults",
                column: "ModelTestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionId",
                table: "ModelTestResults",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestResults_StartTime_DefinitionName",
                table: "ModelTestResults",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTestRuns_ModelBuildId",
                table: "ModelTestRuns",
                column: "ModelBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildDefinitionId",
                table: "ModelTimelineIssues",
                column: "ModelBuildDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId",
                table: "ModelTimelineIssues",
                column: "ModelBuildId")
                .Annotation("SqlServer:Include", new[] { "JobName", "TaskName", "RecordName", "IssueType", "Attempt", "Message" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_ModelBuildId_Attempt",
                table: "ModelTimelineIssues",
                columns: new[] { "ModelBuildId", "Attempt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionId",
                table: "ModelTimelineIssues",
                columns: new[] { "StartTime", "DefinitionId" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelTimelineIssues_StartTime_DefinitionName",
                table: "ModelTimelineIssues",
                columns: new[] { "StartTime", "DefinitionName" })
                .Annotation("SqlServer:Include", new[] { "BuildResult", "BuildKind", "GitHubTargetBranch" });

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
                name: "ModelGitHubIssues");

            migrationBuilder.DropTable(
                name: "ModelOsxDeprovisionRetry");

            migrationBuilder.DropTable(
                name: "ModelTrackingIssueMatches");

            migrationBuilder.DropTable(
                name: "ModelTrackingIssueResults");

            migrationBuilder.DropTable(
                name: "ModelTestResults");

            migrationBuilder.DropTable(
                name: "ModelTimelineIssues");

            migrationBuilder.DropTable(
                name: "ModelBuildAttempts");

            migrationBuilder.DropTable(
                name: "ModelTrackingIssues");

            migrationBuilder.DropTable(
                name: "ModelTestRuns");

            migrationBuilder.DropTable(
                name: "ModelBuilds");

            migrationBuilder.DropTable(
                name: "ModelBuildDefinitions");
        }
    }
}
