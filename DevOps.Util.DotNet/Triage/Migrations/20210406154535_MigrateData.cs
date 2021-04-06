using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevOps.Util.DotNet.Triage.Migrations
{
    public partial class MigrateData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelMigrations",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationKind = table.Column<int>(nullable: false),
                    OldId = table.Column<int>(nullable: false),
                    NewId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelMigrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelMigrations_MigrationKind_OldId",
                table: "ModelMigrations",
                columns: new[] { "MigrationKind", "OldId" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "NewId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelMigrations");
        }
    }
}
