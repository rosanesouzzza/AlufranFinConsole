using AlufranFinConsole.Application.Services;

namespace AlufranFinConsole.Tests;

/// <summary>
/// Testes das regras de QA por tipo de base — spec §10-17.
/// </summary>
public class QaRulesTests
{
    private readonly IQaIssueService _sut = new QaIssueService();
    private const string Comp = "2026-04";

    private static IReadOnlyDictionary<string, string> Row(params (string k, string v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => p.v);

    // ── PAG ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Pag_EmptyCategory_GeneratesBlockingQa()
    {
        var issues = _sut.Check(Row(("SupplierName","ORION"),("TitleAmount","1000")), "PAG", Comp);
        issues.Should().Contain(i => i.IssueType == "UnclassifiedCategory" && i.Severity == "Blocking");
    }

    [Fact]
    public void Pag_EmptySupplierWithValue_GeneratesBlockingQa()
    {
        var issues = _sut.Check(Row(("TitleAmount","1000"),("ErpCategoryName","ALIMENTOS")), "PAG", Comp);
        issues.Should().Contain(i => i.IssueType == "SupplierNotMapped" && i.Severity == "Blocking");
    }

    [Fact]
    public void Pag_PaidWithoutPaymentDate_GeneratesWarning()
    {
        var issues = _sut.Check(
            Row(("SupplierName","ORION"),("TitleAmount","1000"),("PaidAmount","1000"),
                ("TitleStatus","PAGO"),("ErpCategoryName","CAT")),
            "PAG", Comp);
        issues.Should().Contain(i => i.IssueType == "MissingDocument");
    }

    [Fact]
    public void Pag_InvalidDueDate_GeneratesBlocking()
    {
        var issues = _sut.Check(
            Row(("SupplierName","ORION"),("TitleAmount","1000"),("ErpCategoryName","CAT"),
                ("DueDate","NOT-A-DATE")),
            "PAG", Comp);
        issues.Should().Contain(i => i.IssueType == "InvalidDate" && i.Severity == "Blocking");
    }

    // ── REC ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Rec_EmptyClientWithValue_GeneratesBlocking()
    {
        var issues = _sut.Check(Row(("TitleAmount","500"),("ErpCategoryName","CAT")), "REC", Comp);
        issues.Should().Contain(i => i.IssueType == "ClientNotMapped" && i.Severity == "Blocking");
    }

    // ── EMITIDAS ─────────────────────────────────────────────────────────────

    [Fact]
    public void Emitidas_CancelledWithAmount_GeneratesBlocking()
    {
        var issues = _sut.Check(
            Row(("InvoiceNumber","NF001"),("ClientName","CLIENTE"),("GrossAmount","1000"),
                ("InvoiceStatus","CANCELADA")),
            "EMITIDAS", Comp);
        issues.Should().Contain(i => i.IssueType == "CancelledWithAmount" && i.Severity == "Blocking");
    }

    [Fact]
    public void Emitidas_MissingInvoiceNumber_GeneratesBlocking()
    {
        var issues = _sut.Check(Row(("ClientName","CLIENTE"),("GrossAmount","1000")), "EMITIDAS", Comp);
        issues.Should().Contain(i => i.IssueType == "MissingDocument" && i.Severity == "Blocking");
    }

    // ── TRANSF ───────────────────────────────────────────────────────────────

    [Fact]
    public void Transf_SameOriginDestination_GeneratesBlocking()
    {
        var issues = _sut.Check(
            Row(("SourceAccount","CC001"),("DestinationAccount","CC001"),("TransferAmount","1000")),
            "TRANSF", Comp);
        issues.Should().Contain(i => i.IssueType == "InvalidAmount" && i.Severity == "Blocking");
    }

    [Fact]
    public void Transf_ClassifiedAsReceita_GeneratesBlocking()
    {
        var issues = _sut.Check(
            Row(("SourceAccount","CC001"),("DestinationAccount","CC002"),
                ("TransferAmount","1000"),("TransferType","RECEITA")),
            "TRANSF", Comp);
        issues.Should().Contain(i => i.Severity == "Blocking");
    }

    // ── FOPAG ────────────────────────────────────────────────────────────────

    [Fact]
    public void Fopag_EmptyCompetence_GeneratesBlocking()
    {
        var issues = _sut.Check(
            Row(("PayrollItemName","SALARIO"),("PayrollAmount","3000")),
            "FOPAG", Comp);
        issues.Should().Contain(i => i.IssueType == "MissingRequiredField" && i.Severity == "Blocking");
    }

    // ── Clean row — sem issues ────────────────────────────────────────────────

    [Fact]
    public void CleanPagRow_NoIssues()
    {
        var issues = _sut.Check(
            Row(("SupplierName","ORION"),("TitleAmount","1000"),
                ("ErpCategoryName","ALIMENTOS"),("CompanyName","EMPRESA"),
                ("UnitName","UNIDADE"),("DocumentNumber","DOC001")),
            "PAG", Comp);
        issues.Should().BeEmpty();
    }
}
