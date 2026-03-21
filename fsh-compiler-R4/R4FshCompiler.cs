using fsh_compiler;
using fsh_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace fsh_compiler_r4;

/// <summary>
/// FSH compiler targeting FHIR R4 (version 4.0.1).
/// Wraps <see cref="FshCompiler"/> with R4-appropriate defaults, supplying the R4
/// <see cref="ModelInfo.ModelInspector"/> so the base compiler works entirely against
/// the R4 model.
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
    /// and <see cref="CompilerOptions.Inspector"/> defaults to the R4
    /// <see cref="ModelInfo.ModelInspector"/> when not specified.
    /// </param>
    public static CompileResult<List<FhirResource>> Compile(FshDoc doc, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        opts.FhirVersion ??= FhirVersion;
        opts.Inspector ??= ModelInfo.ModelInspector;
        return FshCompiler.Compile(doc, opts);
    }

    /// <summary>
    /// Compiles all entities across multiple <paramref name="docs"/> to FHIR R4 resources
    /// using a merged context so that aliases, rule sets, and invariants are shared across files.
    /// </summary>
    public static CompileResult<List<FhirResource>> Compile(
        IEnumerable<FshDoc> docs, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        opts.FhirVersion ??= FhirVersion;
        opts.Inspector ??= ModelInfo.ModelInspector;
        if (opts.Resolver == null)
        {
            var source = ZipSource.CreateValidationSource();
            opts.Resolver = source;
        }
        return FshCompiler.Compile(docs, opts);
    }
}
