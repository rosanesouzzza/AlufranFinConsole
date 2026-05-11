using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlufranFinConsole.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClosingApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClosingApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Competence = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    DreSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingApprovals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosingApprovals_Competence",
                table: "ClosingApprovals",
                column: "Competence");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingApprovals_Competence_Status",
                table: "ClosingApprovals",
                columns: new[] { "Competence", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosingApprovals");
        }
    }
}
