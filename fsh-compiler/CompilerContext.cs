using fsh_processor.Models;

namespace fsh_compiler;

/// <summary>
/// Compilation context built from one or more <see cref="FshDoc"/> instances.
/// Holds resolved aliases and named rule sets for use during rule processing.
/// </summary>
public class CompilerContext
{
    /// <summary>
    /// Alias name → canonical URL, collected from all <c>Alias</c> entities in the document.
    /// </summary>
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// RuleSet name → <see cref="RuleSet"/> entity, collected from all <c>RuleSet</c> entities.
    /// </summary>
    public Dictionary<string, RuleSet> RuleSets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Builds a <see cref="CompilerContext"/> by scanning a single <see cref="FshDoc"/> for
    /// <c>Alias</c> and <c>RuleSet</c> entities.
    /// </summary>
    public static CompilerContext Build(FshDoc doc)
    {
        var ctx = new CompilerContext();
        foreach (var entity in doc.Entities)
        {
            switch (entity)
            {
                case Alias alias when !string.IsNullOrEmpty(alias.Name):
                    ctx.Aliases[alias.Name] = alias.Value ?? string.Empty;
                    break;
                case RuleSet rs when !string.IsNullOrEmpty(rs.Name):
                    ctx.RuleSets[rs.Name] = rs;
                    break;
            }
        }
        return ctx;
    }

    /// <summary>
    /// Merges an additional <see cref="FshDoc"/> into this context (for multi-file scenarios).
    /// Existing entries are not overwritten.
    /// </summary>
    public void MergeFrom(FshDoc doc)
    {
        foreach (var entity in doc.Entities)
        {
            switch (entity)
            {
                case Alias alias when !string.IsNullOrEmpty(alias.Name):
                    Aliases.TryAdd(alias.Name, alias.Value ?? string.Empty);
                    break;
                case RuleSet rs when !string.IsNullOrEmpty(rs.Name):
                    RuleSets.TryAdd(rs.Name, rs);
                    break;
            }
        }
    }

    /// <summary>
    /// Resolves an alias name to its canonical URL.
    /// Returns the input unchanged if the alias is not found.
    /// </summary>
    public string ResolveAlias(string nameOrUrl) =>
        Aliases.TryGetValue(nameOrUrl, out var resolved) ? resolved : nameOrUrl;
}
