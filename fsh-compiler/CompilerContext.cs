using fsh_processor.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;

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
    /// StructureDefinitions compiled so far, indexed by multiple keys so that a profile-based
    /// <c>InstanceOf</c> can be resolved to its underlying FHIR base resource type.
    /// Keys include: the FSH entity name, the last path segment of the SD's canonical URL, and
    /// the SD's <c>id</c> field.
    /// </summary>
    public Dictionary<string, StructureDefinition> CompiledStructureDefinitions { get; } =
        new(StringComparer.Ordinal);

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

    /// <summary>
    /// Registers a compiled <see cref="StructureDefinition"/> in <see cref="CompiledStructureDefinitions"/>
    /// under multiple keys: the FSH entity name, the last path-segment of <c>sd.Url</c>, and
    /// <c>sd.Id</c>. This enables profile-based <c>InstanceOf</c> lookup by any of these names.
    /// </summary>
    public void RegisterStructureDefinition(string entityName, StructureDefinition sd)
    {
        CompiledStructureDefinitions.TryAdd(entityName, sd);
        if (!string.IsNullOrEmpty(sd.Url))
        {
            var lastSlash = sd.Url.LastIndexOf('/');
            var urlSegment = lastSlash >= 0 ? sd.Url[(lastSlash + 1)..] : sd.Url;
            CompiledStructureDefinitions.TryAdd(urlSegment, sd);
        }
        if (!string.IsNullOrEmpty(sd.Id))
            CompiledStructureDefinitions.TryAdd(sd.Id, sd);
    }

    /// <summary>
    /// When <paramref name="typeName"/> is a profile identifier rather than a bare FHIR resource
    /// type name, walks the <see cref="CompiledStructureDefinitions"/> chain to find the underlying
    /// FHIR base resource type, then returns the corresponding <see cref="ClassMapping"/> from
    /// <paramref name="inspector"/>.
    /// </summary>
    /// <param name="typeName">The type/profile name to resolve.</param>
    /// <param name="inspector">Version-specific model inspector.</param>
    /// <returns>
    /// A <see cref="ClassMapping"/> for the resolved FHIR resource type, or <c>null</c> when
    /// the type cannot be resolved.
    /// </returns>
    public ClassMapping? ResolveClassMappingForProfile(string typeName, ModelInspector inspector, IResourceResolver resolver)
    {
        var sd = resolver.FindStructureDefinition(typeName);
        var visited = new HashSet<string>(StringComparer.Ordinal) { typeName };

        while (sd is not null)
        {
            // sd.Type is the FHIR base resource type (e.g. "Library", "ValueSet").
            if (!string.IsNullOrEmpty(sd.Type))
            {
                var cm = inspector.FindClassMapping(sd.Type);
                if (cm is not null && cm.IsResource)
                    return cm;
            }

            // Walk the BaseDefinition chain for multi-level profile hierarchies.
            if (string.IsNullOrEmpty(sd.BaseDefinition))
                break;

            if (!visited.Add(sd.BaseDefinition))
                break; // cycle guard

            sd = resolver.FindStructureDefinition(sd.BaseDefinition);
        }

        return null;
    }
}
