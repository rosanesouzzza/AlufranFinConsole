using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlufranFinConsole.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelinePhase7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TargetColumn",
                table: "ColumnMappings",
                newName: "TargetColumnName");

            migrationBuilder.RenameColumn(
                name: "SourceColumn",
                table: "ColumnMappings",
                newName: "SourceColumnName");

            migrationBuilder.RenameColumn(
                name: "FileType",
                table: "ColumnMappings",
                newName: "BaseType");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ErpCategories",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "DreGroup",
                table: "ErpCategories",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DreOrder",
                table: "ErpCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DreSubgroup",
                table: "ErpCategories",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ColumnMappings",
                type: "TEXT",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "ColumnMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShouldKeep",
                table: "ColumnMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TransformationRule",
                table: "ColumnMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                table: "ClassificationRules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy_Id",
                table: "ClassificationRules",
                type: "TEXT",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<string>(
                name: "BaseType",
                table: "ClassificationRules",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "*");

            migrationBuilder.AddColumn<int>(
                name: "ChartOfAccount_Id",
                table: "ClassificationRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DreGroup",
                table: "ClassificationRules",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DreOrder",
                table: "ClassificationRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DreSubgroup",
                table: "ClassificationRules",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ErpCategory_Id",
                table: "ClassificationRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportFile_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy_Id = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileVersions_ImportFiles_ImportFile_Id",
                        column: x => x.ImportFile_Id,
                        principalTable: "ImportFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileVersion_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Competence = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidRows = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscardedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    QaRows = table.Column<int>(type: "INTEGER", nullable: false),
                    FactsGenerated = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    StartedBy_Id = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingRuns_FileVersions_FileVersion_Id",
                        column: x => x.FileVersion_Id,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscardedRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessingRun_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    FileVersion_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Competence = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    OriginalRowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false),
                    DiscardReason = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DiscardDetail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscardedRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscardedRows_ProcessingRuns_ProcessingRun_Id",
                        column: x => x.ProcessingRun_Id,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinancialFacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessingRun_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceStagingRow_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Competence = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Company_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Unit_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Supplier_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Client_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Service_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Product_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    ChartOfAccount_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    ErpCategory_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    DocumentNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AmountCompetence = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AmountCash = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DreGroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DreSubgroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DreOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    ClassificationStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialFacts_ProcessingRuns_ProcessingRun_Id",
                        column: x => x.ProcessingRun_Id,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QaIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessingRun_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    FileVersion_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Competence = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    OriginalRowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QaIssues_ProcessingRuns_ProcessingRun_Id",
                        column: x => x.ProcessingRun_Id,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StagingRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessingRun_Id = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Competence = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    ImportFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalRowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedJson = table.Column<string>(type: "TEXT", nullable: false),
                    LineHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LineStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagingRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StagingRows_ProcessingRuns_ProcessingRun_Id",
                        column: x => x.ProcessingRun_Id,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_BaseType_SourceColumnName",
                table: "ColumnMappings",
                columns: new[] { "BaseType", "SourceColumnName" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscardedRows_DiscardReason",
                table: "DiscardedRows",
                column: "DiscardReason");

            migrationBuilder.CreateIndex(
                name: "IX_DiscardedRows_ProcessingRun_Id",
                table: "DiscardedRows",
                column: "ProcessingRun_Id");

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_ImportFile_Id_VersionNumber",
                table: "FileVersions",
                columns: new[] { "ImportFile_Id", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialFacts_Competence_BaseType",
                table: "FinancialFacts",
                columns: new[] { "Competence", "BaseType" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialFacts_DreGroup",
                table: "FinancialFacts",
                column: "DreGroup");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialFacts_ProcessingRun_Id",
                table: "FinancialFacts",
                column: "ProcessingRun_Id");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingRuns_BaseType_Competence",
                table: "ProcessingRuns",
                columns: new[] { "BaseType", "Competence" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingRuns_FileVersion_Id",
                table: "ProcessingRuns",
                column: "FileVersion_Id");

            migrationBuilder.CreateIndex(
                name: "IX_QaIssues_ProcessingRun_Id",
                table: "QaIssues",
                column: "ProcessingRun_Id");

            migrationBuilder.CreateIndex(
                name: "IX_QaIssues_Severity_Status",
                table: "QaIssues",
                columns: new[] { "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StagingRows_LineHash",
                table: "StagingRows",
                column: "LineHash");

            migrationBuilder.CreateIndex(
                name: "IX_StagingRows_LineStatus",
                table: "StagingRows",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StagingRows_ProcessingRun_Id",
                table: "StagingRows",
                column: "ProcessingRun_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscardedRows");

            migrationBuilder.DropTable(
                name: "FinancialFacts");

            migrationBuilder.DropTable(
                name: "QaIssues");

            migrationBuilder.DropTable(
                name: "StagingRows");

            migrationBuilder.DropTable(
                name: "ProcessingRuns");

            migrationBuilder.DropTable(
                name: "FileVersions");

            migrationBuilder.DropIndex(
                name: "IX_ColumnMappings_BaseType_SourceColumnName",
                table: "ColumnMappings");

            migrationBuilder.DropColumn(
                name: "DreGroup",
                table: "ErpCategories");

            migrationBuilder.DropColumn(
                name: "DreOrder",
                table: "ErpCategories");

            migrationBuilder.DropColumn(
                name: "DreSubgroup",
                table: "ErpCategories");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "ColumnMappings");

            migrationBuilder.DropColumn(
                name: "ShouldKeep",
                table: "ColumnMappings");

            migrationBuilder.DropColumn(
                name: "TransformationRule",
                table: "ColumnMappings");

            migrationBuilder.DropColumn(
                name: "BaseType",
                table: "ClassificationRules");

            migrationBuilder.DropColumn(
                name: "ChartOfAccount_Id",
                table: "ClassificationRules");

            migrationBuilder.DropColumn(
                name: "DreGroup",
                table: "ClassificationRules");

            migrationBuilder.DropColumn(
                name: "DreOrder",
                table: "ClassificationRules");

            migrationBuilder.DropColumn(
                name: "DreSubgroup",
                table: "ClassificationRules");

            migrationBuilder.DropColumn(
                name: "ErpCategory_Id",
                table: "ClassificationRules");

            migrationBuilder.RenameColumn(
                name: "TargetColumnName",
                table: "ColumnMappings",
                newName: "TargetColumn");

            migrationBuilder.RenameColumn(
                name: "SourceColumnName",
                table: "ColumnMappings",
                newName: "SourceColumn");

            migrationBuilder.RenameColumn(
                name: "BaseType",
                table: "ColumnMappings",
                newName: "FileType");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ErpCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ColumnMappings",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                table: "ClassificationRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy_Id",
                table: "ClassificationRules",
                type: "TEXT",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
