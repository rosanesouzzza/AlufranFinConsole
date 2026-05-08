using System.Text.RegularExpressions;

namespace AlufranFinConsole.Application.Services;

public interface ITextNormalizationService
{
    string NormalizeForKey(string text);
}

public class TextNormalizationService : ITextNormalizationService
{
    public string NormalizeForKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove null char and CHAR(160)
        text = text.Replace("\0", "").Replace(" ", " ");

        // Trim and replace multiple spaces
        text = Regex.Replace(text.Trim(), @"\s+", " ");

        // Remove accents safely
        text = RemoveAccents(text);

        // Uppercase
        text = text.ToUpper();

        // Remove special chars but keep hyphen and underscore
        text = Regex.Replace(text, @"[^A-Z0-9\-_\s]", "");

        // Trim again
        return text.Trim();
    }

    private string RemoveAccents(string text)
    {
        var map = new Dictionary<char, string>
        {
            { 'á', "a" }, { 'à', "a" }, { 'ã', "a" }, { 'â', "a" }, { 'ä', "a" },
            { 'é', "e" }, { 'è', "e" }, { 'ê', "e" }, { 'ë', "e" },
            { 'í', "i" }, { 'ì', "i" }, { 'î', "i" }, { 'ï', "i" },
            { 'ó', "o" }, { 'ò', "o" }, { 'õ', "o" }, { 'ô', "o" }, { 'ö', "o" },
            { 'ú', "u" }, { 'ù', "u" }, { 'û', "u" }, { 'ü', "u" },
            { 'ç', "c" }, { 'ñ', "n" }
        };

        var result = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            result.Append(map.ContainsKey(char.ToLower(c)) ? map[char.ToLower(c)] : c.ToString());
        }

        return result.ToString();
    }
}
