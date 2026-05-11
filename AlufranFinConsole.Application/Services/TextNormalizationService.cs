using System.Text;
using System.Text.RegularExpressions;

namespace AlufranFinConsole.Application.Services;

public interface ITextNormalizationService
{
    /// <summary>
    /// Normaliza valor para uso como chave de join e comparação.
    /// Retorna null quando a entrada for nula ou branca — spec §7.
    /// </summary>
    string? NormalizeKey(string? value);

    /// <summary>Atalho para compatibilidade com código legado (retorna string.Empty em vez de null).</summary>
    string NormalizeForKey(string? text);
}

public class TextNormalizationService : ITextNormalizationService
{
    // Mapa explícito de substituição de acentos — spec §7
    private static readonly Dictionary<char, char> AccentMap = new()
    {
        { 'á','a' }, { 'à','a' }, { 'ã','a' }, { 'â','a' }, { 'ä','a' },
        { 'Á','A' }, { 'À','A' }, { 'Ã','A' }, { 'Â','A' }, { 'Ä','A' },
        { 'é','e' }, { 'è','e' }, { 'ê','e' }, { 'ë','e' },
        { 'É','E' }, { 'È','E' }, { 'Ê','E' }, { 'Ë','E' },
        { 'í','i' }, { 'ì','i' }, { 'î','i' }, { 'ï','i' },
        { 'Í','I' }, { 'Ì','I' }, { 'Î','I' }, { 'Ï','I' },
        { 'ó','o' }, { 'ò','o' }, { 'õ','o' }, { 'ô','o' }, { 'ö','o' },
        { 'Ó','O' }, { 'Ò','O' }, { 'Õ','O' }, { 'Ô','O' }, { 'Ö','O' },
        { 'ú','u' }, { 'ù','u' }, { 'û','u' }, { 'ü','u' },
        { 'Ú','U' }, { 'Ù','U' }, { 'Û','U' }, { 'Ü','U' },
        { 'ç','c' }, { 'Ç','C' },
        { 'ñ','n' }, { 'Ñ','N' },
    };

    // Unicode hyphens/dashes → ASCII hyphen-minus
    private static readonly char[] HyphenVariants =
        ['‐','‑','‒','–','—','―','−','﹘','﹣','－'];

    /// <inheritdoc/>
    public string? NormalizeKey(string? value)
    {
        // Passo 1 — nulo/branco → nulo
        if (value is null) return null;

        var sb = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            // Passo 2 — CHAR(160) não separável → espaço normal
            if (c == ' ') { sb.Append(' '); continue; }

            // Passo 6 — hífens variantes → hífen simples
            if (Array.IndexOf(HyphenVariants, c) >= 0) { sb.Append('-'); continue; }

            // Passo 7 — acentos por mapa explícito
            if (AccentMap.TryGetValue(c, out var mapped)) { sb.Append(mapped); continue; }

            sb.Append(c);
        }

        // Passo 3 — Trim
        var text = sb.ToString().Trim();

        // Retorna nulo se ficou branco após trim
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Passo 4 — espaços duplicados
        text = Regex.Replace(text, @"\s+", " ");

        // Passo 5 — maiúsculas
        text = text.ToUpperInvariant();

        return text;
    }

    /// <inheritdoc/>
    public string NormalizeForKey(string? text) => NormalizeKey(text) ?? string.Empty;
}
