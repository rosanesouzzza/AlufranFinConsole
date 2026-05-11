using AlufranFinConsole.Application.Services;

namespace AlufranFinConsole.Tests;

public class DiscardRulesTests
{
    private readonly IDiscardService _sut = new DiscardService();

    private static Dictionary<string, string> Row(params (string k, string v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => p.v);

    [Fact]
    public void EmptyRow_IsDiscarded()
    {
        var check = _sut.Check(Row(), "PAG");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("EmptyRow");
    }

    [Fact]
    public void AllBlankValues_IsDiscarded()
    {
        var check = _sut.Check(Row(("A",""),("B","  ")), "PAG");
        check.ShouldDiscard.Should().BeTrue();
    }

    [Fact]
    public void RepeatedHeader_IsDiscarded()
    {
        // linha cujo conteúdo contém palavras de cabeçalho
        var check = _sut.Check(
            Row(("col1","FORNECEDOR"),("col2","CLIENTE"),("col3","DOCUMENTO"),("col4","VALOR")),
            "PAG");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("RepeatedHeader");
    }

    [Fact]
    public void TotalGeral_IsDiscarded()
    {
        var check = _sut.Check(Row(("SupplierName","TOTAL GERAL"),("TitleAmount","1000")), "PAG");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("GrandTotal");
    }

    [Fact]
    public void Subtotal_IsDiscarded()
    {
        var check = _sut.Check(Row(("SupplierName","SUBTOTAL"),("TitleAmount","500")), "PAG");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("Subtotal");
    }

    [Fact]
    public void MissingFinancialValue_PAG_IsDiscarded()
    {
        var check = _sut.Check(
            Row(("SupplierName","ORION"),("TitleAmount","0"),("PaidAmount","0")),
            "PAG");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("MissingFinancialValue");
    }

    [Fact]
    public void VisualSeparator_IsDiscarded()
    {
        var check = _sut.Check(Row(("col1","-----")), "REC");
        check.ShouldDiscard.Should().BeTrue();
        check.Reason.Should().Be("VisualSeparator");
    }

    [Fact]
    public void ValidRow_IsNotDiscarded()
    {
        var check = _sut.Check(
            Row(("SupplierName","ORION REFEICOES"),("TitleAmount","1500.00")),
            "PAG");
        check.ShouldDiscard.Should().BeFalse();
    }
}
