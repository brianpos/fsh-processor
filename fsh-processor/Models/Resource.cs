namespace fsh_processor.Models;

/// <summary>
/// Resource definition (Resource: name)
/// </summary>
public class Resource : FshEntity
{
    /// <summary>
    /// Parent resource
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Id for the resource
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
    /// Rules defining the resource (can include SD rules and LR-specific rules)
    /// </summary>
    public List<FshRule> Rules { get; set; } = new();
}
