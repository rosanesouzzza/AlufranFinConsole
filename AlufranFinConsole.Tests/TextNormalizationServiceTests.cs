using AlufranFinConsole.Application.Services;

namespace AlufranFinConsole.Tests;

public class TextNormalizationServiceTests
{
    private readonly ITextNormalizationService _sut = new TextNormalizationService();

    [Fact]
    public void NullInput_ReturnsNull()
        => _sut.NormalizeKey(null).Should().BeNull();

    [Fact]
    public void EmptyString_ReturnsNull()
        => _sut.NormalizeKey("").Should().BeNull();

    [Fact]
    public void WhitespaceOnly_ReturnsNull()
        => _sut.NormalizeKey("   ").Should().BeNull();

    [Fact]
    public void Char160_IsNormalized()
    {
        var input = "ORION REFEICOES"; // CHAR(160) entre as palavras
        _sut.NormalizeKey(input).Should().Be("ORION REFEICOES");
    }

    [Fact]
    public void DuplicateSpaces_AreCollapsed()
        => _sut.NormalizeKey("A  B   C").Should().Be("A B C");

    [Fact]
    public void AccentsRemoved_UppercaseAccented()
    {
        _sut.NormalizeKey("ÁLVARO").Should().Be("ALVARO");
        _sut.NormalizeKey("Ções").Should().Be("COES");
        _sut.NormalizeKey("émile").Should().Be("EMILE");
    }

    [Fact]
    public void AllAccentClasses_AreSubstituted()
    {
        _sut.NormalizeKey("áàãâä").Should().Be("AAAAA");
        _sut.NormalizeKey("éèêë").Should().Be("EEEE");
        _sut.NormalizeKey("íìîï").Should().Be("IIII");
        _sut.NormalizeKey("óòõôö").Should().Be("OOOOO");
        _sut.NormalizeKey("úùûü").Should().Be("UUUU");
        _sut.NormalizeKey("ç").Should().Be("C");
        _sut.NormalizeKey("ñ").Should().Be("N");
    }

    [Fact]
    public void HyphenVariants_NormalizedToSimpleHyphen()
    {
        // en-dash, em-dash, minus sign
        _sut.NormalizeKey("A–B").Should().Be("A-B");
        _sut.NormalizeKey("A—B").Should().Be("A-B");
        _sut.NormalizeKey("A−B").Should().Be("A-B");
    }

    [Fact]
    public void OutputIsUppercase()
        => _sut.NormalizeKey("saneamento financeiro").Should().Be("SANEAMENTO FINANCEIRO");

    [Fact]
    public void Trim_RemovesLeadingTrailingSpaces()
        => _sut.NormalizeKey("  empresa  ").Should().Be("EMPRESA");

    [Fact]
    public void NormalizeForKey_EmptyStringFallback()
        => _sut.NormalizeForKey(null).Should().Be(string.Empty);
}
