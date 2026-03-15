using fsh_compiler;
using fsh_processor.Models;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace fsh_compiler_r4;

/// <summary>
/// FSH compiler targeting FHIR R4 (version 4.0.1).
/// Wraps <see cref="FshCompiler"/> with R4-appropriate defaults.
/// </summary>
public static class R4FshCompiler
{
    /// <summary>FHIR R4 version string.</summary>
    public const string FhirVersion = "4.0.1";

    /// <summary>
    /// Compiles all entities in <paramref name="doc"/> to FHIR R4 resources.
    /// </summary>
    /// <param name="doc">Parsed FSH document.</param>
    /// <param name="options">
    /// Optional options. <see cref="CompilerOptions.FhirVersion"/> defaults to <c>"4.0.1"</c>
    /// when not specified.
    /// </param>
    public static CompileResult<List<FhirResource>> Compile(FshDoc doc, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        opts.FhirVersion ??= FhirVersion;
        return FshCompiler.Compile(doc, opts);
    }
}
