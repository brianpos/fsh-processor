using fsh_compiler;
using fsh_processor.Models;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace fsh_compiler_r4b;

/// <summary>
/// FSH compiler targeting FHIR R4B (version 4.3.0).
/// Wraps <see cref="FshCompiler"/> with R4B-appropriate defaults.
/// </summary>
public static class R4BFshCompiler
{
    /// <summary>FHIR R4B version string.</summary>
    public const string FhirVersion = "4.3.0";

    /// <summary>
    /// Compiles all entities in <paramref name="doc"/> to FHIR R4B resources.
    /// </summary>
    /// <param name="doc">Parsed FSH document.</param>
    /// <param name="options">
    /// Optional options. <see cref="CompilerOptions.FhirVersion"/> defaults to <c>"4.3.0"</c>
    /// when not specified.
    /// </param>
    public static CompileResult<List<FhirResource>> Compile(FshDoc doc, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        opts.FhirVersion ??= FhirVersion;
        return FshCompiler.Compile(doc, opts);
    }
}
