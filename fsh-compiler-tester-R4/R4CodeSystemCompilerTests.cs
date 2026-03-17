using Hl7.Fhir.Model;
using FhirCodeSystem = Hl7.Fhir.Model.CodeSystem;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH CodeSystem entities to FHIR R4 CodeSystem resources.
/// </summary>
[TestClass]
public class R4CodeSystemCompilerTests
{
    [TestMethod]
    public void ShouldCompileSimpleCodeSystem()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            Title: ""My Code System""
            Description: ""A simple code system""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCodeSystem");
        Assert.AreEqual("MyCodeSystem", cs.Name);
        Assert.AreEqual("My Code System", cs.Title);
        Assert.AreEqual("A simple code system", cs.Description);
        Assert.AreEqual(PublicationStatus.Active, cs.Status);
        Assert.AreEqual(CodeSystemContentMode.Complete, cs.Content);
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithConcepts()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * #active ""Active"" ""An active status""
            * #inactive ""Inactive""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.IsNotNull(cs.Concept);
        Assert.AreEqual(2, cs.Concept.Count);

        var active = cs.Concept.First(c => c.Code == "active");
        Assert.AreEqual("Active", active.Display);
        Assert.AreEqual("An active status", active.Definition);

        var inactive = cs.Concept.First(c => c.Code == "inactive");
        Assert.AreEqual("Inactive", inactive.Display);
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithCaretValueDescription()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^description = ""Override description""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual("Override description", cs.Description);
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithCaseSensitive()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^caseSensitive = true
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.IsTrue(cs.CaseSensitive);
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithHierarchyMeaning()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^hierarchyMeaning = ""is-a""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual(FhirCodeSystem.CodeSystemHierarchyMeaning.IsA, cs.HierarchyMeaning);
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithPublisher()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^publisher = ""HL7 International""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual("HL7 International", cs.Publisher);
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithStatus()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^status = ""retired""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual(PublicationStatus.Retired, cs.Status);
    }

    // ─── Per-concept caret value ──────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyPerConceptCaretValue()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * #active ""Active""
            * #inactive ""Inactive""
            * #active ^definition = ""The active status""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        var activeConcept = cs.Concept.First(c => c.Code == "active");
        Assert.AreEqual("The active status", activeConcept.Definition);
        // Other concepts should be unaffected
        var inactiveConcept = cs.Concept.First(c => c.Code == "inactive");
        Assert.IsNull(inactiveConcept.Definition);
    }

    // ─── InsertRule expansion ─────────────────────────────────────────────────

    [TestMethod]
    public void ShouldExpandInsertRuleInCodeSystem()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            RuleSet: CSMeta
            * ^description = ""Injected description""

            CodeSystem: MyCS
            * insert CSMeta
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual("Injected description", cs.Description);
    }

    // ─── Count and URL ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldComputeCodeSystemCount()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * #active ""Active""
            * #inactive ""Inactive""
            * #draft ""Draft""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual(3, cs.Count, "CodeSystem.Count should equal the total number of concepts");
    }

    [TestMethod]
    public void ShouldUseCodeSystemPathSegmentInUrl()
    {
        var fsh = CompilerTestHelper.LeftAlign(@"
            CodeSystem: MyCS
            Id: my-cs
        ");
        var doc = fsh_processor.FshParser.Parse(fsh);
        var fshDoc = ((fsh_processor.Models.ParseResult.Success)doc).Document;
        var opts = new fsh_compiler.CompilerOptions { CanonicalBase = "http://example.org/fhir" };
        var result = fsh_compiler.FshCompiler.Compile(fshDoc, opts);
        var cs = (FhirCodeSystem)((fsh_compiler.CompileResult<System.Collections.Generic.List<Hl7.Fhir.Model.Resource>>.SuccessResult)result).Value[0];
        Assert.AreEqual("http://example.org/fhir/CodeSystem/my-cs", cs.Url,
            "CodeSystem URL should use /CodeSystem/ segment");
    }
}
