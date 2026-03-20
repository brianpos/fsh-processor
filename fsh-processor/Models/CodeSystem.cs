namespace fsh_processor.Models;

/// <summary>
/// CodeSystem definition (CodeSystem: name)
/// </summary>
public class CodeSystem : FshEntity
{
    /// <summary>
    /// Id for the code system
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
    /// Rules for the code system (concepts and caret values)
    /// </summary>
    public List<CsRule> Rules { get; set; } = new();
}
