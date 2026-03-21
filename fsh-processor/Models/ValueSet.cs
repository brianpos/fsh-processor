namespace fsh_processor.Models;

/// <summary>
/// ValueSet definition (ValueSet: name)
/// </summary>
public class ValueSet : FshEntity
{
    /// <summary>
    /// Id for the value set
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
    /// Rules for the value set (components and caret values)
    /// </summary>
    public List<VsRule> Rules { get; set; } = new();
}
