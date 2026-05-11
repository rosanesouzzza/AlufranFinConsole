namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Fonte de verdade para o mapeamento de colunas por tipo de base.
/// Toda coluna de origem deve ser resolvida via esta entidade — spec §6.
/// </summary>
public class ColumnMapping
{
    public int Id { get; set; }

    /// <summary>PAG | REC | FAT | EMITIDAS | COMP | TRANSF | FOPAG</summary>
    public string BaseType { get; set; } = "";

    /// <summary>Nome da coluna no arquivo de origem (ex: "Fornecedor", "NOME FORN.")</summary>
    public string SourceColumnName { get; set; } = "";

    /// <summary>Nome interno canônico do campo (ex: "SupplierName")</summary>
    public string TargetColumnName { get; set; } = "";

    /// <summary>string | decimal | datetime | int | bool</summary>
    public string DataType { get; set; } = "string";

    /// <summary>Coluna ausente e obrigatória → QA estrutural</summary>
    public bool IsRequired { get; set; }

    /// <summary>Se false, coluna é descartada do NormalizedJson (permanece no RawJson)</summary>
    public bool ShouldKeep { get; set; } = true;

    /// <summary>Regra de transformação opcional em JSON (ex: {"trim":true,"upper":true})</summary>
    public string? TransformationRule { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
