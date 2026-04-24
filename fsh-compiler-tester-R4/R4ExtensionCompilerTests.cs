using Hl7.Fhir.Model;

namespace Hl7.FhirShorthand.Compiler_tester_r4;

/// <summary>
/// Tests compiling FSH Extension entities to FHIR R4 StructureDefinitions.
/// </summary>
[TestClass]
public class R4ExtensionCompilerTests
{
    [TestMethod]
    public void ShouldCompileSimpleExtension()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Title: ""My Extension""
            Description: ""A simple extension""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.AreEqual("MyExtension", sd.Name);
        Assert.AreEqual("Extension", sd.Type);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.ComplexType, sd.Kind);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    [TestMethod]
    public void ShouldCompileExtensionWithParent()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Parent: Extension
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Extension", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCompileExtensionWithContext()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Context: Patient
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.IsNotNull(sd.Context, "Extension should have a Context");
        Assert.HasCount(1, sd.Context);
        Assert.AreEqual("Patient", sd.Context[0].Expression);
        Assert.AreEqual(StructureDefinition.ExtensionContextType.Element, sd.Context[0].Type);
    }

    [TestMethod]
    public void ShouldCompileExtensionWithCardinalityRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            * value[x] 0..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        var ed = CompilerTestHelper.GetElement(sd, "value[x]");
        // Both 0 (min) and "1" (max) match the inherited defaults for Extension.value[x]
        // (base cardinality 0..1) and are suppressed to match sushi output.
        Assert.AreEqual(0, ed.Min);
        Assert.IsNull(ed.MaxElement,
            $"max=\"1\" should be stripped (inherited default); got max=\"{ed.Max}\".");
    }

    // ─── Collection caret-value properties ───────────────────────────────────

    /// <summary>
    /// Verifies that a non-indexed <c>^contextInvariant</c> caret rule (which targets a
    /// <c>List&lt;FhirString&gt;</c> property on <see cref="StructureDefinition"/>) is
    /// appended to the collection rather than causing an <see cref="InvalidCastException"/>.
    /// This was the root cause of the FhirString→List&lt;FhirString&gt; cast bug observed
    /// when compiling SDC IG extensions.
    /// </summary>
    [TestMethod]
    public void ShouldApplyContextInvariantCaretRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            * ^context[+].type = #element
            * ^context[=].expression = ""Questionnaire.item""
            * ^contextInvariant = ""initial.exists().not()""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.IsNotNull(sd.ContextInvariantElement, "ContextInvariantElement should not be null");
        Assert.HasCount(1, sd.ContextInvariantElement, "Expected exactly one contextInvariant");
        Assert.AreEqual("initial.exists().not()", sd.ContextInvariantElement[0].Value);
    }

    /// <summary>
    /// Verifies that multiple non-indexed <c>^contextInvariant</c> caret rules each append
    /// an entry to the list, so the resulting collection has one entry per rule.
    /// </summary>
    [TestMethod]
    public void ShouldComposeIndentedRulePathsForExtension()
    {
        // C-FP1: Indented rules should have their paths composed from parent rules.
        // * extension[option]           ← PathRule: path = "extension[option]"
        //   * value[x] 1..1             ← should compose to "extension[option].value[x]"
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: ToggleExtension
            * extension contains option 1..*
            * extension[option]
              * value[x] 1..1
              * value[x] only Coding
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "ToggleExtension");
        Assert.IsNotNull(sd, "Extension should compile");
        var elements = sd.Differential.Element;
        // extension:option should be in the differential (as slice element)
        var optionElem = elements.FirstOrDefault(e => e.SliceName == "option");
        Assert.IsNotNull(optionElem, "extension:option should be in the differential");
        // There should be an element for extension[option].value[x] with path containing value[x]
        // (the indented value[x] rules should be resolved to extension.value[x] under the slice)
        var hasValueXElem = elements.Any(e => e.Path != null && e.Path.EndsWith("value[x]"));
        Assert.IsTrue(hasValueXElem, "Indented value[x] rule should be composed to extension.value[x]");
    }
}
