using Hl7.FhirShorthand.Compiler;
using Hl7.FhirShorthand.Compiler_r4;
using Hl7.FhirShorthand.Serialization;
using Hl7.FhirShorthand.Serialization.Models;
using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

/// <summary>
/// Shared utilities for SUSHI exporter-compatibility tests.
///
/// <para>
/// Tests in this folder are ported from the SUSHI exporter test suite:
/// <see href="https://github.com/FHIR/sushi/tree/main/test/export"/>.
/// They exercise the <c>fsh-compiler</c> layer (profile/extension/logical/resource/mapping/
/// valueset/codesystem export) the same way the SUSHI TypeScript tests exercise the SUSHI
/// exporter.
/// </para>
///
/// <para>Key behavioural differences vs SUSHI (used across all ported tests):</para>
/// <list type="bullet">
///   <item><description>SUSHI builds input programmatically (<c>new Profile('Foo'); profile.parent = 'Basic'</c>).
///     Ports use FSH text via <see cref="CompileDoc(string)"/>.</description></item>
///   <item><description>SUSHI asserts log messages via <c>loggerSpy</c>. Ports assert on
///     <see cref="CompileResult{T}.Warnings"/>. Where a SUSHI test only validates an error
///     message and the C# compiler does not yet emit a matching warning, the port is marked
///     <see cref="Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Inconclusive(string)"/>.</description></item>
///   <item><description>SUSHI operates at <c>Package</c> level (<c>pkg.fshMap</c>,
///     <c>pkg.deferredCaretRules</c>). Ports inspect the flat <c>List&lt;FhirResource&gt;</c>
///     returned by <c>R4FshCompiler.Compile</c>.</description></item>
///   <item><description>SUSHI produces a snapshot by default. Ports check the differential only;
///     tests that rely on inherited-element snapshot state are marked Inconclusive.</description></item>
///   <item><description>Tests depending on a full FHIR package resolver (Observation, Patient,
///     Basic, eLTSSServiceModel, etc.) will currently fail because
///     <see cref="CompilerOptions.Resolver"/> is not wired up in these tests. Per task
///     instructions ("just create the tests, don't try and fix any if they fail") the tests
///     are written to the SUSHI spec; most will fail until the resolver is attached.</description></item>
/// </list>
/// </summary>
public static class SushiCompilerTestHelper
{
    /// <summary>
    /// Parses and compiles a FSH string, returning the <see cref="CompileResult{T}"/> for
    /// inspection (including <see cref="CompileResult{T}.Warnings"/>).
    /// Fails the test on parse errors.
    /// </summary>
    public static CompileResult<List<FhirResource>> CompileDocResult(string fsh)
    {
        var trimmed = CompilerTestHelper.LeftAlign(fsh);
        var parseResult = FshParser.Parse(trimmed);

        if (parseResult is ParseResult.Failure parseFailure)
        {
            var msg = string.Join("; ", parseFailure.Errors.Select(e => $"Line {e.Line}: {e.Message}"));
            Assert.Fail($"Parse failed: {msg}");
        }

        var doc = ((ParseResult.Success)parseResult).Document;
        return R4FshCompiler.Compile(doc);
    }

    /// <summary>
    /// Parses and compiles a FSH string and returns the compiled resources.
    /// Fails the test on parse or compilation errors.
    /// </summary>
    public static List<FhirResource> CompileDoc(string fsh) => CompilerTestHelper.CompileDoc(fsh);

    /// <summary>
    /// Compiles FSH expected to succeed and returns the success result (so warnings can be
    /// inspected).  Fails the test on compilation errors.
    /// </summary>
    public static CompileResult<List<FhirResource>>.SuccessResult CompileExpectSuccess(string fsh)
    {
        var result = CompileDocResult(fsh);
        if (result is CompileResult<List<FhirResource>>.FailureResult failure)
        {
            var msg = string.Join("; ", failure.Errors.Select(e => e.ToString()));
            Assert.Fail($"Compile failed: {msg}");
        }
        return (CompileResult<List<FhirResource>>.SuccessResult)result;
    }

    /// <summary>
    /// Returns all <see cref="StructureDefinition"/> resources in the compiled result,
    /// corresponding roughly to SUSHI's <c>exporter.export().profiles</c> /
    /// <c>.extensions</c> / <c>.logicals</c> / <c>.resources</c>.
    /// </summary>
    public static List<StructureDefinition> StructureDefinitions(List<FhirResource> resources) =>
        resources.OfType<StructureDefinition>().ToList();

    /// <summary>
    /// Returns the first compiled <see cref="StructureDefinition"/> with the given
    /// <see cref="StructureDefinition.Name"/>; null when not found.
    /// </summary>
    public static StructureDefinition? FindSd(List<FhirResource> resources, string name) =>
        resources.OfType<StructureDefinition>().FirstOrDefault(s => s.Name == name);

    /// <summary>
    /// Returns the first <see cref="ElementDefinition"/> in the differential with an id that
    /// ends with the given suffix (convenience for matching SUSHI's
    /// <c>elements.find(e =&gt; e.id === 'X.Y')</c>).  Returns null when no element matches.
    /// </summary>
    public static ElementDefinition? FindElement(StructureDefinition sd, string pathOrId)
    {
        return sd.Differential?.Element
            .FirstOrDefault(e => e.ElementId == pathOrId || e.Path == pathOrId);
    }

    /// <summary>
    /// Returns all <see cref="Hl7.Fhir.Model.CodeSystem"/> resources in the compiled result,
    /// corresponding to SUSHI's <c>exporter.export().codeSystems</c>.
    /// </summary>
    public static List<Hl7.Fhir.Model.CodeSystem> CodeSystems(List<FhirResource> resources) =>
        resources.OfType<Hl7.Fhir.Model.CodeSystem>().ToList();

    /// <summary>
    /// Returns the first compiled <see cref="Hl7.Fhir.Model.CodeSystem"/> with the given
    /// <see cref="Hl7.Fhir.Model.CodeSystem.Name"/>; null when not found.
    /// </summary>
    public static Hl7.Fhir.Model.CodeSystem? FindCs(List<FhirResource> resources, string name) =>
        resources.OfType<Hl7.Fhir.Model.CodeSystem>().FirstOrDefault(cs => cs.Name == name);

    /// <summary>
    /// Returns all <see cref="Hl7.Fhir.Model.ValueSet"/> resources in the compiled result,
    /// corresponding to SUSHI's <c>exporter.export().valueSets</c>.
    /// </summary>
    public static List<Hl7.Fhir.Model.ValueSet> ValueSets(List<FhirResource> resources) =>
        resources.OfType<Hl7.Fhir.Model.ValueSet>().ToList();

    /// <summary>
    /// Returns the first compiled <see cref="Hl7.Fhir.Model.ValueSet"/> with the given
    /// <see cref="Hl7.Fhir.Model.ValueSet.Name"/>; null when not found.
    /// </summary>
    public static Hl7.Fhir.Model.ValueSet? FindVs(List<FhirResource> resources, string name) =>
        resources.OfType<Hl7.Fhir.Model.ValueSet>().FirstOrDefault(vs => vs.Name == name);
}
