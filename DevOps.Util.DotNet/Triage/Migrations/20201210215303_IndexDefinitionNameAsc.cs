using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class IndexDefinitionNameAsc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                sql: "CREATE NONCLUSTERED INDEX [IX_ModelBuildDefinition_DefinitionName] ON [dbo].[ModelBuildDefinitions]([DefinitionName] ASC) INCLUDE (AzureOrganization, AzureProject, DefinitionId)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                sql: "DROP INDEX [IX_ModelBuildDefinition_DefinitionName] ON [dbo].[ModelBuildDefinitions]");
        }
    }
}
