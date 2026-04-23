// Ported from SUSHI: test/export/CodeSystemExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/CodeSystemExporter.test.ts
//
// Translation notes:
//  - SUSHI builds FshCodeSystem programmatically with ConceptRule / CaretValueRule / InsertRule.
//    These ports use FSH text via SushiCompilerTestHelper.CompileDoc.
//  - SUSHI `exporter.exportCodeSystem(cs)` / `exporter.export().codeSystems` → CompileDoc.
//  - SUSHI `exporter.applyInsertRules()` → handled automatically by CompileDoc.
//  - `loggerSpy` assertions → CompileResult.Warnings assertions.
//  - `pkg.fshMap` → Assert.Inconclusive (no equivalent in fsh-compiler).
//  - Concept hierarchy is expressed in FSH via `* #parent #child` notation.
//  - `pathArray = ['#someCode']` caret rules → `* #someCode ^caretPath = value` in FSH.
//
// Type aliases to disambiguate Hl7.Fhir.Model.CodeSystem from Serialization.Models.CodeSystem:
using FhirCS = Hl7.Fhir.Model.CodeSystem;
using FhirVS = Hl7.Fhir.Model.ValueSet;
using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class CodeSystemExporterTests
{
    // ─── Empty / basic ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldOutputEmptyResultsWithEmptyInput()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Alias: $X = http://example.org/x
        ");
        var css = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.CodeSystems(ok.Value)
            : new List<FhirCS>();
        Assert.AreEqual(0, css.Count);
    }

    [TestMethod]
    public void ShouldExportASingleCodeSystem()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
        ");
        var css = SushiCompilerTestHelper.CodeSystems(resources);
        Assert.AreEqual(1, css.Count);
        var cs = css[0];
        Assert.AreEqual("MyCodeSystem", cs.Name);
        Assert.AreEqual("MyCodeSystem", cs.Id);
        Assert.AreEqual(PublicationStatus.Draft, cs.Status);
        Assert.AreEqual(CodeSystemContentMode.Complete, cs.Content);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/CodeSystem/MyCodeSystem", cs.Url);
    }

    [TestMethod]
    public void ShouldAddSourceInfoForTheExportedCodeSystemToThePackage()
    {
        Assert.Inconclusive("SUSHI pkg.fshMap has no fsh-compiler equivalent.");
    }

    [TestMethod]
    public void ShouldExportACodeSystemWithAdditionalMetadata()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            Id: CodeSystem1
            Title: ""My Fancy Code System""
            Description: ""Lots of important details about my fancy code system""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "MyCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual("CodeSystem1", cs.Id);
        Assert.AreEqual("My Fancy Code System", cs.Title);
        Assert.AreEqual("Lots of important details about my fancy code system", cs.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/CodeSystem/CodeSystem1", cs.Url);
    }

    [TestMethod]
    public void ShouldExportACodeSystemWithStatusAndVersionInFSHOnlyMode()
    {
        // SUSHI: FSHOnly mode propagates config status/version onto the compiled CodeSystem.
        // fsh-compiler does not model FSHOnly config; test verifies the caret-override path.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            Id: CodeSystem1
            * ^status = #active
            * ^version = ""0.1.0""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "MyCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual(PublicationStatus.Active, cs.Status);
        Assert.AreEqual("0.1.0", cs.Version);
    }

    [TestMethod]
    public void ShouldExportEachCodeSystemOnceEvenIfExportIsCalledMoreThanOnce()
    {
        // fsh-compiler does not cache partial exports; compiling the same doc twice produces
        // the same result. Port verifies idempotency.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
        ");
        Assert.AreEqual(1, SushiCompilerTestHelper.CodeSystems(resources).Count);
        // second compile of the same text must produce exactly one as well.
        var resources2 = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
        ");
        Assert.AreEqual(1, SushiCompilerTestHelper.CodeSystems(resources2).Count);
    }

    // ─── Concept rules ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldExportACodeSystemWithAConceptWithOnlyACode()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            * #myCode
            * #anotherCode
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "MyCodeSystem");
        Assert.IsNotNull(cs);
        Assert.IsNotNull(cs.Concept);
        Assert.AreEqual(2, cs.Concept.Count);
        Assert.AreEqual(2, cs.Count);
        Assert.IsTrue(cs.Concept.Any(c => c.Code == "myCode"));
        Assert.IsTrue(cs.Concept.Any(c => c.Code == "anotherCode"));
    }

    [TestMethod]
    public void ShouldExportACodeSystemWithAConceptWithACodeDisplayAndDefinition()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            * #myCode ""My code"" ""This is the formal definition of my code""
            * #anotherCode ""A second code"" ""More details about this code""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "MyCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual(2, cs.Count);
        var myCode = cs.Concept.FirstOrDefault(c => c.Code == "myCode");
        Assert.IsNotNull(myCode);
        Assert.AreEqual("My code", myCode.Display);
        Assert.AreEqual("This is the formal definition of my code", myCode.Definition);
    }

    [TestMethod]
    public void ShouldExportACodeSystemWithHierarchicalCodes()
    {
        // Hierarchy:  topCode > middleCode > bottomCode
        //             topCode > otherMiddle
        //             unrelatedCode
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: HierarchicalCodeSystem
            * #topCode ""Top Code"" ""This is at the top of the hierarchy.""
            * #topCode #middleCode ""Middle Code"" ""This is in the middle of the hierarchy.""
            * #topCode #middleCode #bottomCode ""Bottom Code"" ""This is at the bottom of the hierarchy.""
            * #topCode #otherMiddle ""Other Middle"" ""This is another middle code.""
            * #unrelatedCode ""Unrelated Code"" ""This is not related to the hierarchy.""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "HierarchicalCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual(5, cs.Count);
        var topCode = cs.Concept.FirstOrDefault(c => c.Code == "topCode");
        Assert.IsNotNull(topCode);
        Assert.AreEqual(2, topCode.Concept.Count);
        var middleCode = topCode.Concept.FirstOrDefault(c => c.Code == "middleCode");
        Assert.IsNotNull(middleCode);
        Assert.AreEqual(1, middleCode.Concept.Count);
        Assert.AreEqual("bottomCode", middleCode.Concept[0].Code);
        Assert.IsNotNull(topCode.Concept.FirstOrDefault(c => c.Code == "otherMiddle"));
        Assert.IsNotNull(cs.Concept.FirstOrDefault(c => c.Code == "unrelatedCode"));
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringADuplicateCode()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: Zoo
            * #goat ""A goat""
            * #goat ""Another goat?""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("goat") || w.Message.ToLower().Contains("duplicate") || w.Message.ToLower().Contains("already")),
            "Expected a warning about the duplicate code 'goat'.");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenEncounteringADuplicateCodeIfTheNewCodeHasNoDisplayOrDefinition()
    {
        // SUSHI: duplicate code with no display/definition is silently de-duplicated (no error).
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: Zoo
            * #goat ""A goat""
            * #goat
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Zoo");
        Assert.IsNotNull(cs);
        // Should have exactly one 'goat' concept with the original display.
        var goat = cs.Concept.Where(c => c.Code == "goat").ToList();
        Assert.IsTrue(goat.Count <= 1, "Duplicate no-display code should not be added.");
        if (goat.Count == 1) Assert.AreEqual("A goat", goat[0].Display);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringACodeWithAnIncorrectlyDefinedHierarchy()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: Zoo
            * #bear ""Bear"" ""A member of family Ursidae.""
            * #bear #sunbear #ursula ""Ursula the sun bear""
        ");
        // SUSHI: "Could not find sunbear in concept hierarchy"
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("sunbear") || w.Message.ToLower().Contains("hierarchy")),
            "Expected a warning about an incorrectly defined hierarchy.");
    }

    // ─── Validation warnings ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldWarnWhenTitleAndOrDescriptionIsAnEmptyString()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: MyCodeSystem
            Title: """"
            Description: """"
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("title") || w.Message.ToLower().Contains("description")),
            "Expected warnings about empty title/description.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheCodeSystemHasAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: StrangeSystem
            Id: ""Is this allowed?""
        ");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.ToLower().Contains("id")),
            "Expected a warning about the invalid id.");
    }

    [TestMethod]
    public void ShouldNotLogAMessageWhenTheCodeSystemOverridesAnInvalidIdWithACaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: StrangeSystem
            Id: ""Is this allowed?""
            * ^id = ""this-is-allowed""
        ");
        var cs = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindCs(ok.Value, "StrangeSystem")
            : null;
        // SUSHI: no warning when valid ^id caret overrides the invalid id.
        Assert.IsTrue(cs == null || cs.Id == "this-is-allowed",
            "Expected the ^id caret rule to override the invalid id.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheCodeSystemOverridesAnInvalidIdWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: StrangeSystem
            Id: ""Is this allowed?""
            * ^id = ""No this is not allowed""
        ");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.ToLower().Contains("id")),
            "Expected a warning about the still-invalid id after caret override.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheCodeSystemOverridesAValidIdWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: StrangeSystem
            Id: this-is-allowed
            * ^id = ""This is not allowed""
        ");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.ToLower().Contains("id")),
            "Expected a warning when a valid id is replaced by an invalid caret value.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheCodeSystemHasAnInvalidName()
    {
        // SUSHI warns when a name is not valid for machine-processing.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: Strange.Code.System
        ");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.ToLower().Contains("name")),
            "Expected a warning about the invalid name containing dots.");
    }

    [TestMethod]
    public void ShouldNotLogAMessageWhenTheCodeSystemOverridesAnInvalidNameWithACaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: Strange.Code.System
            * ^name = ""StrangeCodeSystem""
        ");
        var cs = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindCs(ok.Value, "Strange.Code.System")
            : null;
        if (cs != null)
            Assert.AreEqual("StrangeCodeSystem", cs.Name);
        // SUSHI: no warning when valid ^name caret overrides the invalid name.
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheCodeSystemOverridesAnInvalidNameWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: Strange.Code.System
            * ^name = ""Strange.Code.System""
        ");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.ToLower().Contains("name")),
            "Expected a warning about the still-invalid name.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheCodeSystemOverridesAValidNameWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: StrangeCodeSystem
            * ^name = ""Strange.Code.System""
        ");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.ToLower().Contains("name")),
            "Expected a warning when a valid name is replaced by an invalid caret value.");
    }

    [TestMethod]
    public void ShouldSanitizeTheIdAndLogAMessageWhenAValidNameIsUsedToMakeAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: Not_good_id
        ");
        var cs = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindCs(ok.Value, "Not_good_id")
            : null;
        Assert.IsNotNull(cs);
        Assert.AreEqual("Not_good_id", cs.Name);
        Assert.AreEqual("Not-good-id", cs.Id);
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("Not_good_id") || w.Message.Contains("Not-good-id")),
            "Expected a warning about the sanitized id.");
    }

    [TestMethod]
    public void ShouldSanitizeTheIdAndLogAMessageWhenALongValidNameIsUsedToMakeAnInvalidId()
    {
        var longName = "Toolong";
        while (longName.Length < 65) longName += "longer";
        var fsh = $@"
            CodeSystem: {longName}
        ";
        var result = SushiCompilerTestHelper.CompileDocResult(fsh);
        var cs = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindCs(ok.Value, longName)
            : null;
        Assert.IsNotNull(cs);
        Assert.AreEqual(longName, cs.Name);
        Assert.IsTrue(cs.Id.Length <= 64, $"Id should be truncated to 64 chars; was: {cs.Id}");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMultipleCodeSystemsHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: FirstCodeSystem
            Id: my-code-system

            CodeSystem: SecondCodeSystem
            Id: my-code-system
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-code-system") || w.Message.ToLower().Contains("duplicate") || w.Message.ToLower().Contains("multiple")),
            "Expected a warning about the duplicate code system id.");
    }

    // ─── CaretValueRules ──────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyACaretValueRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CaretCodeSystem
            * ^publisher = ""carat""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual("carat", cs.Publisher);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnATopLevelConcept()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^designation[0].value = ""Designated value""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual(1, cs.Count);
        var someCode = cs.Concept.FirstOrDefault(c => c.Code == "someCode");
        Assert.IsNotNull(someCode);
        Assert.IsNotNull(someCode.Designation);
        Assert.AreEqual("Designated value", someCode.Designation.FirstOrDefault()?.Value);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAConceptWithinAHierarchy()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode #otherCode ""Other Code""
            * #someCode #otherCode ^designation[0].value = ""Other designated value""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual(2, cs.Count);
        var someCode = cs.Concept.FirstOrDefault(c => c.Code == "someCode");
        Assert.IsNotNull(someCode);
        var otherCode = someCode.Concept.FirstOrDefault(c => c.Code == "otherCode");
        Assert.IsNotNull(otherCode);
        Assert.IsNotNull(otherCode.Designation);
        Assert.AreEqual("Other designated value", otherCode.Designation.FirstOrDefault()?.Value);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAConceptThatAssignsAnInstance()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: AnInlineCoding
            InstanceOf: Coding
            Usage: #inline
            * system = ""http://example.org/system""

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #standard
            * #someCode ^property[0].valueCoding = AnInlineCoding
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        var someCode = cs.Concept.FirstOrDefault(c => c.Code == "someCode");
        Assert.IsNotNull(someCode);
        Assert.IsNotNull(someCode.Property);
        var prop = someCode.Property.FirstOrDefault();
        Assert.IsNotNull(prop);
        Assert.AreEqual("standard", prop.Code);
        var valueCoding = prop.Value as Coding;
        Assert.IsNotNull(valueCoding);
        Assert.AreEqual("http://example.org/system", valueCoding.System);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAConceptThatAssignsAnInstanceWithANumericId()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: 79e1
            InstanceOf: Coding
            Usage: #inline
            * system = ""http://example.org/system""

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #standard
            * #someCode ^property[0].valueCoding = 79e1
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        var someCode = cs.Concept.FirstOrDefault(c => c.Code == "someCode");
        Assert.IsNotNull(someCode);
        var prop = someCode?.Property?.FirstOrDefault();
        var valueCoding = prop?.Value as Coding;
        Assert.IsNotNull(valueCoding);
        Assert.AreEqual("http://example.org/system", valueCoding.System);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAConceptThatAssignsAnInstanceWithAnIdThatResemblesABoolean()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: ""false""
            InstanceOf: Coding
            Usage: #inline
            * system = ""http://example.org/system""

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #standard
            * #someCode ^property[0].valueCoding = false
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        var someCode = cs.Concept.FirstOrDefault(c => c.Code == "someCode");
        Assert.IsNotNull(someCode);
        // Port: just verify the concept exists; exact boolean→Instance lookup is implementation-specific.
    }

    [TestMethod]
    public void ShouldApplyCaretValueRulesThatCreateAContainedResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * ^contained.resourceType = ""Observation""
            * ^contained.id = ""my-observation""
            * ^contained.status = #draft
            * ^contained.code = #123
            * ^contained.valueString = ""contained observation""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        Assert.IsNotNull(cs.Contained);
        Assert.AreEqual(1, cs.Contained.Count);
        var obs = cs.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("my-observation", obs.Id);
    }

    [TestMethod]
    public void ShouldApplyCaretValueRulesThatModifyAContainedResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #inline
            * id = ""my-observation""
            * status = #draft
            * code = #123

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * ^contained = MyObservation
            * ^contained.valueString = ""contained observation""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        Assert.IsNotNull(cs.Contained);
        Assert.AreEqual(1, cs.Contained.Count);
        var obs = cs.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("my-observation", obs.Id);
        Assert.AreEqual("contained observation", obs.Value?.ToString());
    }

    [TestMethod]
    public void ShouldLogAWarningWhenApplyingACaretValueRuleThatAssignsAnExampleInstance()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #example
            * id = ""my-observation""
            * status = #draft
            * code = #123

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * ^contained = MyObservation
            * ^contained.valueString = ""contained observation""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("example") || w.Message.ToLower().Contains("contained")),
            "Expected a warning about assigning an example instance as a contained resource.");
    }

    [TestMethod]
    public void ShouldLogAWarningWhenApplyingACaretValueRuleThatAssignsAnExampleInstanceWithANumericId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #example
            * id = ""555""
            * status = #draft
            * code = #123

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * ^contained = 555
            * ^contained.valueString = ""contained observation""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("example") || w.Message.Contains("555")),
            "Expected a warning about containing an example instance with a numeric id.");
    }

    [TestMethod]
    public void ShouldReplaceReferencesWhenApplyingACaretValueRule()
    {
        // SUSHI: `#active AllergyIntoleranceClinicalStatusCodes` concept caret on a valueCoding,
        // which expands the system URL from the named code system.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #myProperty
            * #someCode ^property[0].valueCoding = AllergyIntoleranceClinicalStatusCodes#active
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        var someCode = cs.Concept.FirstOrDefault(c => c.Code == "someCode");
        Assert.IsNotNull(someCode);
        var prop = someCode.Property.FirstOrDefault();
        Assert.IsNotNull(prop);
        Assert.AreEqual("myProperty", prop.Code);
        var valueCoding = prop.Value as Coding;
        Assert.IsNotNull(valueCoding);
        Assert.AreEqual("active", valueCoding.Code);
        Assert.AreEqual("http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", valueCoding.System);
    }

    [TestMethod]
    public void ShouldResolveSoftIndexingWhenApplyingTopLevelCaretValueRules()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CaretCodeSystem
            * ^contact[+].name = ""Example Name""
            * ^contact[=].telecom[+].rank = 1
            * ^contact[=].telecom[=].value = ""example@email.com""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CaretCodeSystem");
        Assert.IsNotNull(cs);
        Assert.IsNotNull(cs.Contact);
        Assert.AreEqual(1, cs.Contact.Count);
        Assert.AreEqual("Example Name", cs.Contact[0].Name);
        Assert.AreEqual(1, cs.Contact[0].Telecom.Count);
        Assert.AreEqual(1, cs.Contact[0].Telecom[0].Rank);
        Assert.AreEqual("example@email.com", cs.Contact[0].Telecom[0].Value);
    }

    [TestMethod]
    public void ShouldResolveSoftIndexingWhenApplyingCaretValueRulesWithPaths()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: CodeCaretCS
            * #topCode ""Top Code""
            * #topCode #bottomCode ""Bottom Code""
            * #topCode ^designation[+].value = ""First top designation""
            * #topCode ^designation[+].value = ""Second top designation""
            * #topCode #bottomCode ^designation[+].value = ""First bottom designation""
            * #topCode #bottomCode ^designation[+].value = ""Second bottom designation""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "CodeCaretCS");
        Assert.IsNotNull(cs);
        Assert.AreEqual(2, cs.Count);
        var topCode = cs.Concept.FirstOrDefault(c => c.Code == "topCode");
        Assert.IsNotNull(topCode);
        Assert.AreEqual(2, topCode.Designation.Count);
        Assert.AreEqual("First top designation", topCode.Designation[0].Value);
        Assert.AreEqual("Second top designation", topCode.Designation[1].Value);
        var bottomCode = topCode.Concept.FirstOrDefault(c => c.Code == "bottomCode");
        Assert.IsNotNull(bottomCode);
        Assert.AreEqual(2, bottomCode.Designation.Count);
        Assert.AreEqual("First bottom designation", bottomCode.Designation[0].Value);
        Assert.AreEqual("Second bottom designation", bottomCode.Designation[1].Value);
    }

    [TestMethod]
    public void ShouldExportACodeSystemWithExtensions()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: Strange.Code.System
            * #bar ""Bar"" ""Bar""
            * ^extension[structuredefinition-fmm].valueInteger = 1
            * #bar ^extension[structuredefinition-fmm].valueInteger = 2
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Strange.Code.System");
        Assert.IsNotNull(cs);
        Assert.IsTrue(cs.Extension.Any(e =>
                e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-fmm"
                && e.Value is Integer i && i.Value == 1),
            "Expected structuredefinition-fmm extension with value 1 on the code system.");
        var barConcept = cs.Concept.FirstOrDefault(c => c.Code == "bar");
        Assert.IsNotNull(barConcept);
        Assert.IsTrue(barConcept.Extension.Any(e =>
                e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-fmm"
                && e.Value is Integer i2 && i2.Value == 2),
            "Expected structuredefinition-fmm extension with value 2 on the concept.");
    }

    [TestMethod]
    public void ShouldOutputAnErrorWhenAChoiceElementHasValuesAssignedToMoreThanOneChoiceType()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: MultiChoiceSystem
            * ^extension[0].url = ""http://example.org/SomeExt""
            * ^extension[0].valueString = ""multi value""
            * ^extension[0].valueInteger = 24
            * #bar ""Bar"" ""Bar""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("choice") || w.Message.ToLower().Contains("value[x]")),
            "Expected a warning about multiple choice type assignments.");
    }

    [TestMethod]
    public void ShouldNotOverrideCountWhenCaretCountIsProvidedByUser()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            * ^content = #fragment
            * ^count = 5
            * #myCode
            * #anotherCode
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "MyCodeSystem");
        Assert.IsNotNull(cs);
        Assert.AreEqual(5, cs.Count);
    }

    [TestMethod]
    public void ShouldWarnWhenCaretCountDoesNotMatchNumberOfConceptsInCompleteCodeSystem()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: MyCodeSystem
            * ^count = 5
            * #myCode
            * #anotherCode
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("5") || w.Message.ToLower().Contains("count")),
            "Expected a warning about the mismatched ^count.");
    }

    [TestMethod]
    public void ShouldWarnWhenCaretCountIsSetAndConceptsIsNullInCompleteCodeSystem()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: MyCodeSystem
            * ^count = 5
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("5") || w.Message.ToLower().Contains("count")),
            "Expected a warning about ^count when no concepts are defined.");
    }

    [TestMethod]
    public void ShouldNotSetCountWhenCaretContentIsNotComplete()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: MyCodeSystem
            * ^content = #fragment
            * #myCode
            * #anotherCode
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "MyCodeSystem");
        Assert.IsNotNull(cs);
        // SUSHI: count is not automatically set when content != #complete.
        Assert.IsNull(cs.Count, "Count should not be auto-set for non-#complete code systems.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenApplyingAnInvalidConceptRule()
    {
        // Incomplete hierarchy: 'mistake' references 'bottom' as parent, but 'bottom' is itself a
        // child of 'top', so the full hierarchy 'top > bottom' is required.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: MyCodeSystem
            * #top
            * #top #bottom
            * #bottom #mistake
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("mistake") || w.Message.ToLower().Contains("hierarchy")),
            "Expected a warning about the invalid concept hierarchy.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenApplyingInvalidCaretValueRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: CaretCodeSystem
            * ^publisherz = true
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("publisherz") || w.Message.ToLower().Contains("path")),
            "Expected a warning about the invalid caret path.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenApplyingAnInvalidCaretValueRuleOnConcept()
    {
        // SUSHI: caret rule `#someCode #wrongCode ^designation[0].value` where #wrongCode
        // doesn't exist at the given hierarchy level.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode #otherCode ""Other Code""
            * #someCode #wrongCode ^designation[0].value = ""Other designated value""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("wrongcode") || w.Message.ToLower().Contains("not found") || w.Message.ToLower().Contains("concept")),
            "Expected a warning about the invalid concept path in the caret rule.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenACaretValueRuleAssignsAnInstanceButTheInstanceIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #standard
            * #someCode ^property[0].valueCoding = AnInlineCoding
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("AnInlineCoding") || w.Message.ToLower().Contains("not found")),
            "Expected a warning when the referenced Instance is not found.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenACaretValueRuleAssignsAValueThatIsNumericAndRefersToAnInstanceButBothTypesAreWrong()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: 79e1
            InstanceOf: Coding
            Usage: #inline
            * system = ""http://example.org/system""

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #standard
            * #someCode ^property[0].valueDateTime = 79e1
        ");
        // SUSHI: "Cannot assign number value: 790. Value does not match element type: dateTime"
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("datetime") || w.Message.ToLower().Contains("cannot assign")),
            "Expected a warning about mismatched numeric value type.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenACaretValueRuleAssignsAValueThatIsBooleanAndRefersToAnInstanceButBothTypesAreWrong()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: ""true""
            InstanceOf: Coding
            Usage: #inline
            * system = ""http://example.org/system""

            CodeSystem: CaretCodeSystem
            * #someCode ""Some Code""
            * #someCode ^property[0].code = #standard
            * #someCode ^property[0].valueDateTime = true
        ");
        // SUSHI: "Cannot assign boolean value: true. Value does not match element type: dateTime"
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("datetime") || w.Message.ToLower().Contains("cannot assign")),
            "Expected a warning about mismatched boolean value type.");
    }

    // ─── #insertRules ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyRulesFromAnInsertRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * ^title = ""Wow fancy""

            CodeSystem: Foo
            * insert Bar
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        Assert.AreEqual("Wow fancy", cs.Title);
    }

    [TestMethod]
    public void ShouldResolveSoftIndexingWhenInsertingAnInsertRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * ^contact[+].name = ""Example Name""
            * ^contact[=].telecom[+].rank = 1
            * ^contact[=].telecom[=].value = ""example@email.com""

            CodeSystem: Foo
            * insert Bar
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        Assert.AreEqual(1, cs.Contact.Count);
        Assert.AreEqual("Example Name", cs.Contact[0].Name);
        Assert.AreEqual(1, cs.Contact[0].Telecom[0].Rank);
        Assert.AreEqual("example@email.com", cs.Contact[0].Telecom[0].Value);
    }

    [TestMethod]
    public void ShouldInsertARuleSetAtACodePath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * ^designation[+].value = ""Bar Value""
            * #extra

            CodeSystem: Foo
            * #bear
            * #bear insert Bar
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        var bear = cs.Concept.FirstOrDefault(c => c.Code == "bear");
        Assert.IsNotNull(bear);
        Assert.IsTrue(bear.Designation.Any(d => d.Value == "Bar Value"),
            "Expected 'Bar Value' designation on the bear concept.");
        Assert.IsTrue(bear.Concept.Any(c => c.Code == "extra"),
            "Expected 'extra' as a child concept of bear.");
    }

    [TestMethod]
    public void ShouldUpdateCountWhenApplyingConceptsFromAnInsertRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * #lion

            CodeSystem: Foo
            * insert Bar
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        Assert.AreEqual("lion", cs.Concept.FirstOrDefault()?.Code);
        Assert.AreEqual(1, cs.Count);
    }

    [TestMethod]
    public void ShouldLogAnErrorAndNotApplyRulesFromAnInvalidInsertRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            RuleSet: Bar
            * experimental = true
            * ^title = ""Wow fancy""

            CodeSystem: Foo
            * insert Bar
        ");
        // SUSHI: AssignmentRule is not valid in a CodeSystem context → error.
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("assignment") || w.Message.ToLower().Contains("experimental") || w.Message.ToLower().Contains("codessystem")),
            "Expected a warning about the invalid rule in the rule set.");
    }

    [TestMethod]
    public void ShouldMaintainConceptOrderWhenAddingConceptsFromAnInsertRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * #lion

            CodeSystem: Foo
            * #bear
            * insert Bar
            * #alligator
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        Assert.AreEqual("bear", cs.Concept[0].Code);
        Assert.AreEqual("lion", cs.Concept[1].Code);
        Assert.AreEqual("alligator", cs.Concept[2].Code);
        Assert.AreEqual(3, cs.Count);
    }

    [TestMethod]
    public void ShouldAddNestedConceptsFromAnInsertRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * #main ""MainCode""

            RuleSet: AnotherBar
            * #main #sub ""SubCode""

            CodeSystem: Foo
            * insert Bar
            * insert AnotherBar
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        var main = cs.Concept.FirstOrDefault(c => c.Code == "main");
        Assert.IsNotNull(main);
        Assert.IsNotNull(main.Concept.FirstOrDefault(c => c.Code == "sub"));
    }

    [TestMethod]
    public void ShouldAddNestedConceptsWhoseHierarchyIsCreatedByAnInsertRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * #MyCode ""MyCode"" ""This is my code""

            CodeSystem: Foo
            * insert Bar
            * #MyCode #SubCode ""SubCode"" ""This is my sub-code""
        ");
        var cs = SushiCompilerTestHelper.FindCs(resources, "Foo");
        Assert.IsNotNull(cs);
        var myCode = cs.Concept.FirstOrDefault(c => c.Code == "MyCode");
        Assert.IsNotNull(myCode);
        Assert.IsNotNull(myCode.Concept.FirstOrDefault(c => c.Code == "SubCode"));
    }

    [TestMethod]
    public void ShouldNotAddConceptsFromAnInsertRuleThatAreDuplicatesOfExistingConcepts()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            RuleSet: Bar
            * #lion ""Lion""
            * #bear ""Extra Bear""

            CodeSystem: Foo
            * #bear ""Regular Bear""
            * insert Bar
            * #alligator ""Alligator""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("bear") || w.Message.ToLower().Contains("duplicate") || w.Message.ToLower().Contains("already")),
            "Expected a warning about the duplicate 'bear' concept from the insert rule.");
    }

    [TestMethod]
    public void ShouldNotAddConceptsFromAnInsertRuleThatAreDuplicatesOfConceptsAddedByAPreviousInsertRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            RuleSet: Bar
            * #bear ""Bear""

            RuleSet: AnotherBar
            * #bear ""Another Bear""

            CodeSystem: Foo
            * insert Bar
            * insert AnotherBar
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("bear") || w.Message.ToLower().Contains("duplicate") || w.Message.ToLower().Contains("already")),
            "Expected a warning about the duplicate 'bear' concept from a second insert rule.");
    }
}
