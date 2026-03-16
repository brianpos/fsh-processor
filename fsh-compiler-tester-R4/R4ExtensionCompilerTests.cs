using Hl7.Fhir.Model;

namespace fsh_compiler_tester_r4;

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
        Assert.AreEqual("Extension", sd.BaseDefinition);
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
        Assert.AreEqual(1, sd.Context.Count);
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
        Assert.AreEqual(0, ed.Min);
        Assert.AreEqual("1", ed.Max);
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
        Assert.AreEqual(1, sd.ContextInvariantElement.Count, "Expected exactly one contextInvariant");
        Assert.AreEqual("initial.exists().not()", sd.ContextInvariantElement[0].Value);
    }

    /// <summary>
    /// Verifies that multiple non-indexed <c>^contextInvariant</c> caret rules each append
    /// an entry to the list, so the resulting collection has one entry per rule.
    /// </summary>
    [TestMethod]
    public void ShouldAppendMultipleContextInvariantCaretRules()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            * ^contextInvariant = ""invariant-one""
            * ^contextInvariant = ""invariant-two""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyExtension");
        Assert.IsNotNull(sd.ContextInvariantElement);
        Assert.AreEqual(2, sd.ContextInvariantElement.Count, "Expected two contextInvariant entries");
        Assert.AreEqual("invariant-one", sd.ContextInvariantElement[0].Value);
        Assert.AreEqual("invariant-two", sd.ContextInvariantElement[1].Value);
    }
}
