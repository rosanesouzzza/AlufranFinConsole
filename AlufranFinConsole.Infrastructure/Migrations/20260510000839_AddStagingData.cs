using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlufranFinConsole.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStagingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StagingData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportFile_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RawData = table.Column<string>(type: "TEXT", nullable: false),
                    ParsedData = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ValidationErrors = table.Column<string>(type: "TEXT", nullable: false),
                    SanitizedData = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagingData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StagingData_ImportFiles_ImportFile_Id",
                        column: x => x.ImportFile_Id,
                        principalTable: "ImportFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StagingData_ImportFile_Id",
                table: "StagingData",
                column: "ImportFile_Id");

            migrationBuilder.CreateIndex(
                name: "IX_StagingData_ValidationStatus",
                table: "StagingData",
                column: "ValidationStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StagingData");
        }
    }
}
