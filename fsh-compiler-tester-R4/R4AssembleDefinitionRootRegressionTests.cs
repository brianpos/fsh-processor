using Hl7.FhirShorthand.Compiler;
using Hl7.FhirShorthand.Compiler_r4;
using Hl7.FhirShorthand.Serialization;
using Hl7.FhirShorthand.Serialization.Models;
using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4;

/// <summary>
/// Regression tests for the <c>AssembleDefinitionRoot</c> extension compilation
/// that uncovered two compiler gaps vs. the sushi-generated reference:
///
/// <list type="number">
///   <item>
///     The root <c>Extension</c> element should carry <c>short</c> copied from the
///     extension Title and <c>definition</c> copied from the extension Description.
///   </item>
///   <item>
///     When <c>* value[x] only uri or boolean</c> is written in FSH, the emitted
///     <c>type</c> list ordering must follow the FHIR base <c>Extension.value[x]</c>
///     declared-type ordering ([boolean, uri]), not the order in which they were
///     listed in FSH.
///   </item>
/// </list>
/// </summary>
[TestClass]
public class R4AssembleDefinitionRootRegressionTests
{
    private const string SdcCanonicalBase = "http://hl7.org/fhir/uv/sdc";

    private static readonly string SdcPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "SDC");

    private static StructureDefinition CompileAssembleDefinitionRoot()
    {
        var docs = new List<FshDoc>
        {
            Parse(Path.Combine(SdcPath, "aliases.fsh")),
            Parse(Path.Combine(SdcPath, "shared.fsh")),
            Parse(Path.Combine(SdcPath, "AssembleDefinitionRoot.fsh"))
        };

        var options = new CompilerOptions
        {
            CanonicalBase = SdcCanonicalBase,
            FhirVersion = R4FshCompiler.FhirVersion,
        };

        var result = R4FshCompiler.Compile(docs, options);
        if (result is CompileResult<List<FhirResource>>.FailureResult f)
        {
            var msg = string.Join("; ", f.Errors.Select(e => e.ToString()));
            Assert.Fail($"Compile failed: {msg}");
        }

        var resources = ((CompileResult<List<FhirResource>>.SuccessResult)result).Value;
        var sd = resources.OfType<StructureDefinition>()
                          .FirstOrDefault(s => s.Name == "AssembleDefinitionRoot");
        Assert.IsNotNull(sd, "Expected an AssembleDefinitionRoot StructureDefinition");
        return sd!;
    }

    private static FshDoc Parse(string path)
    {
        Assert.IsTrue(File.Exists(path), $"Test data not found: {path}");
        var text = File.ReadAllText(path);
        var pr = FshParser.Parse(text);
        if (pr is ParseResult.Failure pf)
        {
            var err = pf.Errors.FirstOrDefault();
            Assert.Fail($"Parse failed for {Path.GetFileName(path)}: {err?.Message} (line {err?.Line})");
        }
        return ((ParseResult.Success)pr).Document;
    }

    /// <summary>
    /// The root <c>Extension</c> element must carry <c>short</c> copied from the
    /// extension Title and <c>definition</c> copied from the extension Description
    /// (matches sushi's default behaviour when no explicit caret rules override them).
    /// </summary>
    [TestMethod]
    public void AssembleDefinitionRoot_RootElementCarriesShortAndDefinitionFromEntity()
    {
        var sd = CompileAssembleDefinitionRoot();
        var root = sd.Differential.Element.First(e => e.Path == "Extension" && e.SliceName == null);

        Assert.AreEqual("Assemble Definition Root", root.Short,
            "Root Extension element should have Short copied from the extension Title.");
        Assert.IsNotNull(root.Definition, "Root Extension element should have Definition set.");
        StringAssert.StartsWith(root.Definition,
            "Indicates that the assembly process SHALL only use definitions");
    }

    /// <summary>
    /// When FSH declares <c>value[x] only uri or boolean</c>, the compiled type list
    /// must be ordered by the base FHIR <c>Extension.value[x]</c> type declaration
    /// order, which places <c>boolean</c> before <c>uri</c>.
    /// </summary>
    [TestMethod]
    public void AssembleDefinitionRoot_ValueXTypeOrderMatchesBaseDeclaration()
    {
        var sd = CompileAssembleDefinitionRoot();
        var valueEl = sd.Differential.Element
            .FirstOrDefault(e => e.Path == "Extension.value[x]" && e.SliceName == null);

        Assert.IsNotNull(valueEl, "Expected Extension.value[x] element in the differential.");
        Assert.IsNotNull(valueEl!.Type);
        Assert.HasCount(2, valueEl.Type,
            $"Expected 2 types, got: [{string.Join(",", valueEl.Type.Select(t => t.Code))}]");
        Assert.AreEqual("boolean", valueEl.Type[0].Code,
            $"Expected boolean first; got [{string.Join(",", valueEl.Type.Select(t => t.Code))}]");
        Assert.AreEqual("uri", valueEl.Type[1].Code,
            $"Expected uri second; got [{string.Join(",", valueEl.Type.Select(t => t.Code))}]");
    }
}
