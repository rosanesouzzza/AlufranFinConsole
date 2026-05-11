using AlufranFinConsole.Application.Services;

namespace AlufranFinConsole.Tests;

/// <summary>
/// Valida a combinação Descarte + QA para cada tipo de base.
/// </summary>
public class SanitizationServiceTests
{
    private readonly IDiscardService _discard = new DiscardService();
    private readonly IQaIssueService _qa      = new QaIssueService();
    private const string Comp = "2026-04";

    private static IReadOnlyDictionary<string, string> R(params (string k, string v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => p.v);

    // ── PAG ──────────────────────────────────────────────────────────────────
    [Fact]
    public void Pag_ValidLine_NotDiscarded_NoQa()
    {
        var row = R(("SupplierName","ORION"),("TitleAmount","1500"),
                    ("ErpCategoryName","ALIMENTOS"),("CompanyName","EMP"),
                    ("UnitName","UN"),("DocumentNumber","DOC1"));
        _discard.Check(row,"PAG").ShouldDiscard.Should().BeFalse();
        _qa.Check(row,"PAG",Comp).Should().BeEmpty();
    }

    [Fact]
    public void Pag_TotalRow_IsDiscarded()
        => _discard.Check(R(("SupplierName","TOTAL GERAL"),("TitleAmount","9999")), "PAG")
            .ShouldDiscard.Should().BeTrue();

    [Fact]
    public void Pag_MissingCompany_GeneratesBlocking()
    {
        // CompanyNotMapped agora é Blocking — DRE e consolidação ficam comprometidos
        var row = R(("SupplierName","ORION"),("TitleAmount","1000"),
                    ("ErpCategoryName","ALIMENTOS"),("UnitName","UN"),("DocumentNumber","DOC1"));
        var issues = _qa.Check(row,"PAG",Comp);
        issues.Should().Contain(i => i.IssueType == "CompanyNotMapped" && i.Severity == "Blocking");
    }

    [Fact]
    public void Pag_MissingUnit_GeneratesBlocking()
    {
        // UnitNotMapped agora é Blocking — centro de custo comprometido
        var row = R(("SupplierName","ORION"),("TitleAmount","1000"),
                    ("ErpCategoryName","ALIMENTOS"),("CompanyName","EMP"),("DocumentNumber","DOC1"));
        var issues = _qa.Check(row,"PAG",Comp);
        issues.Should().Contain(i => i.IssueType == "UnitNotMapped" && i.Severity == "Blocking");
    }

    [Fact]
    public void Pag_PaidWithZeroAmount_GeneratesBlocking()
    {
        // isPaid && paidAmt == 0 agora é Blocking (inconsistência estrutural)
        var row = R(("SupplierName","ORION"),("TitleAmount","1000"),("PaidAmount","0"),
                    ("TitleStatus","PAGO"),("ErpCategoryName","CAT"),
                    ("CompanyName","EMP"),("UnitName","UN"));
        var issues = _qa.Check(row,"PAG",Comp);
        issues.Should().Contain(i => i.IssueType == "InvalidAmount" && i.Severity == "Blocking");
    }

    // ── REC ──────────────────────────────────────────────────────────────────
    [Fact]
    public void Rec_ValidLine_NotDiscarded()
    {
        var row = R(("ClientName","CLIENTE"),("TitleAmount","2000"),
                    ("ErpCategoryName","SERVICOS"),("CompanyName","EMP"),("UnitName","UN"));
        _discard.Check(row,"REC").ShouldDiscard.Should().BeFalse();
    }

    // ── EMITIDAS ─────────────────────────────────────────────────────────────
    [Fact]
    public void Emitidas_ZeroAmountCancelled_NotDiscarded_GoesToQa()
    {
        // Nota cancelada com GrossAmount=0: NÃO deve ser descartada (status isento).
        // A linha segue para QA — auditoria de conciliação exige rastreabilidade.
        var row = R(("InvoiceNumber","NF1"),("ClientName","CLI"),
                    ("GrossAmount","0"),("InvoiceStatus","CANCELADA"));
        _discard.Check(row,"EMITIDAS").ShouldDiscard.Should().BeFalse();
    }

    [Fact]
    public void Emitidas_ZeroAmountNoStatus_IsDiscarded()
    {
        // Sem status e sem valor: descartada por MissingFinancialValue
        var row = R(("InvoiceNumber","NF1"),("ClientName","CLI"),("GrossAmount","0"));
        var check = _discard.Check(row,"EMITIDAS");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("MissingFinancialValue");
    }

    // ── FAT ──────────────────────────────────────────────────────────────────
    [Fact]
    public void Fat_MissingTotal_IsDiscarded()
        => _discard.Check(R(("ClientName","CLI"),("TotalAmount","0")), "FAT")
            .ShouldDiscard.Should().BeTrue();

    // ── COMP ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Comp_EmptyCategory_GeneratesBlockingQa()
    {
        var row = R(("SupplierName","FORN"),("TotalAmount","800"));
        _discard.Check(row,"COMP").ShouldDiscard.Should().BeFalse(); // não descartado
        _qa.Check(row,"COMP",Comp)
            .Should().Contain(i => i.IssueType == "UnclassifiedCategory" && i.Severity == "Blocking");
    }

    // ── TRANSF ───────────────────────────────────────────────────────────────
    [Fact]
    public void Transf_ValidLine_NotDiscarded_NoQa()
    {
        var row = R(("SourceAccount","CC001"),("DestinationAccount","CC002"),
                    ("TransferAmount","5000"),("CompanyName","EMP"),("UnitName","UN"));
        _discard.Check(row,"TRANSF").ShouldDiscard.Should().BeFalse();
        _qa.Check(row,"TRANSF",Comp).Should().BeEmpty();
    }

    // ── FOPAG ────────────────────────────────────────────────────────────────
    [Fact]
    public void Fopag_ValidLine_NotDiscarded()
    {
        var row = R(("PayrollItemName","SALARIO"),("PayrollAmount","3000"),
                    ("CompetenceDate","2026-04-01"),("CompanyName","EMP"),("UnitName","UN"));
        _discard.Check(row,"FOPAG").ShouldDiscard.Should().BeFalse();
    }

    // ── InvalidStructure ──────────────────────────────────────────────────────
    [Fact]
    public void Pag_NoStructuralFields_IsInvalidStructure()
    {
        // Linha PAG sem nenhum campo estrutural mínimo
        var row = R(("ColunaDiversa","VALOR QUALQUER"),("OutraColuna","DADOS"));
        var check = _discard.Check(row,"PAG");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("InvalidStructure");
    }

    [Fact]
    public void Transf_NoAccounts_IsInvalidStructure()
    {
        var row = R(("Descricao","Transferencia"),("Valor","1000"));
        var check = _discard.Check(row,"TRANSF");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("InvalidStructure");
    }
}
