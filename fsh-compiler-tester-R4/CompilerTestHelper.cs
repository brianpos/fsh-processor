using fsh_compiler;
using fsh_compiler_r4;
using fsh_processor;
using fsh_processor.Models;
using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;
using FhirCodeSystem = Hl7.Fhir.Model.CodeSystem;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Shared utilities for R4 FSH compiler tests.
/// </summary>
public static class CompilerTestHelper
{
    /// <summary>
    /// Parses and compiles a FSH string using the R4 compiler.
    /// Fails the test on parse or compilation errors.
    /// </summary>
    public static List<FhirResource> CompileDoc(string fsh)
    {
        var trimmed = LeftAlign(fsh);
        var parseResult = FshParser.Parse(trimmed);

        if (parseResult is ParseResult.Failure parseFailure)
        {
            var msg = string.Join("; ", parseFailure.Errors.Select(e => $"Line {e.Line}: {e.Message}"));
            Assert.Fail($"Parse failed: {msg}");
        }

        var doc = ((ParseResult.Success)parseResult).Document;
        var compileResult = R4FshCompiler.Compile(doc);

        if (compileResult is CompileResult<List<FhirResource>>.FailureResult failure)
        {
            var msg = string.Join("; ", failure.Errors.Select(e => e.ToString()));
            Assert.Fail($"Compile failed: {msg}");
        }

        return ((CompileResult<List<FhirResource>>.SuccessResult)compileResult).Value;
    }

    /// <summary>
    /// Gets the first <see cref="StructureDefinition"/> from compiled resources,
    /// asserting it exists and optionally that its name matches.
    /// </summary>
    public static StructureDefinition GetStructureDefinition(
        List<FhirResource> resources, string? name = null)
    {
        var sd = resources.OfType<StructureDefinition>()
            .FirstOrDefault(s => name == null || s.Name == name);
        Assert.IsNotNull(sd, name != null
            ? $"StructureDefinition '{name}' not found in compiled resources"
            : "No StructureDefinition found in compiled resources");
        return sd;
    }

    /// <summary>Gets the first <see cref="FhirValueSet"/> from compiled resources.</summary>
    public static FhirValueSet GetValueSet(List<FhirResource> resources, string? name = null)
    {
        var vs = resources.OfType<FhirValueSet>()
            .FirstOrDefault(v => name == null || v.Name == name);
        Assert.IsNotNull(vs, name != null
            ? $"ValueSet '{name}' not found"
            : "No ValueSet found in compiled resources");
        return vs;
    }

    /// <summary>Gets the first <see cref="FhirCodeSystem"/> from compiled resources.</summary>
    public static FhirCodeSystem GetCodeSystem(List<FhirResource> resources, string? name = null)
    {
        var cs = resources.OfType<FhirCodeSystem>()
            .FirstOrDefault(c => name == null || c.Name == name);
        Assert.IsNotNull(cs, name != null
            ? $"CodeSystem '{name}' not found"
            : "No CodeSystem found in compiled resources");
        return cs;
    }

    /// <summary>
    /// Finds an <see cref="ElementDefinition"/> by its path suffix within a
    /// <see cref="StructureDefinition"/>'s differential, asserting it exists.
    /// </summary>
    public static ElementDefinition GetElement(StructureDefinition sd, string pathSuffix)
    {
        var fullPath = $"{sd.Type}.{pathSuffix}";
        var ed = sd.Differential?.Element
            .FirstOrDefault(e => e.Path == fullPath && e.SliceName == null);
        Assert.IsNotNull(ed, $"ElementDefinition at '{fullPath}' not found");
        return ed;
    }

    /// <summary>
    /// Finds a slice <see cref="ElementDefinition"/> by path suffix and slice name.
    /// </summary>
    public static ElementDefinition GetSliceElement(
        StructureDefinition sd, string pathSuffix, string sliceName)
    {
        var fullPath = $"{sd.Type}.{pathSuffix}";
        var ed = sd.Differential?.Element
            .FirstOrDefault(e => e.Path == fullPath && e.SliceName == sliceName);
        Assert.IsNotNull(ed, $"Slice '{sliceName}' at '{fullPath}' not found");
        return ed;
    }

    /// <summary>
    /// Removes common leading whitespace from a multiline string.
    /// </summary>
    public static string LeftAlign(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var lines = input.Split('\n');

        int start = 0;
        while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
        int end = lines.Length - 1;
        while (end >= start && string.IsNullOrWhiteSpace(lines[end])) end--;

        if (start > end) return string.Empty;
        lines = lines[start..(end + 1)];

        int minIndent = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        return string.Join("\n", lines.Select(l => l.Length >= minIndent ? l[minIndent..] : l.TrimStart())) + "\n";
    }
}
