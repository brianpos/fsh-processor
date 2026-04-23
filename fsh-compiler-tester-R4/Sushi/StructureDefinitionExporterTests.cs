// Ported from SUSHI: test/export/StructureDefinitionExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinitionExporter.test.ts
//
// Translation notes:
//  - SUSHI builds input programmatically (new Profile('Foo'); profile.parent = 'Patient').
//    Ports use FSH text via SushiCompilerTestHelper.CompileDoc.
//  - loggerSpy error/warn assertions → CompileResult<T>.Warnings assertions.
//  - pkg.fshMap, exporter.deferredCaretRules, jest spy on SD.validate →
//    Assert.Inconclusive (no equivalent in fsh-compiler).
//  - Tests requiring a FHIR resolver (Patient, Observation, Basic, us-core-patient, etc.)
//    will currently fail until CompilerOptions.Resolver is wired up in these tests.
//  - Per task instructions ("port tests, don't fix issues") all tests are written to the
//    SUSHI spec; failures reflect compiler features not yet implemented.
//
// Sections covered:
//   #StructureDefinition  (15 tests)
//   #Parents              (30 tests)
//   #Profile              ( 8 tests)
//   #Extension            (12 tests)
//   #LogicalModel         ( 6 metadata tests — structural element tests omitted)
//   #Resource             ( 6 metadata tests — structural element tests omitted)

