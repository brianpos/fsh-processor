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
    /// Invariant name → <see cref="Invariant"/> entity, collected from all <c>Invariant</c> entities.
    /// Used to populate <c>ConstraintComponent.Human</c>, <c>Expression</c>, <c>XPath</c>, and
    /// <c>Severity</c> when an <c>ObeysRule</c> references an invariant by name.
    /// </summary>
    public Dictionary<string, Invariant> Invariants { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Instance name → <see cref="Instance"/> entity, collected from all <c>Instance</c> entities.
    /// Used to resolve <c>* contained = &lt;name&gt;</c> cross-instance references, where the
    /// named instance is embedded into the host resource's <c>contained[]</c> list.
    /// </summary>
    public Dictionary<string, Instance> Instances { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Non-fatal warnings accumulated during compilation.  Populated by rule processors when
    /// a rule is silently skipped or an unresolved reference is encountered.
    /// </summary>
    public List<CompilerWarning> Warnings { get; } = new();

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
                case Invariant inv when !string.IsNullOrEmpty(inv.Name):
                    ctx.Invariants[inv.Name] = inv;
                    break;
                case Instance inst when !string.IsNullOrEmpty(inst.Name):
                    ctx.Instances[inst.Name] = inst;
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
                case Invariant inv when !string.IsNullOrEmpty(inv.Name):
                    Invariants.TryAdd(inv.Name, inv);
                    break;
                case Instance inst when !string.IsNullOrEmpty(inst.Name):
                    Instances.TryAdd(inst.Name, inst);
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
