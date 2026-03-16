using Hl7.Fhir.Introspection;

namespace fsh_compiler;

/// <summary>
/// Options controlling the FSH compilation process.
/// </summary>
public class CompilerOptions
{
    /// <summary>
    /// Base canonical URL prefix applied to compiled resources (e.g. "http://example.org/fhir").
    /// When set, resources whose URL is just a name will be prefixed with this value.
    /// </summary>
    public string? CanonicalBase { get; set; }

    /// <summary>
    /// FHIR version string to embed in compiled resources (e.g. "4.0.1", "4.3.0", "5.0.0").
    /// Defaults to null (no version embedded).
    /// </summary>
    public string? FhirVersion { get; set; }

    /// <summary>
    /// Additional alias overrides applied on top of any aliases parsed from the FSH document.
    /// Keys are alias names; values are the canonical URLs they resolve to.
    /// </summary>
    public Dictionary<string, string>? AliasOverrides { get; set; }

    /// <summary>
    /// <see cref="ModelInspector"/> used for caret-value property lookup and dynamic type
    /// instantiation.  Version-specific compiler wrappers supply their version's
    /// <c>ModelInfo.ModelInspector</c> here so that the base compiler works entirely
    /// against the caller's FHIR model without any compile-time dependency on a specific
    /// version assembly.
    /// </summary>
    /// <remarks>
    /// When <c>null</c> the compiler falls back to an inspector built from the
    /// <c>Hl7.Fhir.Conformance</c> assembly, which covers conformance types
    /// (StructureDefinition, ElementDefinition, ValueSet, CodeSystem) for all FHIR versions.
    /// </remarks>
    public ModelInspector? Inspector { get; set; }
}
