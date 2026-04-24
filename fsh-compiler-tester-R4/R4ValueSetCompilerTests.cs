using Hl7.Fhir.Model;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;

namespace Hl7.FhirShorthand.Compiler_tester_r4;

/// <summary>
/// Tests compiling FSH ValueSet entities to FHIR R4 ValueSet resources.
/// </summary>
[TestClass]
public class R4ValueSetCompilerTests
{
    [TestMethod]
    public void ShouldCompileSimpleValueSet()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            Title: ""My Value Set""
            Description: ""A simple value set""
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.AreEqual("MyValueSet", vs.Name);
        Assert.AreEqual("My Value Set", vs.Title);
        Assert.AreEqual("A simple value set", vs.Description);
        Assert.AreEqual(PublicationStatus.Active, vs.Status);
    }

    [TestMethod]
    public void ShouldCompileValueSetWithInclude()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * include codes from system http://loinc.org
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.IsNotNull(vs.Compose);
        Assert.HasCount(1, vs.Compose.Include);
        Assert.AreEqual("http://loinc.org", vs.Compose.Include[0].System);
    }

    [TestMethod]
    public void ShouldPreserveUrnCodeSystemInSingleCodeInclude()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * urn:ietf:bcp:13#application/sql ""SQL""
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.IsNotNull(vs.Compose);
        Assert.HasCount(1, vs.Compose.Include);
        // Per language-reference include coding syntax, explicit system identifiers are
        // used as provided; URN systems must not be canonical-base rewritten.
        Assert.AreEqual("urn:ietf:bcp:13", vs.Compose.Include[0].System);
    }

    [TestMethod]
    public void ShouldCompileValueSetWithExclude()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * include codes from system http://loinc.org
            * exclude codes from system http://snomed.info/sct
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.IsNotNull(vs.Compose);
        Assert.HasCount(1, vs.Compose.Include);
        Assert.HasCount(1, vs.Compose.Exclude);
        Assert.AreEqual("http://snomed.info/sct", vs.Compose.Exclude[0].System);
    }

    [TestMethod]
    public void ShouldCompileValueSetWithCaretValueTitle()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * ^title = ""Override Title""
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.AreEqual("Override Title", vs.Title);
    }

    // ─── InsertRule expansion ─────────────────────────────────────────────────

    [TestMethod]
    public void ShouldExpandInsertRuleInValueSet()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            RuleSet: VSMeta
            * ^description = ""Injected VS description""

            ValueSet: MyValueSet
            * insert VSMeta
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.AreEqual("Injected VS description", vs.Description);
    }

    // ─── Code-level caret value rules (Gap 9) ────────────────────────────────

    [TestMethod]
    public void ShouldApplyCodeCaretValueRuleToConceptDisplay()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            * include #active from system http://example.org/status
            * #active ^display = ""Active Patient""
        ");
        var vs = CompilerTestHelper.GetValueSet(resources, "MyValueSet");
        Assert.IsNotNull(vs.Compose);
        var concept = vs.Compose.Include
            .SelectMany(i => i.Concept ?? Enumerable.Empty<FhirValueSet.ConceptReferenceComponent>())
            .FirstOrDefault(c => c.Code == "active");
        Assert.IsNotNull(concept, "Code 'active' should be in the include");
        Assert.AreEqual("Active Patient", concept.Display);
    }
}
