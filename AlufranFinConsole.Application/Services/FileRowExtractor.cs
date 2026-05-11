using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Extracted row from a financial file.
/// ParsedDataJson is non-null for XLSX/PDF when column mapping succeeded — allows the
/// validation pipeline to skip CSV re-parsing.
/// </summary>
public record ExtractedRow(int LineNumber, string RawData, string? ParsedDataJson);

/// <summary>
/// Reads financial files (CSV, XLSX, PDF) and returns typed rows ready for staging.
/// Supports XLSX with flexible Portuguese ERP column-name mapping and PDF with
/// word-group line detection.
/// </summary>
public static class FileRowExtractor
{
    // ── Internal field ordering per file type (matches DataValidationService) ───

    private static readonly Dictionary<string, string[]> FieldOrder =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PAG"]     = ["documento", "fornecedor", "valor", "data_pagamento", "descricao"],
            ["REC"]     = ["documento", "cliente",    "valor", "data_recebimento", "descricao"],
            ["FAT"]     = ["numero_nf", "cliente",    "valor", "data_emissao",    "descricao"],
            ["EMITIDAS"]= ["numero_doc","cliente",    "valor", "data_vencimento", "data_emissao","status"],
            ["COMP"]    = ["numero_nf", "fornecedor", "valor", "data_entrada",    "categoria",  "descricao"],
            ["TRANSF"]  = ["numero_doc","conta_origem","conta_destino","valor",   "data",       "descricao"],
            ["FOPAG"]   = ["matricula", "funcionario","cargo",  "valor_bruto",    "descontos",  "valor_liquido","competencia"],
        };

    // ── Column-name alias table (lowercase key → internal field name) ────────

    private static readonly Dictionary<string, string> ColumnAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ─ documento / numero_doc (generic document number) ─
            ["documento"]              = "documento",
            ["doc"]                    = "documento",
            ["nr doc"]                 = "documento",
            ["nr. doc"]                = "documento",
            ["num doc"]                = "documento",
            ["numero doc"]             = "documento",
            ["número doc"]             = "documento",
            ["número do documento"]    = "documento",
            ["cod doc"]                = "documento",
            ["codigo"]                 = "documento",
            ["código"]                 = "documento",
            ["numero_doc"]             = "numero_doc",

            // ─ numero_nf ─
            ["numero nf"]              = "numero_nf",
            ["número nf"]              = "numero_nf",
            ["nf"]                     = "numero_nf",
            ["nota fiscal"]            = "numero_nf",
            ["nr nf"]                  = "numero_nf",
            ["nr nota"]                = "numero_nf",
            ["número nota"]            = "numero_nf",
            ["numero nota"]            = "numero_nf",
            ["nfe"]                    = "numero_nf",
            ["numero_nf"]              = "numero_nf",
            ["doc./série"]             = "numero_nf",
            ["doc/série"]              = "numero_nf",
            ["doc. /série"]            = "numero_nf",
            ["doc./serie"]             = "numero_nf",
            ["doc/serie"]              = "numero_nf",
            ["doc./serie"]             = "numero_nf",

            // ─ fornecedor ─
            ["fornecedor"]             = "fornecedor",
            ["nome fornecedor"]        = "fornecedor",
            ["razão social"]           = "fornecedor",
            ["razao social"]           = "fornecedor",
            ["empresa"]                = "fornecedor",

            // ─ filial (company branch — PAG ERP export) ─
            ["filial"]                 = "filial",
            ["fil"]                    = "filial",
            ["filial erp"]             = "filial",

            // ─ unidade (business unit / restaurant) ─
            ["unidade"]                = "unidade",
            ["und"]                    = "unidade",
            ["un"]                     = "unidade",
            ["unid"]                   = "unidade",
            ["c. custo"]               = "unidade",
            ["c.custo"]                = "unidade",
            ["centro de custo"]        = "unidade",
            ["centro custo"]           = "unidade",
            ["ctr custo"]              = "unidade",

            // ─ categoria_erp (ERP category — multiple source columns map here) ─
            ["categoria_erp"]          = "categoria_erp",
            ["tipo_de_conta"]          = "categoria_erp",
            ["tipo de conta"]          = "categoria_erp",
            ["conta_categoria"]        = "categoria_erp",
            ["conta categoria"]        = "categoria_erp",
            ["categoria erp"]          = "categoria_erp",
            ["cat erp"]                = "categoria_erp",
            ["cat. erp"]               = "categoria_erp",
            ["tipo conta"]             = "categoria_erp",
            ["tp conta"]               = "categoria_erp",

            // ─ cliente ─
            ["cliente"]                = "cliente",
            ["nome cliente"]           = "cliente",

            // ─ valor ─
            ["valor"]                  = "valor",
            ["valor pago"]             = "valor",
            ["valor pagamento"]        = "valor",
            ["vl pagamento"]           = "valor",
            ["vl pago"]                = "valor",
            ["vlr"]                    = "valor",
            ["vlr pago"]               = "valor",
            ["valor r$"]               = "valor",
            ["r$"]                     = "valor",
            ["valor total"]            = "valor",
            ["valor nf"]               = "valor",
            ["vl nf"]                  = "valor",
            ["vlr nf"]                 = "valor",
            ["total nf"]               = "valor",
            ["valor recebido"]         = "valor",
            ["vl recebimento"]         = "valor",
            ["valor titulo"]           = "valor",
            ["valor título"]           = "valor",
            ["valor duplicata"]        = "valor",
            ["valor compra"]           = "valor",
            ["vlr compra"]             = "valor",
            ["vl entrada"]             = "valor",
            ["total"]                  = "valor",
            ["valor transferencia"]    = "valor",
            ["valor transferência"]    = "valor",
            ["vl transf"]              = "valor",

            // ─ vl_líquido / valor líquido — PAG BD real file ─
            ["vl líquido"]             = "valor",
            ["vl liquido"]             = "valor",
            ["valor líquido"]          = "valor",
            ["valor liquido"]          = "valor",
            ["vlr líquido"]            = "valor",
            ["vlr liquido"]            = "valor",

            // ─ vr.líquido — PAG ERP export (Vr.Líquido column) ─
            ["vr.líquido"]             = "valor",
            ["vr. líquido"]            = "valor",
            ["vr.liquido"]             = "valor",
            ["vr. liquido"]            = "valor",
            ["vr líquido"]             = "valor",
            ["vr liquido"]             = "valor",

            // ─ vl_rateio (extra column — kept but dropped by ColumnMapping) ─
            ["vl rateio"]              = "vl_rateio",
            ["valor rateio"]           = "vl_rateio",
            ["vl_rateio"]              = "vl_rateio",
            ["vl rat"]                 = "vl_rateio",

            // ─ mes_vencimento (extra column in BD_PAG) ─
            ["mês vencimento"]         = "mes_vencimento",
            ["mes vencimento"]         = "mes_vencimento",
            ["mês venc"]               = "mes_vencimento",
            ["mes venc"]               = "mes_vencimento",
            ["mes/vencimento"]         = "mes_vencimento",

            // ─ grupo_dre (DRE group hint in BD files) ─
            ["grupo_dre"]              = "grupo_dre",
            ["grupo dre"]              = "grupo_dre",
            ["grp dre"]                = "grupo_dre",

            // ─ subgrupo (DRE subgroup hint) ─
            ["subgrupo"]               = "subgrupo",
            ["sub grupo"]              = "subgrupo",
            ["sub-grupo"]              = "subgrupo",

            // ─ data_pagamento ─
            ["data pagamento"]         = "data_pagamento",
            ["data pag"]               = "data_pagamento",
            ["dt pagamento"]           = "data_pagamento",
            ["dt pag"]                 = "data_pagamento",
            ["data do pagamento"]      = "data_pagamento",

            // ─ data_recebimento ─
            ["data recebimento"]       = "data_recebimento",
            ["data rec"]               = "data_recebimento",
            ["dt recebimento"]         = "data_recebimento",
            ["dt rec"]                 = "data_recebimento",
            ["data do recebimento"]    = "data_recebimento",

            // ─ data_emissao ─
            ["data emissao"]           = "data_emissao",
            ["data emissão"]           = "data_emissao",
            ["dt emissao"]             = "data_emissao",
            ["dt emissão"]             = "data_emissao",
            ["data emissao nf"]        = "data_emissao",
            ["data nota"]              = "data_emissao",
            ["emissao"]                = "data_emissao",
            ["emissão"]                = "data_emissao",

            // ─ data_entrada ─
            ["data entrada"]           = "data_entrada",
            ["dt entrada"]             = "data_entrada",
            ["data compra"]            = "data_entrada",
            ["dt compra"]              = "data_entrada",
            ["data nf"]                = "data_entrada",
            ["entrada"]                = "data_entrada",

            // ─ data_vencimento ─
            ["data vencimento"]        = "data_vencimento",
            ["dt vencimento"]          = "data_vencimento",
            ["vencimento"]             = "data_vencimento",
            ["dt venc"]                = "data_vencimento",
            ["venc"]                   = "data_vencimento",
            ["venc."]                  = "data_vencimento",

            // ─ data (genérico – TRANSF) ─
            ["data"]                   = "data",
            ["dt"]                     = "data",
            ["data transferencia"]     = "data",
            ["data transferência"]     = "data",
            ["dt transferencia"]       = "data",
            ["dt transferência"]       = "data",
            ["data transf"]            = "data",
            ["dt transf"]              = "data",

            // ─ conta_origem / conta_destino ─
            ["conta origem"]           = "conta_origem",
            ["ct origem"]              = "conta_origem",
            ["conta débito"]           = "conta_origem",
            ["conta debito"]           = "conta_origem",
            ["de"]                     = "conta_origem",
            ["origem"]                 = "conta_origem",
            ["conta destino"]          = "conta_destino",
            ["ct destino"]             = "conta_destino",
            ["conta crédito"]          = "conta_destino",
            ["conta credito"]          = "conta_destino",
            ["para"]                   = "conta_destino",
            ["destino"]                = "conta_destino",

            // ─ categoria ─
            ["categoria"]              = "categoria",
            ["grupo"]                  = "categoria",
            ["tipo"]                   = "categoria",
            ["classe"]                 = "categoria",
            ["natureza"]               = "categoria",
            ["centro custo"]           = "categoria",
            ["centro de custo"]        = "categoria",
            ["cc"]                     = "categoria",

            // ─ status ─
            ["status"]                 = "status",
            ["situacao"]               = "status",
            ["situação"]               = "status",
            ["sit"]                    = "status",

            // ─ descricao ─
            ["descricao"]              = "descricao",
            ["descrição"]              = "descricao",
            ["historico"]              = "descricao",
            ["histórico"]              = "descricao",
            ["obs"]                    = "descricao",
            ["observacao"]             = "descricao",
            ["observação"]             = "descricao",
            ["descr"]                  = "descricao",

            // ─ FOPAG: matricula ─
            ["matricula"]              = "matricula",
            ["matrícula"]              = "matricula",
            ["mat"]                    = "matricula",
            ["mat."]                   = "matricula",
            ["codigo funcionario"]     = "matricula",
            ["código funcionário"]     = "matricula",
            ["cod func"]               = "matricula",
            ["cód func"]               = "matricula",
            ["reg"]                    = "matricula",

            // ─ FOPAG: funcionario ─
            ["funcionario"]            = "funcionario",
            ["funcionário"]            = "funcionario",
            ["nome"]                   = "funcionario",
            ["nome funcionario"]       = "funcionario",
            ["nome funcionário"]       = "funcionario",
            ["colaborador"]            = "funcionario",
            ["empregado"]              = "funcionario",

            // ─ FOPAG: cargo ─
            ["cargo"]                  = "cargo",
            ["função"]                 = "cargo",
            ["funcao"]                 = "cargo",
            ["func"]                   = "cargo",
            ["ocupacao"]               = "cargo",
            ["ocupação"]               = "cargo",
            ["departamento"]           = "cargo",
            ["dept"]                   = "cargo",

            // ─ FOPAG: valor_bruto ─
            ["valor bruto"]            = "valor_bruto",
            ["salario bruto"]          = "valor_bruto",
            ["salário bruto"]          = "valor_bruto",
            ["sal bruto"]              = "valor_bruto",
            ["bruto"]                  = "valor_bruto",
            ["vl bruto"]               = "valor_bruto",
            ["vlr bruto"]              = "valor_bruto",
            ["proventos"]              = "valor_bruto",

            // ─ FOPAG: descontos ─
            ["descontos"]              = "descontos",
            ["desconto"]               = "descontos",
            ["total descontos"]        = "descontos",
            ["total desc"]             = "descontos",
            ["deducoes"]               = "descontos",
            ["deduções"]               = "descontos",
            ["desc"]                   = "descontos",

            // ─ FOPAG: valor_liquido ─
            ["valor liquido"]          = "valor_liquido",
            ["valor líquido"]          = "valor_liquido",
            ["sal liquido"]            = "valor_liquido",
            ["salário líquido"]        = "valor_liquido",
            ["salario liquido"]        = "valor_liquido",
            ["liquido"]                = "valor_liquido",
            ["líquido"]                = "valor_liquido",
            ["vl liquido"]             = "valor_liquido",
            ["vlr liquido"]            = "valor_liquido",

            // ─ FOPAG: competencia ─
            ["competencia"]            = "competencia",
            ["competência"]            = "competencia",
            ["periodo"]                = "competencia",
            ["período"]                = "competencia",
            ["comp"]                   = "competencia",
            ["mes"]                    = "competencia",
            ["mês"]                    = "competencia",
            ["referencia"]             = "competencia",
            ["referência"]             = "competencia",
        };

    // ── Public entry point ───────────────────────────────────────────────────

    /// <summary>
    /// Extracts rows from a CSV, XLSX or PDF file.
    /// For XLSX, ParsedDataJson is pre-populated so staging validation can bypass CSV parsing.
    /// </summary>
    public static List<ExtractedRow> Extract(string filePath, string fileType)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xls" => ExtractFromXlsx(filePath, fileType),
            ".pdf"            => ExtractFromPdf(filePath, fileType),
            _                 => ExtractFromCsv(filePath),
        };
    }

    // ── CSV ──────────────────────────────────────────────────────────────────

    private static List<ExtractedRow> ExtractFromCsv(string filePath)
    {
        var rows = new List<ExtractedRow>();
        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(line))
                rows.Add(new ExtractedRow(i + 1, line, null));
        }
        return rows;
    }

    // ── XLSX ─────────────────────────────────────────────────────────────────

    private static List<ExtractedRow> ExtractFromXlsx(string filePath, string fileType)
    {
        var rows = new List<ExtractedRow>();
        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow < 2 || lastCol < 1) return rows;

        // ── Auto-detect header row (handles ERP reports with title/version rows before headers) ──
        // Scan first 10 rows, pick the one with the most ColumnAliases matches.
        int headerRowNumber = 1;
        int maxAliasMatches = 0;
        for (int scanRow = 1; scanRow <= Math.Min(lastRow, 10); scanRow++)
        {
            var scanRowRef = ws.Row(scanRow);
            int matchCount = 0;
            for (int c = 1; c <= lastCol; c++)
            {
                var cellText = scanRowRef.Cell(c).GetString().Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(cellText) && ColumnAliases.ContainsKey(cellText))
                    matchCount++;
            }
            if (matchCount > maxAliasMatches) { maxAliasMatches = matchCount; headerRowNumber = scanRow; }
        }

        var headerRow = ws.Row(headerRowNumber);
        var colMap = new Dictionary<int, string>(); // col index → internal field name

        for (int c = 1; c <= lastCol; c++)
        {
            var headerText = headerRow.Cell(c).GetString().Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(headerText) &&
                ColumnAliases.TryGetValue(headerText, out var internalName))
                colMap[c] = internalName;
        }

        // Fallback: if no headers matched, use positional order
        if (colMap.Count == 0 && FieldOrder.TryGetValue(fileType.ToUpper(), out var fieldsFallback))
        {
            for (int c = 1; c <= Math.Min(lastCol, fieldsFallback.Length); c++)
                colMap[c] = fieldsFallback[c - 1];
        }

        // ── Read data rows ──
        for (int r = headerRowNumber + 1; r <= lastRow; r++)
        {
            var xlRow = ws.Row(r);

            // Skip completely empty rows
            bool anyValue = false;
            for (int c = 1; c <= lastCol; c++)
                if (!xlRow.Cell(c).IsEmpty()) { anyValue = true; break; }
            if (!anyValue) continue;

            var parsed   = new Dictionary<string, string>();
            var rawParts = new List<string>(lastCol);

            for (int c = 1; c <= lastCol; c++)
            {
                var cell = xlRow.Cell(c);
                string cellValue = CellToString(cell);
                rawParts.Add(cellValue);

                if (colMap.TryGetValue(c, out var fieldName))
                    parsed[fieldName] = cellValue;
            }

            ReconcileDocumentField(fileType, parsed);

            var rawData    = string.Join(",", rawParts);
            var parsedJson = parsed.Count > 0 ? JsonSerializer.Serialize(parsed) : null;

            rows.Add(new ExtractedRow(r - headerRowNumber, rawData, parsedJson));
        }

        return rows;
    }

    /// <summary>Convert an XL cell to a normalized string (dates as yyyy-MM-dd, numbers as InvariantCulture).</summary>
    private static string CellToString(IXLCell cell)
    {
        if (cell.IsEmpty()) return "";

        // Only attempt DateTime parsing when the cell is ACTUALLY date-typed.
        // ClosedXML's TryGetValue<DateTime> returns true for ANY numeric cell by treating
        // the number as an Excel serial date — e.g. 6929.68 (a monetary amount) becomes
        // 1918-12-20. Guarding on DataType == DateTime prevents that misparse.
        // Also wrap in try-catch for Excel's serial-60 leap-year bug.
        if (cell.DataType == XLDataType.DateTime)
        {
            try
            {
                if (cell.TryGetValue(out DateTime dt))
                    return dt.ToString("yyyy-MM-dd");
            }
            catch
            {
                // Excel serial-60 bug — fall through to numeric/string handling
            }
        }

        if (cell.TryGetValue(out double dbl))
            return dbl.ToString(CultureInfo.InvariantCulture);

        var raw = cell.GetString().Trim();

        // Normalize Brazilian decimal numbers: "1.234,56" → "1234.56"
        return NormalizeDecimalString(raw);
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    private static List<ExtractedRow> ExtractFromPdf(string filePath, string fileType)
    {
        var rows = new List<ExtractedRow>();

        using var document = PdfDocument.Open(filePath);

        // Collect all logical text lines across pages, ordered top-to-bottom
        var allLines = new List<string>();
        foreach (var page in document.GetPages())
        {
            var lineGroups = page.GetWords()
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 3.0) * 3)   // 3pt tolerance
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

            allLines.AddRange(lineGroups.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        if (allLines.Count == 0) return rows;

        // ── Find header row (first line with ≥2 recognised column names) ──
        List<string>? headerTokens = null;
        int headerIdx = -1;
        for (int i = 0; i < Math.Min(allLines.Count, 25); i++)
        {
            var tokens = SplitPdfLine(allLines[i]);
            int hits = tokens.Count(t => ColumnAliases.ContainsKey(t.Trim().ToLowerInvariant()));
            if (hits >= 2)
            {
                headerTokens = tokens;
                headerIdx = i;
                break;
            }
        }

        if (headerTokens == null)
        {
            // No header — emit lines as raw data (validator will apply CSV parsing)
            for (int i = 0; i < allLines.Count; i++)
            {
                var line = allLines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line))
                    rows.Add(new ExtractedRow(i + 1, line, null));
            }
            return rows;
        }

        // ── Map header token index → internal field name ──
        var colMapPdf = new Dictionary<int, string>();
        for (int j = 0; j < headerTokens.Count; j++)
        {
            var tok = headerTokens[j].Trim().ToLowerInvariant();
            if (ColumnAliases.TryGetValue(tok, out var fieldName))
                colMapPdf[j] = fieldName;
        }

        // ── Parse data rows ──
        int lineNum = 0;
        for (int i = headerIdx + 1; i < allLines.Count; i++)
        {
            var line = allLines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var tokens = SplitPdfLine(line);
            if (tokens.Count < 2) continue;

            var parsed = new Dictionary<string, string>();
            for (int j = 0; j < Math.Min(tokens.Count, headerTokens.Count); j++)
            {
                if (colMapPdf.TryGetValue(j, out var fieldName))
                    parsed[fieldName] = NormalizeDecimalString(tokens[j].Trim());
            }

            ReconcileDocumentField(fileType, parsed);

            var parsedJson = parsed.Count > 0 ? JsonSerializer.Serialize(parsed) : null;
            lineNum++;
            rows.Add(new ExtractedRow(lineNum, line, parsedJson));
        }

        return rows;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Split a PDF text line by 2+ spaces (ERP tables use wide spacing as delimiters).</summary>
    private static List<string> SplitPdfLine(string line)
    {
        var tokens = Regex.Split(line.Trim(), @"\s{2,}")
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .ToList();

        // Fallback: single-space split when no multi-space separator found
        if (tokens.Count <= 1)
            tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        return tokens;
    }

    /// <summary>
    /// Normalize Brazilian-formatted decimal strings to InvariantCulture.
    /// "1.234,56" → "1234.56", "1234,56" → "1234.56", plain integers/text left as-is.
    /// </summary>
    private static string NormalizeDecimalString(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        // Pattern: digits, optional thousand-dot, comma as decimal separator
        if (Regex.IsMatch(value.Trim(), @"^-?\d{1,3}(\.\d{3})*,\d{2}$"))
        {
            return value.Replace(".", "").Replace(",", ".");
        }

        // Pattern: digits with comma decimal only (no thousand separator)
        if (Regex.IsMatch(value.Trim(), @"^-?\d+,\d{1,2}$"))
        {
            return value.Replace(",", ".");
        }

        return value;
    }

    /// <summary>
    /// Reconcile the generic "documento" alias against type-specific primary key field names.
    /// PAG/REC use "documento"; EMITIDAS/TRANSF use "numero_doc"; FAT/COMP use "numero_nf".
    /// </summary>
    private static void ReconcileDocumentField(string fileType, Dictionary<string, string> parsed)
    {
        var ft = fileType.ToUpper();

        if ((ft == "PAG" || ft == "REC") &&
            parsed.TryGetValue("numero_doc", out var v1) && !parsed.ContainsKey("documento"))
        {
            parsed["documento"] = v1;
            parsed.Remove("numero_doc");
        }

        if ((ft == "EMITIDAS" || ft == "TRANSF") &&
            parsed.TryGetValue("documento", out var v2) && !parsed.ContainsKey("numero_doc"))
        {
            parsed["numero_doc"] = v2;
            parsed.Remove("documento");
        }

        if ((ft == "FAT" || ft == "COMP") &&
            parsed.TryGetValue("documento", out var v3) && !parsed.ContainsKey("numero_nf"))
        {
            parsed["numero_nf"] = v3;
            parsed.Remove("documento");
        }
    }
}
