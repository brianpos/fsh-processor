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

    [TestMethod]
    public void ShouldApplyConceptCodeContextFromIndentedCaretRules()
    {
        // C-CS4: Indented caret rules under a Concept should have their codes propagated
        // from the parent Concept rule so they apply to that concept (not the CodeSystem).
        // We test with the flat ^definition property which TrySet can set directly.
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: StatusCodes
            * #active ""Active""
              * ^definition = ""The active status""
            * #inactive ""Inactive""
              * ^definition = ""The inactive status""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "StatusCodes");
        Assert.IsNotNull(cs, "CodeSystem should compile");
        Assert.AreEqual(2, cs.Concept.Count, "Should have 2 concepts");
        var active = cs.Concept.FirstOrDefault(c => c.Code == "active");
        Assert.IsNotNull(active, "#active concept should exist");
        Assert.AreEqual("The active status", active.Definition,
            "Indented ^definition should be applied to #active concept");
        var inactive = cs.Concept.FirstOrDefault(c => c.Code == "inactive");
        Assert.IsNotNull(inactive, "#inactive concept should exist");
        Assert.AreEqual("The inactive status", inactive.Definition,
            "Indented ^definition should be applied to #inactive concept");
    }

    // ─── Quoted concept codes ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldStripQuotesFromSpacedConceptCodes()
    {
        // FSH allows codes with spaces to be quoted: #"More than half the days"
        // The resulting FHIR code value must NOT include the surrounding double-quotes.
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * #""More than half the days"" ""More than half the days""
            * #""Nearly every day"" ""Nearly every day""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.IsNotNull(cs.Concept);
        Assert.AreEqual(2, cs.Concept.Count);
        Assert.IsTrue(cs.Concept.Any(c => c.Code == "More than half the days"),
            "Code should be 'More than half the days' without extra quotes");
        Assert.IsTrue(cs.Concept.Any(c => c.Code == "Nearly every day"),
            "Code should be 'Nearly every day' without extra quotes");
    }

    // ─── Multi-level caret paths ──────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyMetaProfileViaCaretPath()
    {
        // * ^meta.profile = "..." should set Meta.Profile on the CodeSystem.
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^meta.profile = ""http://example.org/profile""
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.IsNotNull(cs.Meta, "meta should be set");
        Assert.IsNotNull(cs.Meta.Profile);
        Assert.IsTrue(cs.Meta.Profile.Any(p => p == "http://example.org/profile"),
            "meta.profile should contain the specified URL");
    }

    [TestMethod]
    public void ShouldCompileCodeSystemWithPropertyDefinitions()
    {
        // * ^property[+].code / uri / type — soft-index sequence creates a PropertyComponent.
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * ^property[+].code = #itemWeight
            * ^property[=].uri = ""http://hl7.org/fhir/concept-properties#itemWeight""
            * ^property[=].type = #decimal
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.IsNotNull(cs.Property, "property list should be set");
        Assert.AreEqual(1, cs.Property.Count, "Should have exactly one property definition");
        var prop = cs.Property[0];
        Assert.AreEqual("itemWeight", prop.Code, "property.code should be 'itemWeight'");
        Assert.AreEqual("http://hl7.org/fhir/concept-properties#itemWeight", prop.Uri,
            "property.uri should match");
        Assert.AreEqual(FhirCodeSystem.PropertyType.Decimal, prop.Type,
            "property.type should be 'decimal'");
    }

    [TestMethod]
    public void ShouldCompileConceptPropertyValuesViaIndentedCaretRules()
    {
        // Indented caret rules on concepts can set concept.property values via
        // property[+].code / property[=].valueDecimal soft-index sequences.
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCS
            * #not-at-all ""Not at all""
              * ^property[+].code = #itemWeight
              * ^property[=].valueDecimal = 0.0
            * #several-days ""Several days""
              * ^property[+].code = #itemWeight
              * ^property[=].valueDecimal = 1.0
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "MyCS");
        Assert.AreEqual(2, cs.Concept.Count);

        var c0 = cs.Concept.First(c => c.Code == "not-at-all");
        Assert.IsNotNull(c0.Property, "not-at-all should have property values");
        Assert.AreEqual(1, c0.Property.Count);
        Assert.AreEqual("itemWeight", c0.Property[0].Code);
        Assert.AreEqual(0m, ((FhirDecimal)c0.Property[0].Value).Value,
            "valueDecimal should be 0.0");

        var c1 = cs.Concept.First(c => c.Code == "several-days");
        Assert.IsNotNull(c1.Property, "several-days should have property values");
        Assert.AreEqual(1, c1.Property.Count);
        Assert.AreEqual("itemWeight", c1.Property[0].Code);
        Assert.AreEqual(1m, ((FhirDecimal)c1.Property[0].Value).Value,
            "valueDecimal should be 1.0");
    }

    [TestMethod]
    public void ShouldCompileCSPHQ9StyleCodeSystem()
    {
        // Full integration test mirroring the structure of CodeSystemCSPHQ9.fsh.
        // Verifies meta.profile, property definitions, quoted codes, and concept property values.
        var resources = CompilerTestHelper.CompileDoc(@"
            CodeSystem: CodeSystemCSPHQ9
            Id: CSPHQ9
            Title: ""SDC-CodeSystem PHQ9""
            Description: ""The answer list for questions 1 through 9 on the PHQ-9 form""
            * ^meta.profile = ""http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-codesystem""
            * ^status = #active
            * ^experimental = true
            * ^caseSensitive = true
            * ^content = #complete
            * ^property[+].code = #itemWeight
            * ^property[=].uri = ""http://hl7.org/fhir/concept-properties#itemWeight""
            * ^property[=].type = #decimal
            * #Not-at-all ""Not at all""
              * ^property[+].code = #itemWeight
              * ^property[=].valueDecimal = 0.0
            * #Several-days ""Several days""
              * ^property[+].code = #itemWeight
              * ^property[=].valueDecimal = 1.0
            * #""More than half the days"" ""More than half the days""
              * ^property[+].code = #itemWeight
              * ^property[=].valueDecimal = 2.0
            * #""Nearly every day"" ""Nearly every day""
              * ^property[+].code = #itemWeight
              * ^property[=].valueDecimal = 3.0
        ");
        var cs = CompilerTestHelper.GetCodeSystem(resources, "CodeSystemCSPHQ9");

        // meta.profile
        Assert.IsNotNull(cs.Meta, "meta should be present");
        Assert.IsTrue(cs.Meta.Profile.Contains("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-codesystem"),
            "meta.profile should match");

        // top-level scalar fields from caret rules
        Assert.AreEqual(PublicationStatus.Active, cs.Status);
        Assert.IsTrue(cs.Experimental == true);
        Assert.IsTrue(cs.CaseSensitive == true);
        Assert.AreEqual(CodeSystemContentMode.Complete, cs.Content);

        // property definition
        Assert.AreEqual(1, cs.Property?.Count, "Should have one property definition");
        var prop = cs.Property![0];
        Assert.AreEqual("itemWeight", prop.Code);
        Assert.AreEqual("http://hl7.org/fhir/concept-properties#itemWeight", prop.Uri);
        Assert.AreEqual(FhirCodeSystem.PropertyType.Decimal, prop.Type);

        // concept count
        Assert.AreEqual(4, cs.Count);
        Assert.AreEqual(4, cs.Concept.Count);

        // quoted codes should have NO surrounding double-quotes
        Assert.IsTrue(cs.Concept.Any(c => c.Code == "More than half the days"),
            "'More than half the days' code should not have extra quotes");
        Assert.IsTrue(cs.Concept.Any(c => c.Code == "Nearly every day"),
            "'Nearly every day' code should not have extra quotes");

        // concept property values
        var notAtAll = cs.Concept.First(c => c.Code == "Not-at-all");
        Assert.AreEqual(1, notAtAll.Property?.Count);
        Assert.AreEqual("itemWeight", notAtAll.Property![0].Code);
        Assert.AreEqual(0m, ((FhirDecimal)notAtAll.Property[0].Value).Value);

        var moreHalf = cs.Concept.First(c => c.Code == "More than half the days");
        Assert.AreEqual(1, moreHalf.Property?.Count);
        Assert.AreEqual(2m, ((FhirDecimal)moreHalf.Property![0].Value).Value);
    }

    // ─── FshCode → CodeableConcept with alias-qualified system ───────────────

    [TestMethod]
    public void ShouldCompileJurisdictionFromSystemQualifiedCode()
    {
        // FSH spec: assigning a system-qualified code ($alias#code "display") to a
        // CodeableConcept property creates CodeableConcept { coding: [{ system, code, display }] }.
        var fsh = CompilerTestHelper.LeftAlign(@"
            Alias: $m49.htm = http://unstats.un.org/unsd/methods/m49/m49.htm

            CodeSystem: JurisdictionCS
            * ^jurisdiction = $m49.htm#001 ""World""
        ");
        var parseResult = fsh_processor.FshParser.Parse(fsh);
        var fshDoc = ((fsh_processor.Models.ParseResult.Success)parseResult).Document;
        var opts = new fsh_compiler.CompilerOptions { CanonicalBase = "http://example.org/fhir" };
        var result = fsh_compiler_r4.R4FshCompiler.Compile(fshDoc, opts);
        var cs = (FhirCodeSystem)((fsh_compiler.CompileResult<System.Collections.Generic.List<Hl7.Fhir.Model.Resource>>.SuccessResult)result).Value[0];

        Assert.IsNotNull(cs.Jurisdiction, "jurisdiction should be set");
        Assert.AreEqual(1, cs.Jurisdiction.Count, "Should have one jurisdiction entry");
        var cc = cs.Jurisdiction[0];
        Assert.AreEqual(1, cc.Coding.Count, "CodeableConcept should have one Coding");
        var coding = cc.Coding[0];
        Assert.AreEqual("http://unstats.un.org/unsd/methods/m49/m49.htm", coding.System,
            "Alias should be resolved to the canonical URL");
        Assert.AreEqual("001", coding.Code, "Code should be '001'");
        Assert.AreEqual("World", coding.Display, "Display should be 'World'");
    }

    [TestMethod]
    public void ShouldCompileCHFCodesStyleJurisdiction()
    {
        // Mirror the exact CHFCodes FSH structure that originally triggered this bug.
        var resources = CompilerTestHelper.CompileDocs(
            CompilerTestHelper.ParseDoc(@"Alias: $m49.htm = http://unstats.un.org/unsd/methods/m49/m49.htm"),
            CompilerTestHelper.ParseDoc(@"
                CodeSystem: CHFCodes
                Id: chf-codes
                Title: ""CHF Codes""
                * ^jurisdiction = $m49.htm#001 ""World""
                * #body-weight-change ""Body weight change""
            ")
        );
        var cs = CompilerTestHelper.GetCodeSystem(resources, "CHFCodes");
        Assert.IsNotNull(cs.Jurisdiction, "jurisdiction should be set");
        Assert.AreEqual(1, cs.Jurisdiction.Count);
        var coding = cs.Jurisdiction[0].Coding[0];
        Assert.AreEqual("http://unstats.un.org/unsd/methods/m49/m49.htm", coding.System);
        Assert.AreEqual("001", coding.Code);
        Assert.AreEqual("World", coding.Display);
    }
}
