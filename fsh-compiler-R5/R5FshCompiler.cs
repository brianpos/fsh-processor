using fsh_compiler;
using fsh_processor.Models;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace fsh_compiler_r5;

/// <summary>
/// FSH compiler targeting FHIR R5 (version 5.0.0).
/// Wraps <see cref="FshCompiler"/> with R5-appropriate defaults.
/// </summary>
public static class R5FshCompiler
{
    /// <summary>FHIR R5 version string.</summary>
    public const string FhirVersion = "5.0.0";

    /// <summary>
    /// Compiles all entities in <paramref name="doc"/> to FHIR R5 resources.
    /// </summary>
    /// <param name="doc">Parsed FSH document.</param>
    /// <param name="options">
    /// Optional options. <see cref="CompilerOptions.FhirVersion"/> defaults to <c>"5.0.0"</c>
    /// when not specified.
    /// </param>
    public static CompileResult<List<FhirResource>> Compile(FshDoc doc, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        opts.FhirVersion ??= FhirVersion;
        return FshCompiler.Compile(doc, opts);
    }
}
