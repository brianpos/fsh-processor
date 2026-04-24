using Hl7.FhirShorthand.Compiler;
using Hl7.FhirShorthand.Compiler_r4;
using Hl7.FhirShorthand.Serialization;
using Hl7.FhirShorthand.Serialization.Models;
using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4;

/// <summary>
/// Regression tests pinpointing the simple-Extension StructureDefinition generation
/// failures first observed via <c>Compile_SpecificResource("AnswerExpressionExtension.fsh")</c>.
///
/// <para>
/// The <c>AnswerExpressionExtension.fsh</c> source is a classic "simple" FHIR R4 extension:
/// it declares <c>value[x] only Expression</c> and no sub-extensions.  Sushi produces
/// a differential with four elements in this exact shape:
/// </para>
///
/// <list type="number">
///   <item><c>Extension</c> — no <c>min</c> (default 0 is suppressed), <c>max="1"</c></item>
///   <item><c>Extension.extension</c> — auto-injected <c>max="0"</c> marker for simple extensions</item>
///   <item><c>Extension.url</c> — <c>fixedUri</c> equal to the extension's canonical URL, no <c>type</c></item>
///   <item><c>Extension.value[x]</c> — <c>type</c> restricted to <c>Expression</c></item>
/// </list>
///
/// <para>
/// Prior to the fix, the compiler produced only 3 elements (root with an explicit
/// <c>min:0</c>, <c>Extension.url</c> with <c>type:[{code:uri}]</c> instead of
/// <c>fixedUri</c>, and <c>Extension.value[x]</c>).  The required <c>Extension.extension</c>
/// zero-cardinality marker was missing entirely.
/// </para>
/// </summary>
[TestClass]
public class R4SimpleExtensionRegressionTests
{
    private const string SdcCanonicalBase = "http://hl7.org/fhir/uv/sdc";

