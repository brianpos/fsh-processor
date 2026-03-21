namespace fsh_processor.Models;

/// <summary>
/// Extension definition (Extension: name)
/// </summary>
public class Extension : FshEntity
{
    /// <summary>
    /// Parent extension
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Id for the extension
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether Description was originally a multiline (triple-quoted) string.
    /// <c>null</c> = auto-detect; <c>true</c> = always multiline; <c>false</c> = always single-line.
    /// </summary>
    public bool? IsDescriptionMultiline { get; set; }

    /// <summary>
    /// Context items
    /// </summary>
    public List<Context> Contexts { get; set; } = new();

    /// <summary>
    /// Rules defining the extension
    /// </summary>
    public List<FshRule> Rules { get; set; } = new();
}

/// <summary>
/// Discriminates the three grammar alternatives for a context item.
/// Grammar: contextItem: STRING | SEQUENCE | CODE;
/// </summary>
public enum ContextItemType
{
    /// <summary>
    /// Quoted string (<c>STRING</c>) — a FHIRPath expression context.
    /// </summary>
    Fhirpath,

    /// <summary>
    /// Unquoted identifier / path (<c>SEQUENCE</c>) — an element-name/path context.
    /// </summary>
    Element,

    /// <summary>
    /// Code token (<c>CODE</c>, e.g. <c>http://example.org#fragment</c>) — an extension-URL context.
    /// </summary>
    Extension,
}

/// <summary>
/// Context item for extensions
/// </summary>
public class Context : FshNode
{
    /// <summary>
    /// Context value (quoted string stripped of quotes, or unquoted text)
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the context was quoted (i.e. a FHIRPath expression)
    /// </summary>
    public bool IsQuoted { get; set; }

    /// <summary>
    /// The context type derived from the grammar alternative that was matched:
    /// <see cref="ContextItemType.Fhirpath"/> for quoted strings,
    /// <see cref="ContextItemType.Element"/> for unquoted SEQUENCE tokens, and
    /// <see cref="ContextItemType.Extension"/> for CODE tokens (URL#fragment form).
    /// </summary>
    public ContextItemType Type { get; set; }
}
