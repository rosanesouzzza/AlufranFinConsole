using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlufranFinConsole.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateUsersToIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassificationRules_Users_CreatedBy_Id",
                table: "ClassificationRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportFiles_Users_UploadedBy_Id",
                table: "ImportFiles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ImportFiles_UploadedBy_Id",
                table: "ImportFiles");

            migrationBuilder.DropIndex(
                name: "IX_ClassificationRules_CreatedBy_Id",
                table: "ClassificationRules");

            migrationBuilder.AlterColumn<string>(
                name: "UploadedBy_Id",
                table: "ImportFiles",
                type: "TEXT",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "FileSize",
                table: "ImportFiles",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy_Id",
                table: "ClassificationRules",
                type: "TEXT",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // Add foreign keys to AspNetUsers
            migrationBuilder.AddForeignKey(
                name: "FK_ImportFiles_AspNetUsers_UploadedBy_Id",
                table: "ImportFiles",
                column: "UploadedBy_Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassificationRules_AspNetUsers_CreatedBy_Id",
                table: "ClassificationRules",
                column: "CreatedBy_Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Recreate indexes for performance
            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_UploadedBy_Id",
                table: "ImportFiles",
                column: "UploadedBy_Id");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_CreatedBy_Id",
                table: "ClassificationRules",
                column: "CreatedBy_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign keys and indexes added in Up()
            migrationBuilder.DropForeignKey(
                name: "FK_ImportFiles_AspNetUsers_UploadedBy_Id",
                table: "ImportFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassificationRules_AspNetUsers_CreatedBy_Id",
                table: "ClassificationRules");

            migrationBuilder.DropIndex(
                name: "IX_ImportFiles_UploadedBy_Id",
                table: "ImportFiles");

            migrationBuilder.DropIndex(
                name: "IX_ClassificationRules_CreatedBy_Id",
                table: "ClassificationRules");

            migrationBuilder.AlterColumn<int>(
                name: "UploadedBy_Id",
                table: "ImportFiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<long>(
                name: "FileSize",
                table: "ImportFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy_Id",
                table: "ClassificationRules",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 450);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_UploadedBy_Id",
                table: "ImportFiles",
                column: "UploadedBy_Id");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_CreatedBy_Id",
                table: "ClassificationRules",
                column: "CreatedBy_Id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassificationRules_Users_CreatedBy_Id",
                table: "ClassificationRules",
                column: "CreatedBy_Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportFiles_Users_UploadedBy_Id",
                table: "ImportFiles",
                column: "UploadedBy_Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
