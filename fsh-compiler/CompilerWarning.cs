using fsh_processor.Models;

namespace fsh_compiler;

/// <summary>
/// A non-fatal warning emitted during compilation (e.g. a silently-skipped rule or an
/// unresolved reference that did not prevent the resource from being generated).
/// </summary>
public class CompilerWarning
{
    /// <summary>
    /// Name of the FSH entity that triggered the warning (may be empty for cross-cutting warnings).
    /// </summary>
    public string? EntityName { get; init; }

    /// <summary>Human-readable warning message.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Source position within the FSH document, if available.
    /// </summary>
    public SourcePosition? Position { get; init; }
}
