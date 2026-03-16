using fsh_processor.Models;

namespace fsh_compiler;

/// <summary>
/// Represents a compilation error for a single FSH entity.
/// </summary>
public class CompilerError
{
    /// <summary>Name of the FSH entity that caused the error.</summary>
    public string EntityName { get; init; } = string.Empty;

    /// <summary>Human-readable error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Source location of the entity (may be null if unavailable).</summary>
    public SourcePosition? Position { get; init; }

    /// <inheritdoc/>
    public override string ToString() =>
        Position is { } pos
            ? $"[{EntityName}] ({pos.StartLine}:{pos.StartColumn}): {Message}"
            : $"[{EntityName}]: {Message}";
}