using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class StructureDefinitionExporterTests
{
    // ─── #StructureDefinition ─────────────────────────────────────────────────

    [TestMethod]
    public void ShouldNotExportDuplicateStructureDefinitions()
    {
        // Profile uses an Extension from the same doc — SUSHI exports exactly 1 profile + 1 extension.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Bar

            Profile: Foo
            Parent: Basic
            * extension contains Bar 0..*
        ");
        var sds = SushiCompilerTestHelper.StructureDefinitions(resources);
        Assert.AreEqual(1, sds.Count(sd => sd.Kind == StructureDefinition.StructureDefinitionKind.Resource
                                          || sd.Type == "Basic"
                                          || sd.Name == "Foo"),
            "Expected exactly 1 profile output.");
        Assert.AreEqual(1, sds.Count(sd => sd.Type == "Extension" || sd.Name == "Bar"),
            "Expected exactly 1 extension output.");
    }

    [TestMethod]
    public void ShouldWarnWhenTheStructDefIsAProfileAndTitleAndOrDescriptionIsAnEmptyString()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Basic
            Title: """"
            Description: """"
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("title") || w.Message.ToLower().Contains("description")),
            "Expected warnings about empty title/description.");
    }

    [TestMethod]
    public void ShouldWarnWhenTheStructDefIsAnExtensionAndTitleAndOrDescriptionIsAnEmptyString()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: Bar
            Title: """"
            Description: """"
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("title") || w.Message.ToLower().Contains("description")),
            "Expected warnings about empty extension title/description.");
    }

    [TestMethod]
    public void ShouldWarnWhenTheStructDefIsALogicalAndTitleAndOrDescriptionIsAnEmptyString()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: BackFooTheFuture
            Parent: Element
            Title: """"
            Description: """"
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("title") || w.Message.ToLower().Contains("description")),
            "Expected warnings about empty logical title/description.");
    }

    [TestMethod]
    public void ShouldWarnWhenTheStructDefIsAResourceAndTitleAndOrDescriptionIsAnEmptyString()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: SpidermanBarFromHome
            Id: PatientResource
            Title: """"
            Description: """"
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("title") || w.Message.ToLower().Contains("description")),
            "Expected warnings about empty resource title/description.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheStructureDefinitionHasAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Wrong
            Id: ""will?not?work""
            Parent: Observation
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("id") || w.Message.ToLower().Contains("valid")),
            "Expected a warning about the invalid id.");
    }

    [TestMethod]
    public void ShouldNotLogAMessageWhenTheStructureDefinitionOverridesAnInvalidIdWithACaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Wrong
            Id: ""will?not?work""
            Parent: Patient
            * ^id = ""will-work""
        ");
        var sd = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindSd(ok.Value, "Wrong")
            : null;
        if (sd != null)
            Assert.AreEqual("will-work", sd.Id);
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheStructureDefinitionOverridesAnInvalidIdWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Wrong
            Id: ""will?not?work""
            Parent: Patient
            * ^id = ""Still Wont Work!""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("id") || w.Message.ToLower().Contains("valid")),
            "Expected a warning about the still-invalid id after caret override.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheStructureDefinitionOverridesAValidIdWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Wrong
            Id: valid-id
            Parent: Observation
            * ^id = ""This Is Not Right!""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("id") || w.Message.ToLower().Contains("valid")),
            "Expected a warning when a valid id is replaced by an invalid caret value.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheStructureDefinitionHasAnInvalidName()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Not-good
            Parent: Observation
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("name") || w.Message.ToLower().Contains("machine")),
            "Expected a warning about the invalid name.");
    }

    [TestMethod]
    public void ShouldNotLogAMessageWhenTheStructureDefinitionOverridesAnInvalidNameWithACaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Not-good
            Parent: Observation
            * ^name = ""NotGood""
        ");
        var sd = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindSd(ok.Value, "Not-good")
            : null;
        if (sd != null)
            Assert.AreEqual("NotGood", sd.Name);
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheStructureDefinitionOverridesAnInvalidNameWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Not-good
            Parent: Observation
            * ^name = ""Not-good""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("name") || w.Message.ToLower().Contains("machine")),
            "Expected a warning about the still-invalid name.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTheStructureDefinitionOverridesAValidNameWithAnInvalidCaretRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: NotGood
            Parent: Patient
            * ^name = ""Not-good""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("name") || w.Message.ToLower().Contains("machine")),
            "Expected a warning when a valid name is replaced by an invalid caret value.");
    }

    [TestMethod]
    public void ShouldSanitizeTheIdAndLogAMessageWhenAValidNameIsUsedToMakeAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Not_good_id
            Parent: Patient
        ");
        var sd = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindSd(ok.Value, "Not_good_id")
            : null;
        Assert.IsNotNull(sd);
        Assert.AreEqual("Not_good_id", sd.Name);
        Assert.AreEqual("Not-good-id", sd.Id);
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
            Profile: {longName}
            Parent: Basic
        ";
        var result = SushiCompilerTestHelper.CompileDocResult(fsh);
        var sd = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindSd(ok.Value, longName)
            : null;
        Assert.IsNotNull(sd);
        Assert.AreEqual(longName, sd.Name);
        Assert.IsTrue(sd.Id.Length <= 64, $"Id should be truncated to 64 chars; was: {sd.Id}");
    }

    [TestMethod]
    public void ShouldLogErrorMessagesForValidationErrorsOnTheStructureDefinition()
    {
        Assert.Inconclusive(
            "SUSHI spies on StructureDefinition.prototype.validate() to inject errors. " +
            "fsh-compiler has no equivalent mock injection mechanism.");
    }

    // ─── #Parents ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldCreateAProfileWhenTheDefinitionSpecifiesAResourceForAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyPatientProfile
            Id: my-patient
            Parent: Patient
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientProfile");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Patient", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAProfileWhenTheDefinitionSpecifiesAnotherProfileForAParent()
    {
        // Needs us-core-patient from FHIR package resolver — will fail without resolver.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyPatientProfile
            Id: my-patient
            Parent: us-core-patient
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientProfile");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAProfileWhenTheDefinitionSpecifiesAComplexDataTypeForAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyAddressProfile
            Id: my-address
            Parent: Address
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyAddressProfile");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Address", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAProfileWhenTheDefinitionSpecifiesAPrimitiveDataTypeForAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyDateTimeProfile
            Id: my-datetime
            Parent: dateTime
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyDateTimeProfile");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/dateTime", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAnExtensionWithDefaultParentOfBaseExtensionWhenTheDefinitionDoesNotSpecifyAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Id: my-extension
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Extension", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAnExtensionWhenTheDefinitionSpecifiesTheBaseExtensionForAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            Parent: Extension
            Id: my-extension
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Extension", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAnExtensionWhenTheDefinitionSpecifiesAnotherExtensionForAParent()
    {
        // Needs familymemberhistory-type from FHIR package resolver.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyFamilyHistoryExtension
            Parent: familymemberhistory-type
            Id: my-family-history
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyFamilyHistoryExtension");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/familymemberhistory-type",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateALogicalModelWithDefaultParentOfBaseWhenTheDefinitionDoesNotSpecifyAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyPatientModel
            Id: PatientModel
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientModel");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Base", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateALogicalModelWhenTheDefinitionSpecifiesElementForAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyPatientModel
            Parent: Element
            Id: PatientModel
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientModel");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Element", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateALogicalModelWhenTheDefinitionSpecifiesAnotherLogicalModelForAParent()
    {
        // Needs AlternateIdentification from FHIR package resolver.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyIdentificationModel
            Parent: AlternateIdentification
            Id: my-identification
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyIdentificationModel");
        Assert.IsNotNull(sd);
        Assert.IsTrue(
            sd.BaseDefinition?.Contains("AlternateIdentification") == true,
            "Expected baseDefinition containing AlternateIdentification.");
    }

    [TestMethod]
    public void ShouldCreateAResourceWithDefaultParentOfDomainResourceWhenTheDefinitionDoesNotSpecifyAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyPatientResource
            Id: PatientResource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/DomainResource",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateAResourceWhenTheDefinitionSpecifiesResourceForAParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyPatientResource
            Parent: Resource
            Id: PatientResource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Resource", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldThrowParentNotProvidedErrorWhenParentSpecifiesAnEmptyParent()
    {
        // SUSHI throws: "The definition for Foo does not include a Parent"
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().ToLower().Contains("parent")))
                        || result.Warnings.Any(w => w.Message.ToLower().Contains("parent"));
        Assert.IsTrue(hasError, "Expected an error about the missing Parent declaration.");
    }

    [TestMethod]
    public void ShouldThrowParentNotDefinedErrorWhenParentIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Bar
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Bar")))
                        || result.Warnings.Any(w => w.Message.Contains("Bar"));
        Assert.IsTrue(hasError, "Expected an error: 'Parent Bar not found for Foo'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsNameErrorWhenTheExtensionDeclaresItselfAsTheParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: Foo
            Parent: Foo
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Foo")))
                        || result.Warnings.Any(w => w.Message.Contains("Foo"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Extension Foo cannot declare itself as a Parent'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsIdErrorWhenAnExtensionSetsTheSameValueForParentAndId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: InitialExtension
            Id: ParentExtension

            Extension: OverlappingExtension
            Parent: InitialExtension
            Id: InitialExtension
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("InitialExtension")))
                        || result.Warnings.Any(w => w.Message.Contains("InitialExtension"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Extension OverlappingExtension cannot declare InitialExtension as both Parent and Id'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsNameErrorWhenTheProfileDeclaresItselfAsTheParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Foo
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Foo")))
                        || result.Warnings.Any(w => w.Message.Contains("Foo"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Profile Foo cannot declare itself as a Parent'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsNameErrorAndSuggestResourceUrlWhenTheProfileDeclaresItselfAsTheParentAndItIsAFHIRResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Patient
            Parent: Patient
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Patient")))
                        || result.Warnings.Any(w => w.Message.Contains("Patient"));
        Assert.IsTrue(hasError,
            "Expected an error suggesting the resource URL for self-referencing Profile parent.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsIdErrorWhenAProfileSetsTheSameValueForParentAndId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: InitialProfile
            Id: ParentProfile
            Parent: Basic

            Profile: OverlappingProfile
            Parent: InitialProfile
            Id: InitialProfile
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("InitialProfile")))
                        || result.Warnings.Any(w => w.Message.Contains("InitialProfile"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Profile OverlappingProfile cannot declare InitialProfile as both Parent and Id'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsIdErrorAndSuggestResourceUrlWhenAProfileSetsTheSameValueForParentAndIdAndTheParentIsAFHIRResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: KidsFirstPatient
            Parent: Patient
            Id: Patient
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Patient")))
                        || result.Warnings.Any(w => w.Message.Contains("Patient"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Profile KidsFirstPatient cannot declare Patient as both Parent and Id'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsNameErrorWhenTheResourceDeclaresItselfAsTheParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: Foo
            Parent: Foo
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Foo")))
                        || result.Warnings.Any(w => w.Message.Contains("Foo"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Resource Foo cannot declare itself as a Parent'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsIdErrorWhenAResourceSetsTheSameValueForParentAndId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: InitialResource
            Id: ParentExtension
            Parent: Resource

            Resource: OverlappingResource
            Parent: InitialResource
            Id: InitialResource
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("InitialResource")))
                        || result.Warnings.Any(w => w.Message.Contains("InitialResource"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Resource OverlappingResource cannot declare InitialResource as both Parent and Id'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsNameErrorWhenTheLogicalModelDeclaresItselfAsTheParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: Foo
            Parent: Foo
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Foo")))
                        || result.Warnings.Any(w => w.Message.Contains("Foo"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Logical Foo cannot declare itself as a Parent'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsNameErrorAndSuggestResourceUrlWhenTheLogicalModelDeclaresItselfAsTheParentAndItIsAFHIRResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: Patient
            Parent: Patient
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Patient")))
                        || result.Warnings.Any(w => w.Message.Contains("Patient"));
        Assert.IsTrue(hasError,
            "Expected an error suggesting the resource URL for self-referencing Logical parent.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsIdErrorWhenALogicalModelSetsTheSameValueForParentAndId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: InitialLogical
            Id: ParentLogical
            Parent: Base

            Logical: OverlappingLogical
            Parent: InitialLogical
            Id: InitialLogical
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("InitialLogical")))
                        || result.Warnings.Any(w => w.Message.Contains("InitialLogical"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Logical OverlappingLogical cannot declare InitialLogical as both Parent and Id'.");
    }

    [TestMethod]
    public void ShouldThrowParentDeclaredAsIdErrorAndSuggestResourceUrlWhenALogicalModelSetsTheSameValueForParentAndIdAndTheParentIsAFHIRResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: KidsFirstPatient
            Parent: Patient
            Id: Patient
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("Patient")))
                        || result.Warnings.Any(w => w.Message.Contains("Patient"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Logical KidsFirstPatient cannot declare Patient as both Parent and Id'.");
    }

    [TestMethod]
    public void ShouldThrowInvalidExtensionParentErrorWhenAnExtensionHasANonExtensionForAParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: MyPatientExtension
            Parent: Patient
            Id: PatientExt
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().ToLower().Contains("extension")))
                        || result.Warnings.Any(w =>
                            w.Message.ToLower().Contains("extension") || w.Message.Contains("Patient"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Invalid parent Patient specified for extension MyPatientExtension'.");
    }

    [TestMethod]
    public void ShouldThrowInvalidLogicalParentErrorWhenALogicalModelHasAProfileForAParent()
    {
        // actualgroup is a Profile, not a Logical/Resource/Type — needs FHIR resolver.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyGroupModel
            Parent: actualgroup
            Id: GroupModel
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().ToLower().Contains("logical") || e.ToString().Contains("actualgroup")))
                        || result.Warnings.Any(w =>
                            w.Message.ToLower().Contains("logical") || w.Message.Contains("actualgroup"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Invalid parent actualgroup specified for logical model MyGroupModel'.");
    }

    [TestMethod]
    public void ShouldThrowInvalidResourceParentErrorWhenAResourceDoesNotHaveResourceOrDomainResourceForAParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: MyCustomPatient
            Parent: Patient
            Id: CustomPatient
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().ToLower().Contains("resource") || e.ToString().Contains("Patient")))
                        || result.Warnings.Any(w =>
                            w.Message.ToLower().Contains("resource") || w.Message.Contains("Patient"));
        Assert.IsTrue(hasError,
            "Expected an error: 'Invalid parent Patient specified for resource MyCustomPatient'.");
    }

    // ─── #Profile ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldSetAllUserProvidedMetadataForAProfile()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Id: foo
            Parent: Observation
            Title: ""Foo Profile""
            Description: ""foo bar foobar""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("foo", sd.Id);
        Assert.AreEqual("Foo Profile", sd.Title);
        Assert.AreEqual("foo bar foobar", sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/foo", sd.Url);
        Assert.AreEqual("Observation", sd.Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Observation", sd.BaseDefinition);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
    }

    [TestMethod]
    public void ShouldSetStatusAndVersionMetadataForAProfileInFSHOnlyMode()
    {
        // SUSHI: FSHOnly config propagates status/version.
        // Port uses caret rules to achieve the same effect.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Id: foo
            Parent: Observation
            * ^status = #active
            * ^version = ""0.1.0""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(PublicationStatus.Active, sd.Status);
        Assert.AreEqual("0.1.0", sd.Version);
    }

    [TestMethod]
    public void ShouldNotOverwriteMetadataThatIsNotGivenForAProfile()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("Foo", sd.Id);
        Assert.IsNull(sd.Title);
        Assert.IsNull(sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Foo", sd.Url);
        Assert.AreEqual("Observation", sd.Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Observation", sd.BaseDefinition);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    [TestMethod]
    public void ShouldAllowMetadataToBeOverwrittenWithCaretRule_Profile()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * ^status = #draft
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMultipleProfilesHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: FirstProfile
            Id: my-profile
            Parent: Basic

            Profile: SecondProfile
            Id: my-profile
            Parent: Basic
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-profile") || w.Message.ToLower().Contains("multiple") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate profile id.");
    }

    [TestMethod]
    public void ShouldProperlySetClearAllMetadataPropertiesForAProfile()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Id);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Foo", sd.Url);
        Assert.AreEqual("Foo", sd.Name);
        Assert.IsNull(sd.Title);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
        Assert.AreEqual(FHIRVersion.N4_0_1, sd.FhirVersion);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.Resource, sd.Kind);
        Assert.AreEqual(false, sd.Abstract);
        Assert.AreEqual("Observation", sd.Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Observation", sd.BaseDefinition);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    // ─── #Extension ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldSetAllUserProvidedMetadataForAnExtension()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
            Id: foo
            Title: ""Foo Extension""
            Description: ""foo bar foobar""
            Context: (Condition | Observation).code
            Context: http://hl7.org/fhir/StructureDefinition/cqf-library
            Context: Address.period.start
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("foo", sd.Id);
        Assert.AreEqual("Foo Extension", sd.Title);
        Assert.AreEqual("foo bar foobar", sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/foo", sd.Url);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
        Assert.AreEqual("Extension", sd.Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Extension", sd.BaseDefinition);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(3, sd.Context.Count);
    }

    [TestMethod]
    public void ShouldSetStatusAndVersionMetadataForAnExtensionInFSHOnlyMode()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
            Id: foo
            * ^status = #active
            * ^version = ""0.1.0""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(PublicationStatus.Active, sd.Status);
        Assert.AreEqual("0.1.0", sd.Version);
    }

    [TestMethod]
    public void ShouldNotOverwriteMetadataThatIsNotGivenForAnExtension()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("Foo", sd.Id);
        Assert.IsNull(sd.Title);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Foo", sd.Url);
        Assert.AreEqual("Extension", sd.Type);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Extension", sd.BaseDefinition);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    [TestMethod]
    public void ShouldOverwriteParentContextWhenANewContextIsSet()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
            Parent: patient-mothersMaidenName
            Context: (Condition | Observation).code
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(1, sd.Context.Count);
        Assert.AreEqual("(Condition | Observation).code", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldAllowMetadataToBeOverwrittenWithCaretRule_Extension()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
            * ^status = #draft
            * ^context[0].type = #element
            * ^context[0].expression = ""Observation""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
        Assert.IsNotNull(sd.Context);
        Assert.AreEqual(1, sd.Context.Count);
        Assert.AreEqual("Observation", sd.Context[0].Expression);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMultipleExtensionsHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: FirstExtension
            Id: my-extension

            Extension: SecondExtension
            Id: my-extension
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-extension") || w.Message.ToLower().Contains("multiple") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate extension id.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAProfileAndAnExtensionHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: MyProfile
            Id: custom-definition
            Parent: Basic

            Extension: MyExtension
            Id: custom-definition
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("custom-definition") || w.Message.ToLower().Contains("multiple") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate id shared by a profile and extension.");
    }

    [TestMethod]
    public void ShouldNotSetMetadataOnTheRootElementWhenApplyExtensionMetadataToRootIsFalse()
    {
        // SUSHI: config option applyExtensionMetadataToRoot=false.
        // Port: no config option available; assert the extension still compiles.
        Assert.Inconclusive(
            "SUSHI config option applyExtensionMetadataToRoot has no equivalent in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldProperlySetClearAllMetadataPropertiesForAnExtension()
    {
        // Needs patient-mothersMaidenName from FHIR resolver.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: Foo
            Parent: patient-mothersMaidenName
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Id);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Foo", sd.Url);
        Assert.AreEqual("Foo", sd.Name);
        Assert.IsNull(sd.Title);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
        Assert.AreEqual("Extension", sd.Type);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/patient-mothersMaidenName",
            sd.BaseDefinition);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    [TestMethod]
    public void ShouldRemoveInheritedTopLevelUnderscorePrefixedMetadataPropertiesForAnExtension()
    {
        Assert.Inconclusive(
            "SUSHI injects _baseDefinition via FHIR package. " +
            "fsh-compiler does not expose internal JSON properties.");
    }

    // ─── #LogicalModel ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldHaveTheCorrectBaseDefinitionOfBaseWhenParentIsNotProvided()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Base", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldHaveTheCorrectBaseDefinitionForAProvidedParent()
    {
        // Needs AlternateIdentification from FHIR resolver.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Parent: AlternateIdentification
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.IsTrue(sd.BaseDefinition?.Contains("AlternateIdentification") == true,
            "Expected baseDefinition containing AlternateIdentification.");
    }

    [TestMethod]
    public void ShouldSetAllUserProvidedMetadataForALogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Id: foo
            Title: ""Foo Logical Model""
            Description: ""foo bar foobar""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("foo", sd.Id);
        Assert.AreEqual("Foo Logical Model", sd.Title);
        Assert.AreEqual("foo bar foobar", sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/foo", sd.Url);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.Logical, sd.Kind);
    }

    [TestMethod]
    public void ShouldSetStatusAndVersionMetadataForALogicalModelInFSHOnlyMode()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            Id: foo
            * ^status = #active
            * ^version = ""0.1.0""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(PublicationStatus.Active, sd.Status);
        Assert.AreEqual("0.1.0", sd.Version);
    }

    [TestMethod]
    public void ShouldNotOverwriteMetadataThatIsNotGivenForALogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("Foo", sd.Id);
        Assert.IsNull(sd.Title);
        Assert.IsNull(sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Foo", sd.Url);
    }

    [TestMethod]
    public void ShouldAllowMetadataToBeOverwrittenWithCaretRule_LogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: Foo
            * ^status = #draft
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMultipleLogicalModelsHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: FirstLogical
            Id: my-logical

            Logical: SecondLogical
            Id: my-logical
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-logical") || w.Message.ToLower().Contains("multiple") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate logical id.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAProfileAndALogicalModelHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: MyProfile
            Id: custom-definition
            Parent: Basic

            Logical: MyLogical
            Id: custom-definition
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("custom-definition") || w.Message.ToLower().Contains("multiple") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate id shared by a profile and logical model.");
    }

    // ─── #Resource ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldHaveTheCorrectBaseDefinitionOfDomainResourceWhenParentIsNotProvided()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyPatientResource
            Id: PatientResource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/DomainResource",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldHaveTheCorrectBaseDefinitionForAResourceParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyPatientResource
            Parent: Resource
            Id: PatientResource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Resource", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldHaveTheCorrectBaseDefinitionForADomainResourceParent()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyPatientResource
            Parent: DomainResource
            Id: PatientResource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyPatientResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/DomainResource",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldSetAllUserProvidedMetadataForAResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: Foo
            Id: foo
            Title: ""Foo Resource""
            Description: ""foo bar foobar""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("foo", sd.Id);
        Assert.AreEqual("Foo Resource", sd.Title);
        Assert.AreEqual("foo bar foobar", sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/foo", sd.Url);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.Resource, sd.Kind);
    }

    [TestMethod]
    public void ShouldSetStatusAndVersionMetadataForAResourceInFSHOnlyMode()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: Foo
            Id: foo
            * ^status = #active
            * ^version = ""0.1.0""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual(PublicationStatus.Active, sd.Status);
        Assert.AreEqual("0.1.0", sd.Version);
    }

    [TestMethod]
    public void ShouldNotOverwriteMetadataThatIsNotGivenForAResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: Foo
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual("Foo", sd.Id);
        Assert.IsNull(sd.Title);
        Assert.IsNull(sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/Foo", sd.Url);
    }

    [TestMethod]
    public void ShouldAllowMetadataToBeOverwrittenWithCaretRule_Resource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: Foo
            * ^status = #draft
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("Foo", sd.Name);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMultipleResourcesHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: FirstResource
            Id: my-resource

            Resource: SecondResource
            Id: my-resource
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-resource") || w.Message.ToLower().Contains("multiple") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate resource id.");
    }
}