    private static readonly string AnswerExpressionFshPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "SDC", "AnswerExpressionExtension.fsh");

    private static readonly string AliasesFshPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "SDC", "aliases.fsh");

    private static StructureDefinition CompileAnswerExpressionExtension()
    {
        Assert.IsTrue(File.Exists(AnswerExpressionFshPath),
            $"Test data not found: {AnswerExpressionFshPath}");

        var docs = new List<Hl7.FhirShorthand.Serialization.Models.FshDoc>
        {
            Parse(AliasesFshPath),
            Parse(AnswerExpressionFshPath)
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
                          .FirstOrDefault(s => s.Name == "AnswerExpressionExtension");
        Assert.IsNotNull(sd, "Expected an AnswerExpressionExtension StructureDefinition");
        return sd!;
    }

    private static Hl7.FhirShorthand.Serialization.Models.FshDoc Parse(string path)
    {
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
    /// Simple-extension differential must contain exactly 4 elements in the order
    /// produced by sushi: root, the <c>Extension.extension</c> zero-cardinality marker,
    /// <c>Extension.url</c>, and <c>Extension.value[x]</c>.
    /// </summary>
    [TestMethod]
    public void SimpleExtension_DifferentialHasExpectedElementsInOrder()
    {
        var sd = CompileAnswerExpressionExtension();
        var paths = sd.Differential.Element.Select(e => (e.Path, e.SliceName)).ToList();

        Assert.HasCount(4, paths,
            $"Expected 4 differential elements, got {paths.Count}: " +
            string.Join(", ", paths.Select(p => p.Path + (p.SliceName is null ? "" : $":{p.SliceName}"))));

        Assert.AreEqual(("Extension", (string?)null), paths[0]);
        Assert.AreEqual(("Extension.extension", (string?)null), paths[1]);
        Assert.AreEqual(("Extension.url", (string?)null), paths[2]);
        Assert.AreEqual(("Extension.value[x]", (string?)null), paths[3]);
    }

    /// <summary>
    /// The root <c>Extension</c> element must not carry an explicit <c>min=0</c>;
    /// sushi strips that because it equals the inherited default.
    /// </summary>
    [TestMethod]
    public void SimpleExtension_RootElementHasNoExplicitMinWhenDefault()
    {
        var sd = CompileAnswerExpressionExtension();
        var root = sd.Differential.Element.First(e => e.Path == "Extension" && e.SliceName == null);

        Assert.IsNull(root.MinElement,
            "Root Extension element should not carry an explicit min=0 (default).");
        Assert.AreEqual("1", root.Max, "Root Extension element should have max=1");
    }

    /// <summary>
    /// A simple extension (no <c>extension contains …</c> rule) must include an
    /// auto-injected <c>Extension.extension</c> element with <c>max="0"</c> to forbid
    /// nested extensions.
    /// </summary>
    [TestMethod]
    public void SimpleExtension_EmitsZeroCardinalityExtensionChild()
    {
        var sd = CompileAnswerExpressionExtension();
        var extChild = sd.Differential.Element
            .FirstOrDefault(e => e.Path == "Extension.extension" && e.SliceName == null);

        Assert.IsNotNull(extChild,
            "Expected an auto-generated Extension.extension element for the simple extension.");
        Assert.AreEqual("0", extChild!.Max,
            "Extension.extension marker must have max=\"0\" for a simple extension.");
    }

    /// <summary>
    /// <c>Extension.url</c> for any extension is required to carry <c>fixedUri</c> set
    /// to the extension's canonical URL and must NOT carry a redundant type constraint
    /// (the base already pins the type to <c>uri</c>).
    /// </summary>
    [TestMethod]
    public void SimpleExtension_UrlElementUsesFixedUriWithCanonical()
    {
        var sd = CompileAnswerExpressionExtension();
        var urlEl = sd.Differential.Element
            .FirstOrDefault(e => e.Path == "Extension.url" && e.SliceName == null);

        Assert.IsNotNull(urlEl, "Expected an Extension.url element in the differential.");

        var fixedUri = urlEl!.Fixed as FhirUri;
        Assert.IsNotNull(fixedUri,
            "Extension.url.fixed must be a FhirUri carrying the extension's canonical URL.");
        Assert.AreEqual(
            $"{SdcCanonicalBase}/StructureDefinition/sdc-questionnaire-answerExpression",
            fixedUri!.Value);

        Assert.IsTrue(urlEl.Type == null || urlEl.Type.Count == 0,
            "Extension.url should not carry a redundant type constraint; " +
            $"got [{string.Join(",", (urlEl.Type ?? new()).Select(t => t.Code))}].");
    }

    /// <summary>
    /// Sushi omits <c>experimental</c> entirely when the FSH source does not set it.
    /// Our compiler must not emit a default <c>experimental: false</c> for extensions
    /// (it was previously being hard-coded, producing a spurious byte-level diff
    /// against the sushi output).
    /// </summary>
    [TestMethod]
    public void SimpleExtension_DoesNotEmitDefaultExperimental()
    {
        var sd = CompileAnswerExpressionExtension();
        Assert.IsNull(sd.ExperimentalElement,
            "Extension SD should not populate Experimental when the FSH source doesn't set it; " +
            $"got Experimental={sd.Experimental}.");
    }

    /// <summary>
    /// Sushi always emits <c>fhirVersion</c> on generated StructureDefinitions (set
    /// from the IG's target FHIR version).  When <see cref="CompilerOptions.FhirVersion"/>
    /// is supplied, the extension SD must carry the corresponding
    /// <see cref="FHIRVersion"/> value (<c>4.0.1</c> for R4).
    /// </summary>
    [TestMethod]
    public void SimpleExtension_PopulatesFhirVersionFromOptions()
    {
        var sd = CompileAnswerExpressionExtension();
        Assert.AreEqual(FHIRVersion.N4_0_1, sd.FhirVersion,
            "Extension SD should have FhirVersion=4.0.1 when CompilerOptions.FhirVersion is set for R4.");
    }

    // ── EndpointExtension.fsh-specific regressions ─────────────────────────────

    private static readonly string EndpointExtensionFshPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "SDC", "EndpointExtension.fsh");

    private static StructureDefinition CompileEndpointExtension()
    {
        Assert.IsTrue(File.Exists(EndpointExtensionFshPath),
            $"Test data not found: {EndpointExtensionFshPath}");

        var docs = new List<Hl7.FhirShorthand.Serialization.Models.FshDoc>
        {
            Parse(AliasesFshPath),
            Parse(EndpointExtensionFshPath)
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
                          .FirstOrDefault(s => s.Name == "EndpointExtension");
        Assert.IsNotNull(sd, "Expected an EndpointExtension StructureDefinition");
        return sd!;
    }

    /// <summary>
    /// When the FSH source declares <c>* . 0..*</c> on an extension, both the min and
    /// max values match the FHIR R4 default for the <c>Extension</c> root element, and
    /// sushi emits neither <c>min</c> nor <c>max</c>.  Our compiler must also strip
    /// <c>max="*"</c> when it equals the inherited default.
    /// </summary>
    [TestMethod]
    public void SimpleExtension_RootElementStripsDefaultMaxStar()
    {
        var sd = CompileEndpointExtension();
        var root = sd.Differential.Element.First(e => e.Path == "Extension" && e.SliceName == null);

        Assert.IsNull(root.MinElement,
            "Root Extension element should not carry an explicit min=0 (default).");
        Assert.IsNull(root.MaxElement,
            $"Root Extension element should not carry max=\"*\" (inherits default); got max=\"{root.Max}\".");
    }

    /// <summary>
    /// Sanity-check that the <c>EndpointExtension</c> (a simple extension with
    /// <c>value[x] only uri</c>) still receives the auto-injected zero-cardinality
    /// <c>Extension.extension</c> marker and a <c>fixedUri</c> on <c>Extension.url</c>
    /// even when the root cardinality is <c>0..*</c>.
    /// </summary>
    [TestMethod]
    public void EndpointExtension_DifferentialHasExpectedElementsInOrder()
    {
        var sd = CompileEndpointExtension();
        var paths = sd.Differential.Element.Select(e => (e.Path, e.SliceName)).ToList();

        Assert.HasCount(4, paths,
            $"Expected 4 differential elements, got {paths.Count}: " +
            string.Join(", ", paths.Select(p => p.Path + (p.SliceName is null ? "" : $":{p.SliceName}"))));

        Assert.AreEqual(("Extension", (string?)null), paths[0]);
        Assert.AreEqual(("Extension.extension", (string?)null), paths[1]);
        Assert.AreEqual(("Extension.url", (string?)null), paths[2]);
        Assert.AreEqual(("Extension.value[x]", (string?)null), paths[3]);

        var extChild = sd.Differential.Element.First(e => e.Path == "Extension.extension");
        Assert.AreEqual("0", extChild.Max);

        var urlEl = sd.Differential.Element.First(e => e.Path == "Extension.url");
        Assert.IsInstanceOfType<FhirUri>(urlEl.Fixed);
        Assert.AreEqual(
            $"{SdcCanonicalBase}/StructureDefinition/sdc-questionnaire-endpoint",
            ((FhirUri)urlEl.Fixed).Value);

        var valueEl = sd.Differential.Element.First(e => e.Path == "Extension.value[x]");
        Assert.IsNotNull(valueEl.Type);
        Assert.HasCount(1, valueEl.Type);
        Assert.AreEqual("uri", valueEl.Type[0].Code);
    }

    // ── CalculatedExpressionExtension.fsh-specific regressions ─────────────────

    private static readonly string CalculatedExpressionFshPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "SDC", "CalculatedExpressionExtension.fsh");

    private static StructureDefinition CompileCalculatedExpressionExtension()
    {
        Assert.IsTrue(File.Exists(CalculatedExpressionFshPath),
            $"Test data not found: {CalculatedExpressionFshPath}");

        var docs = new List<Hl7.FhirShorthand.Serialization.Models.FshDoc>
        {
            Parse(AliasesFshPath),
            Parse(CalculatedExpressionFshPath)
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
                          .FirstOrDefault(s => s.Name == "CalculatedExpressionExtension");
        Assert.IsNotNull(sd, "Expected a CalculatedExpressionExtension StructureDefinition");
        return sd!;
    }

    /// <summary>
    /// When a simple extension constrains <c>value[x]</c> to min=1 via <c>* value[x] 1..1</c>,
    /// the compiled differential must keep the explicit <c>min=1</c> but strip <c>max="1"</c>
    /// because it equals the inherited default from the base <c>Extension.value[x]</c>
    /// (cardinality 0..1).  Sushi omits the redundant max in this case.
    /// </summary>
    [TestMethod]
    public void SimpleExtension_ValueXStripsDefaultMaxOne()
    {
        var sd = CompileCalculatedExpressionExtension();
        var valueEl = sd.Differential.Element.First(e => e.Path == "Extension.value[x]");

        Assert.AreEqual(1, valueEl.Min, "Explicit min=1 must be preserved on value[x].");
        Assert.IsNull(valueEl.MaxElement,
            $"Extension.value[x] should not emit max=\"1\" (inherited default); got max=\"{valueEl.Max}\".");
    }

    /// <summary>
    /// <c>^contextInvariant</c> FSH caret rules must populate the SD top-level
    /// <c>contextInvariant</c> list.
    /// </summary>
    [TestMethod]
    public void SimpleExtension_ContextInvariantCaretRuleIsApplied()
    {
        var sd = CompileCalculatedExpressionExtension();
        Assert.IsNotNull(sd.ContextInvariant);
        CollectionAssert.Contains(sd.ContextInvariant.ToList(), "initial.exists().not()");
    }
}
