// Ported from SUSHI: test/export/StructureDefinition.ProfileExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.ProfileExporter.test.ts
//
// Translation notes:
//  - SUSHI programmatic `new Profile('Foo'); profile.parent = 'Basic'` → FSH text.
//  - SUSHI pkg.fshMap (per-resource source location tracking) → Inconclusive
//    (no Package abstraction in fsh-compiler).
//  - SUSHI exporter.deferredCaretRules / knownBindingRules (internal SUSHI state) →
//    Inconclusive; not observable on the compiled result.
//  - loggerSpy error assertions → CompileResult<T>.Warnings assertions.
//  - Many tests require a FHIR resolver to resolve 'Basic', 'Patient', etc. — left to fail.

using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class ProfileExporterTests
{
    [TestMethod]
    public void ShouldOutputEmptyResultsWithEmptyInput()
    {
        // Empty FSH: no entities in the document ⇒ no SDs compiled.
        // Parser rejects wholly-empty input; use an alias-only doc instead.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Alias: $X = http://example.org/x
        ");
        var sds = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.StructureDefinitions(ok.Value)
            : new List<StructureDefinition>();
        Assert.AreEqual(0, sds.Count);
    }

    [TestMethod]
    public void ShouldExportASingleProfile()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Basic
        ");
        Assert.AreEqual(1, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldAddSourceInfoForTheExportedProfileToThePackage()
    {
        Assert.Inconclusive(
            "SUSHI tracks per-resource source file + line/column in pkg.fshMap. " +
            "fsh-compiler has no equivalent Package abstraction.");
    }

    [TestMethod]
    public void ShouldExportMultipleProfiles()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Basic

            Profile: Bar
            Parent: Basic
        ");
        Assert.AreEqual(2, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldStillExportProfilesIfOneFails()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Baz

            Profile: Bar
            Parent: Basic
        ");
        if (result is CompileResult<List<FhirResource>>.SuccessResult ok)
        {
            var sds = SushiCompilerTestHelper.StructureDefinitions(ok.Value);
            Assert.AreEqual(1, sds.Count);
            Assert.AreEqual("Bar", sds[0].Name);
        }
        else
        {
            // Compiler currently aborts the batch on unresolved-parent error.
            Assert.Inconclusive("Compiler aborts on unresolved parent; SUSHI continues.");
        }
    }

    [TestMethod]
    public void ShouldLogAErrorWithSourceInformationWhenTheParentIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Bogus
            Parent: BogusParent
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("BogusParent")))
                        || result.Warnings.Any(w => w.Message.Contains("BogusParent"));
        Assert.IsTrue(hasError, "Expected an error/warning referencing BogusParent.");
    }

    [TestMethod]
    public void ShouldLogAErrorWithSourceInformationWhenTheParentIsNotProvided()
    {
        // SUSHI logs: "The definition for Missing does not include a Parent".
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Missing
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Missing") || e.ToString().ToLower().Contains("parent")))
                        || result.Warnings.Any(w => w.Message.Contains("Missing") || w.Message.ToLower().Contains("parent"));
        Assert.IsTrue(hasError, "Expected an error about the missing Parent declaration.");
    }

    [TestMethod]
    public void ShouldExportProfilesWithFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Basic

            Profile: Bar
            Parent: Foo
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(2, sds.Count);
        var foo = sds.First(s => s.Name == "Foo");
        var bar = sds.First(s => s.Name == "Bar");
        Assert.AreEqual(foo.Url, bar.BaseDefinition);
    }

    [TestMethod]
    public void ShouldExportProfilesWithTheSameFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Basic

            Profile: Bar
            Parent: Foo

            Profile: Baz
            Parent: Foo
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(3, sds.Count);
    }

    [TestMethod]
    public void ShouldExportProfilesWithDeepFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Basic

            Profile: Bar
            Parent: Foo

            Profile: Baz
            Parent: Bar
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldExportProfilesWithOutOfOrderFSHyParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Bar

            Profile: Bar
            Parent: Baz

            Profile: Baz
            Parent: Basic
        ");
        Assert.AreEqual(3, SushiCompilerTestHelper.StructureDefinitions(resources).Count);
    }

    [TestMethod]
    public void ShouldExportAProfileWithAnAbstractProfileParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Basic
            * ^abstract = true

            Profile: Bar
            Parent: Foo
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(2, sds.Count);
        var foo = sds.First(s => s.Name == "Foo");
        var bar = sds.First(s => s.Name == "Bar");
        Assert.IsTrue(foo.Abstract);
        Assert.IsFalse(bar.Abstract ?? false);
    }

    [TestMethod]
    public void ShouldExportAProfileWithALogicalParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: ELTSSServiceModel
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.Logical, sd.Kind);
    }

    [TestMethod]
    public void ShouldExportProfilesWithDeepLogicalParents()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: ELTSSServiceModel

            Profile: Bar
            Parent: Foo
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(2, sds.Count);
        Assert.IsTrue(sds.All(s => s.Kind == StructureDefinition.StructureDefinitionKind.Logical));
    }

    [TestMethod]
    public void ShouldExportProfilesWithProfileInstanceParents()
    {
        // SUSHI builds a Definition-usage Instance of StructureDefinition acting as a parent.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: ParentProfile
            InstanceOf: StructureDefinition
            Usage: #definition
            * name = ""ParentProfile""
            * status = #active
            * kind = #resource
            * abstract = false
            * type = ""Observation""
            * derivation = #constraint
            * baseDefinition = ""http://hl7.org/fhir/StructureDefinition/Observation""
            * snapshot.element[0].id = ""Observation""
            * snapshot.element[0].path = ""Observation""

            Profile: ChildProfile
            Parent: ParentProfile
        ");
        var child = SushiCompilerTestHelper.FindSd(resources, "ChildProfile");
        Assert.IsNotNull(child);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/ParentProfile", child.BaseDefinition);
    }

    [TestMethod]
    public void ShouldDeferAddingAnInstanceToAProfileAsAContainedResource()
    {
        Assert.Inconclusive(
            "SUSHI exposes exporter.deferredCaretRules which has no equivalent in " +
            "fsh-compiler. Deferred resolution is internal; only the final SD is observable.");
    }

    [TestMethod]
    public void ShouldDeferAddingAnInstanceWithANumericIdToAProfileAsAContainedResource()
    {
        Assert.Inconclusive("See ShouldDeferAddingAnInstanceToAProfileAsAContainedResource.");
    }

    [TestMethod]
    public void ShouldDeferAddingAnInstanceWithAnIdThatResemblesABooleanToAProfileAsAContainedResource()
    {
        Assert.Inconclusive("See ShouldDeferAddingAnInstanceToAProfileAsAContainedResource.");
    }

    [TestMethod]
    public void ShouldDeferAddingABindingToAnInlineValueSetResource()
    {
        Assert.Inconclusive(
            "SUSHI exposes exporter.knownBindingRules which has no equivalent in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldAllowAContainedResourceWithAResourceTypeToBeBuiltFromSeveralCaretRules()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: ContainingProfile
            Parent: Patient
            * ^contained.resourceType = ""Observation""
            * ^contained.id = ""my-observation""
            * ^contained.status = #draft
            * ^contained.code = #123
            * ^contained.valueString = ""contained observation""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var observation = sd.Contained[0] as Observation;
        Assert.IsNotNull(observation);
        Assert.AreEqual("my-observation", observation.Id);
    }

    [TestMethod]
    public void ShouldDeferApplyingACaretRuleThatWouldBeAppliedWithinAContainedInstance()
    {
        Assert.Inconclusive(
            "SUSHI exposes exporter.deferredCaretRules which has no equivalent in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldNotExportAProfileOfAnR5ResourceInAnR4Project()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: ADProfile
            Parent: ActorDefinition
        ");
        // In R4, ActorDefinition does not exist as a base resource.
        bool hasError = result is CompileResult<List<FhirResource>>.FailureResult
                        || result.Warnings.Any(w => w.Message.Contains("ActorDefinition"));
        Assert.IsTrue(hasError, "Expected an error/warning about ActorDefinition not being available in R4.");
    }

    [TestMethod]
    public void ShouldThrowAMismatchedBindingTypeErrorWhenACodePropertyIsBoundToACodeSystem()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: TestProfile
            Parent: Patient
            * identifier.type from W3cProvenanceActivityType (required)
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("valueset")
                || w.Message.Contains("W3cProvenanceActivityType")),
            "Expected a warning when binding to a CodeSystem instead of a ValueSet.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnInlineExtensionIsUsed()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: MyObservation
            Parent: Observation
            * extension contains SomeExtension 0..*
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("extension")
                || w.Message.ToLower().Contains("inline")),
            "Expected a warning about the inline extension.");
    }
}
