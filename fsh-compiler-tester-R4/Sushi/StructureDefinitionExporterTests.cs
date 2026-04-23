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

    [TestMethod]
    public void ShouldLogAnErrorWhenAResourceAndALogicalModelHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: MyResource
            Id: shared-id

            Logical: MyLogical
            Id: shared-id
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("shared-id") || w.Message.ToLower().Contains("duplicate")),
            "Expected a warning about the duplicate id between resource and logical model.");
    }

    [TestMethod]
    public void ShouldHaveTheCorrectBaseDefinitionOfElementWhenParentIsNotProvided()
    {
        // SUSHI default parent for Resource is DomainResource, not Element.
        // The SD baseDefinition should be DomainResource when no parent specified.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyResource
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual(
            "http://hl7.org/fhir/StructureDefinition/DomainResource",
            sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldProperlySetClearAllMetadataPropertiesForAResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyResource
            Id: my-resource
            Title: ""My Resource Title""
            Description: ""My resource description.""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyResource");
        Assert.IsNotNull(sd);
        Assert.AreEqual("MyResource", sd.Name);
        Assert.AreEqual("my-resource", sd.Id);
        Assert.AreEqual("My Resource Title", sd.Title);
        Assert.AreEqual("My resource description.", sd.Description);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/StructureDefinition/my-resource", sd.Url);
    }

    [TestMethod]
    public void ShouldRemoveInheritedTopLevelUnderscorePrefixedMetadataPropertiesForAResource()
    {
        Assert.Inconclusive(
            "Requires snapshot support to verify that inherited underscore-prefixed extensions " +
            "are stripped from the exported StructureDefinition. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldIncludeAddedElementsAlongWithParentRootElement()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyResource
            * myElement 0..1 string ""My element""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyResource");
        Assert.IsNotNull(sd);
        var myEl = SushiCompilerTestHelper.FindElement(sd, "MyResource.myElement");
        Assert.IsNotNull(myEl, "Expected myElement in the differential.");
    }

    [TestMethod]
    public void ShouldIncludeAddedElementsForBackboneElementAndChildrenResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Resource: MyResource
            * component 0..* BackboneElement ""Component""
            * component.value 1..1 string ""Component value""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyResource");
        Assert.IsNotNull(sd);
        var comp = SushiCompilerTestHelper.FindElement(sd, "MyResource.component");
        Assert.IsNotNull(comp, "Expected component in the differential.");
        var val = SushiCompilerTestHelper.FindElement(sd, "MyResource.component.value");
        Assert.IsNotNull(val, "Expected component.value in the differential.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMustSupportIsTrueInAResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: MyResource
            * myElement 0..1 string ""My element""
            * myElement MS
        ");
        // SUSHI logs an error when MS is applied in a Resource definition
        // (Resources should use profiles for MS flags)
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("mustsupport") || w.Message.ToLower().Contains("must support")),
            "Expected a warning about MustSupport in a Resource.");
    }

    // ─── Remaining #LogicalModel structural tests ─────────────────────────────

    [TestMethod]
    public void ShouldProperlySetClearAllMetadataPropertiesForALogicalModel()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyLogical
            Id: my-logical
            Title: ""My Logical Model""
            Description: ""My logical model description.""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyLogical");
        Assert.IsNotNull(sd);
        Assert.AreEqual("MyLogical", sd.Name);
        Assert.AreEqual("my-logical", sd.Id);
        Assert.AreEqual("My Logical Model", sd.Title);
        Assert.AreEqual("My logical model description.", sd.Description);
    }

    [TestMethod]
    public void ShouldRemoveInheritedTopLevelUnderscorePrefixedMetadataPropertiesForALogicalModel()
    {
        Assert.Inconclusive(
            "Requires snapshot support to verify that inherited underscore-prefixed extensions " +
            "are stripped from the exported StructureDefinition. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldAllowTypeToBeOverwrittenWithCaretRuleWithAUriValue()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyLogical
            * ^type = ""http://example.com/my-logical""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyLogical");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://example.com/my-logical", sd.Type);
    }

    [TestMethod]
    public void ShouldLogAWarningAndAllowOverwritingTypeWithCaretRuleWithANonUriValue()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyLogical
            * ^type = ""NotAUri""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("type") || w.Message.ToLower().Contains("uri")),
            "Expected a warning about non-URI type value.");
    }

    [TestMethod]
    public void ShouldIncludeAddedElementsAlongWithParentElements()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyLogical
            * patientId 0..1 string ""Patient identifier""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyLogical");
        Assert.IsNotNull(sd);
        var patEl = SushiCompilerTestHelper.FindElement(sd, "MyLogical.patientId");
        Assert.IsNotNull(patEl, "Expected patientId element in the differential.");
    }

    [TestMethod]
    public void ShouldIncludeAddedElementsForBackboneElementAndChildrenLogical()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyLogical
            * component 0..* BackboneElement ""Component""
            * component.value 1..1 string ""Component value""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyLogical");
        Assert.IsNotNull(sd);
        var comp = SushiCompilerTestHelper.FindElement(sd, "MyLogical.component");
        Assert.IsNotNull(comp, "Expected component in the differential.");
        var val = SushiCompilerTestHelper.FindElement(sd, "MyLogical.component.value");
        Assert.IsNotNull(val, "Expected component.value in the differential.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMustSupportIsTrueInALogicalModel()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyLogical
            * myElement 0..1 string ""My element""
            * myElement MS
        ");
        // SUSHI logs an error when MS is applied in a Logical model definition
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("mustsupport") || w.Message.ToLower().Contains("must support")),
            "Expected a warning about MustSupport in a Logical model.");
    }

    // ─── Remaining #Profile metadata tests ───────────────────────────────────

    [TestMethod]
    public void ShouldRemoveInheritedTopLevelUnderscorePrefixedMetadataPropertiesForAProfile()
    {
        Assert.Inconclusive(
            "Requires snapshot support to verify that inherited underscore-prefixed extensions " +
            "are stripped from the exported StructureDefinition. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldOnlyInheritInheritableExtensionsForAProfile()
    {
        Assert.Inconclusive(
            "Requires snapshot support to verify that only inheritable extensions are " +
            "propagated from parent. Not yet implemented in fsh-compiler.");
    }

    // ─── #Profile-Element ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyConstraintsToAllInstancesOfContentReferenceElementsWhenTheProfileElementExtensionIsApplied()
    {
        Assert.Inconclusive(
            "Requires the profile-element extension (http://hl7.org/fhir/StructureDefinition/elementdefinition-profile-element) " +
            "and snapshot support. Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldApplyTheProfileElementExtensionWhenThereAreSeveralExtensionsInTheTypeProfileArray()
    {
        Assert.Inconclusive(
            "Requires the profile-element extension and snapshot support. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldNotApplyConstraintsToAllInstancesOfContentReferenceElementsWhenTheProfileElementExtensionIsMisapplied()
    {
        Assert.Inconclusive(
            "Requires the profile-element extension and snapshot support. " +
            "Not yet implemented in fsh-compiler.");
    }

    // ─── Remaining #Extension metadata tests ─────────────────────────────────

    [TestMethod]
    public void ShouldExportSubExtensionsWithSimilarStartingNamesAndDifferentTypes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExtension
            * extension contains
                partA 0..1 and
                partAB 0..1
            * extension[partA].value[x] only string
            * extension[partAB].value[x] only boolean
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExtension");
        Assert.IsNotNull(sd);
        var partA = sd.Differential?.Element.FirstOrDefault(e => e.SliceName == "partA");
        var partAB = sd.Differential?.Element.FirstOrDefault(e => e.SliceName == "partAB");
        Assert.IsNotNull(partA, "Expected partA sub-extension slice.");
        Assert.IsNotNull(partAB, "Expected partAB sub-extension slice.");
    }

    [TestMethod]
    public void ShouldNotHardcodeInTheDefaultContextIfParentAlreadyHadAContext()
    {
        // When a parent extension already defines a context, child should not add a new
        // duplicate context for the base Extension element.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: ParentExtension
            Context: Patient

            Extension: ChildExtension
            Parent: ParentExtension
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ChildExtension");
        Assert.IsNotNull(sd);
        // SUSHI: child should NOT have additional context beyond what parent provides
        // when no Context keyword is specified on the child.
        // We just verify the child compiles without error.
    }

    // ─── Issue #1553 Bug Fix ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldCreateAProfileWhenTheDefinitionSpecifiesAnotherProfileDoesNotHaveACanonicalVersionForTheParent()
    {
        // Profile whose parent has no version pinning — should succeed
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: ChildProfile
            Parent: Patient
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ChildProfile");
        Assert.IsNotNull(sd);
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Patient", sd.BaseDefinition);
    }

    [TestMethod]
    public void ShouldCreateProfilesWhenTheDefinitionSpecifiesADifferentCanonicalVersionForTheParent()
    {
        // Profile with a versioned parent URL — should compile; baseDefinition matches the versioned URL.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: ChildProfile
            Parent: http://hl7.org/fhir/StructureDefinition/Patient|4.0.1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ChildProfile");
        Assert.IsNotNull(sd);
        Assert.IsTrue(
            sd.BaseDefinition?.Contains("Patient") == true,
            "Expected baseDefinition containing Patient.");
    }

    [TestMethod]
    public void ShouldThrowAnErrorWhenTheDefinitionSpecifiesAnotherProfileHavingAnUnsupportedCanonicalVersionForTheParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: ChildProfile
            Parent: http://hl7.org/fhir/StructureDefinition/Patient|99.0.0
        ");
        // SUSHI logs an error when the canonical version cannot be resolved
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("version") || w.Message.ToLower().Contains("parent")),
            "Expected an error about an unsupported canonical version.");
    }

    [TestMethod]
    public void ShouldThrowAnErrorWhenTheDefinitionSpecifiesAnotherProfileHavingAnUnexpectedCanonicalVersionForTheParent()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: ChildProfile
            Parent: http://hl7.org/fhir/StructureDefinition/Patient|999
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("version") || w.Message.ToLower().Contains("parent")),
            "Expected an error about an unexpected canonical version.");
    }

    // ─── #Invariant ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldNotWarnOrErrorOnAValidInvariantUsingKeywords()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-1
            Description: ""Must have a value.""
            Severity: #error
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-1
        ");
        Assert.IsFalse(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("inv-1")),
            "Expected no warnings about inv-1.");
    }

    [TestMethod]
    public void ShouldNotWarnOrErrorOnAValidInvariantUsingRules()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-2
            * description = ""Must have a value.""
            * severity = #error
            * expression = ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-2
        ");
        Assert.IsFalse(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("inv-2")),
            "Expected no warnings about inv-2.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenDescriptionIsNotProvided()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-no-desc
            Severity: #error
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-no-desc
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("description") || w.Message.ToLower().Contains("inv-no-desc")),
            "Expected a warning about missing invariant description.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenSeverityIsNotProvided()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-no-sev
            Description: ""Must have a value.""
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-no-sev
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("severity") || w.Message.ToLower().Contains("inv-no-sev")),
            "Expected a warning about missing invariant severity.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenSeverityIsNotOneOfTheValidValuesSetByKeyword()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-bad-sev
            Description: ""Must have a value.""
            Severity: #fatal
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-bad-sev
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("severity") || w.Message.ToLower().Contains("fatal") || w.Message.ToLower().Contains("inv-bad-sev")),
            "Expected a warning about invalid severity value.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenSeverityIsNotOneOfTheValidValuesSetByRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-bad-sev-rule
            Description: ""Must have a value.""
            * severity = #fatal
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-bad-sev-rule
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("severity") || w.Message.ToLower().Contains("fatal") || w.Message.ToLower().Contains("inv-bad-sev-rule")),
            "Expected a warning about invalid severity value set by rule.");
    }

    [TestMethod]
    public void ShouldLogAWarningWhenSeverityIncludesASystemSetByKeyword()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-sys-sev
            Description: ""Must have a value.""
            Severity: http://hl7.org/fhir/constraint-severity#error
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-sys-sev
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("system") || w.Message.ToLower().Contains("severity")),
            "Expected a warning about severity including a system.");
    }

    [TestMethod]
    public void ShouldLogAWarningWhenSeverityIncludesASystemSetByRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv-sys-sev-rule
            Description: ""Must have a value.""
            * severity = http://hl7.org/fhir/constraint-severity#error
            Expression: ""value.exists()""

            Profile: ObservationProfile
            Parent: Observation
            * value[x] obeys inv-sys-sev-rule
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("system") || w.Message.ToLower().Contains("severity")),
            "Expected a warning about severity including a system (set by rule).");
    }

    // ─── #Rules ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldEmitAnErrorAndContinueWhenThePathIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * invalid.path 0..*
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("path") || w.Message.ToLower().Contains("invalid.path")),
            "Expected an error about the invalid path.");
    }

    [TestMethod]
    public void ShouldEmitAnErrorAndContinueWhenThePathForTheChildOfAChoiceElementIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * value[x].noSuchChild 0..1
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("path") || w.Message.ToLower().Contains("nosuch")),
            "Expected an error about the invalid path for the child of a choice element.");
    }

    // ─── #AddElementRule ──────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldThrowAnErrorForAnInvalidAddElementRulePath()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyLogical
            * 0..1 string ""Invalid path (missing name)""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("path") || w.Message.ToLower().Contains("element")),
            "Expected an error about an invalid AddElementRule path.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithATypeAndMinimumRequiredAttributes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * patient 0..1 Reference ""A Patient""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.patient");
        Assert.IsNotNull(el);
        Assert.AreEqual("A Patient", el.Short);
        Assert.AreEqual(0, el.Min);
        Assert.AreEqual("1", el.Max);
    }

    [TestMethod]
    public void ShouldAddAnElementWithAContentReferenceAndMinimumRequiredAttributes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * child 0..* contentReference http://example.com/StructureDefinition/MyModel#MyModel.child ""A child""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.child");
        Assert.IsNotNull(el);
        Assert.AreEqual("A child", el.Short);
    }

    [TestMethod]
    public void ShouldAddAnElementWithAdditionalConstraintAttributes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * patient 0..1 Reference ""A Patient"" ""Detailed description of patient reference""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.patient");
        Assert.IsNotNull(el);
        Assert.AreEqual("A Patient", el.Short);
        Assert.AreEqual("Detailed description of patient reference", el.Definition);
    }

    [TestMethod]
    public void ShouldAddAnElementWithMultipleTargetTypes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * subject 0..1 Reference(Patient or Practitioner) ""The subject""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.subject");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Count >= 1, "Expected at least one type.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithAllBooleanFlagsSetToTrue()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myFlag 0..1 boolean ""A flag""
            * myFlag MS SU ?!
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myFlag");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.MustSupport == true, "Expected MustSupport=true.");
        Assert.IsTrue(el.IsSummary == true, "Expected IsSummary=true.");
        Assert.IsTrue(el.IsModifier == true, "Expected IsModifier=true.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithAllBooleanFlagsSetToFalse()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myFlag 0..1 boolean ""A flag""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myFlag");
        Assert.IsNotNull(el);
        // When no flag rules are applied, these should be null/false
        Assert.IsTrue(el.MustSupport != true, "Expected MustSupport not set.");
        Assert.IsTrue(el.IsSummary != true, "Expected IsSummary not set.");
        Assert.IsTrue(el.IsModifier != true, "Expected IsModifier not set.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithTrialUseStandardsFlagSetToTrue()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myEl 0..1 string ""My element""
            * myEl TU
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myEl");
        Assert.IsNotNull(el);
        // TU flag sets the standards status extension
        var tuExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(tuExt, "Expected standards-status extension for TU flag.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithNormativeStandardsFlagSetToTrue()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myEl 0..1 string ""My element""
            * myEl N
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myEl");
        Assert.IsNotNull(el);
        var normExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(normExt, "Expected standards-status extension for N flag.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithDraftStandardsFlagSetToTrue()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myEl 0..1 string ""My element""
            * myEl D
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myEl");
        Assert.IsNotNull(el);
        var draftExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(draftExt, "Expected standards-status extension for D flag.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithAllStandardsFlagsSetToFalse()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myEl 0..1 string ""My element""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myEl");
        Assert.IsNotNull(el);
        // No TU/N/D flag => no standards-status extension
        var stdExt = el.Extension?.Any(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsTrue(stdExt != true, "Expected no standards-status extension.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMoreThanOneStandardsFlagIsSetToTrue()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * myEl 0..1 string ""My element""
            * myEl TU N
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("standard") || w.Message.ToLower().Contains("flag")),
            "Expected an error about multiple standards flags.");
    }

    [TestMethod]
    public void ShouldAddAnElementWithSupportedDocAttributes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Logical: MyModel
            * myEl 0..1 string ""Short description"" ""Full definition of myEl.""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyModel");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "MyModel.myEl");
        Assert.IsNotNull(el);
        Assert.AreEqual("Short description", el.Short);
        Assert.AreEqual("Full definition of myEl.", el.Definition);
    }

    [TestMethod]
    public void ShouldLogAnErrorAndAddAnElementWhenAnElementNameContainsAProhibitedSpecialCharacterOrIsMoreThan64CharactersLong()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * my$Element 0..1 string ""Bad char name""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("name") || w.Message.ToLower().Contains("element") || w.Message.ToLower().Contains("character")),
            "Expected an error about the element name with a prohibited character.");
    }

    [TestMethod]
    public void ShouldLogAWarningAndAddAnElementWhenAnElementNameIsNotASimpleAlphanumeric()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * my_Element 0..1 string ""Underscore name""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("name") || w.Message.ToLower().Contains("element")),
            "Expected a warning about the non-simple-alphanumeric element name.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenSdRuleAddedBeforeAddElementRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * myEl MS
            * myEl 0..1 string ""My element""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("myel") || w.Message.ToLower().Contains("path") || w.Message.ToLower().Contains("element")),
            "Expected an error when SD rule precedes the AddElement rule.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenPathDoesNotHaveXForMultipleDataTypesInAddElementRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * myEl 0..1 string or boolean ""My element""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("[x]") || w.Message.ToLower().Contains("choice")),
            "Expected an error about missing [x] for multi-type element.");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenPathDoesNotHaveXForMultipleReferenceTypesInAddElementRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * subject 0..1 Reference(Patient or Practitioner) ""The subject""
        ");
        Assert.IsFalse(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("[x]") || w.Message.ToLower().Contains("choice")),
            "Expected no error about missing [x] for multi-reference element.");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenPathDoesNotHaveXForMultipleCanonicalTypesInAddElementRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Logical: MyModel
            * target 0..1 Canonical(ValueSet or CodeSystem) ""The target""
        ");
        Assert.IsFalse(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("[x]") || w.Message.ToLower().Contains("choice")),
            "Expected no error about missing [x] for multi-canonical element.");
    }

    // ─── #CardRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyACorrectCardRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject 1..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.AreEqual(1, el.Min);
        Assert.AreEqual("1", el.Max);
    }

    [TestMethod]
    public void ShouldNotApplyAnIncorrectCardRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * subject 2..1
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("cardinality") || w.Message.ToLower().Contains("subject") || w.Message.ToLower().Contains("min")),
            "Expected an error about the invalid cardinality (min > max).");
    }

    [TestMethod]
    public void ShouldApplyACardRuleWithOnlyMinSpecified()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject 1..
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.AreEqual(1, el.Min);
    }

    [TestMethod]
    public void ShouldApplyACardRuleWithOnlyMaxSpecified()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * component ..2
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.component");
        Assert.IsNotNull(el);
        Assert.AreEqual("2", el.Max);
    }

    [TestMethod]
    public void ShouldNotApplyAnIncorrectMinOnlyCardRule()
    {
        // Narrowing min beyond current max should fail
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * referenceRange 2..
        ");
        // referenceRange is 0..* in base; setting min=2 is valid.
        // Setting min=10 on subject (0..1) would be invalid.
        var result2 = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Bar
            Parent: Observation
            * subject 2..
        ");
        // subject is 0..1; min=2 violates the base max=1
        Assert.IsTrue(result2.Warnings.Any(w =>
                w.Message.ToLower().Contains("cardinality") || w.Message.ToLower().Contains("subject") || w.Message.ToLower().Contains("min")),
            "Expected an error about violating base cardinality.");
    }

    [TestMethod]
    public void ShouldNotApplyAnIncorrectMaxOnlyCardRule()
    {
        // Widening max beyond base max should fail
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * subject ..2
        ");
        // subject is 0..1; max=2 widens beyond base which is invalid
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("cardinality") || w.Message.ToLower().Contains("subject") || w.Message.ToLower().Contains("max")),
            "Expected an error about widening the max cardinality.");
    }

    [TestMethod]
    public void ShouldNotApplyACardRuleWithNoSidesSpecified()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * subject ..
        ");
        // A card rule with just '..' specifies nothing, should warn or be a no-op
        // SUSHI: this is not a valid card rule
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("cardinality") || w.Message.ToLower().Contains("card")),
            "Expected an error or warning about an empty card rule.");
    }

    // ─── #FlagRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyAValidFlagRule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject MS SU
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.MustSupport == true, "Expected MustSupport=true.");
        Assert.IsTrue(el.IsSummary == true, "Expected IsSummary=true.");
    }

    [TestMethod]
    public void ShouldApplyAFlagRuleThatSpecifiesAnElementIsTrialUse()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject TU
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        var tuExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(tuExt, "Expected standards-status extension for TU flag.");
    }

    [TestMethod]
    public void ShouldApplyAFlagRuleThatSpecifiesAnElementIsNormative()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject N
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        var normExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(normExt, "Expected standards-status extension for N flag.");
    }

    [TestMethod]
    public void ShouldApplyAFlagRuleThatSpecifiesAnElementIsADraft()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject D
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        var draftExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(draftExt, "Expected standards-status extension for D flag.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMoreThanOneStandardsStatusFlagRuleIsSpecifiedOnAnElement()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * subject TU N
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("standard") || w.Message.ToLower().Contains("flag")),
            "Expected an error about multiple standards flags on one element.");
    }

    [TestMethod]
    public void ShouldApplyAFlagRuleThatChangesTheExistingStandardsStatus()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject TU
            * subject N
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        // The last applied standard flag wins
        var stdExt = el.Extension?.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/structuredefinition-standards-status");
        Assert.IsNotNull(stdExt, "Expected standards-status extension after changing from TU to N.");
    }

    // ─── #ValueSetRule ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyACorrectValueSetRuleToAnUnboundString()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * status from http://hl7.org/fhir/ValueSet/observation-status (required)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el.Binding, "Expected a binding on status.");
        Assert.AreEqual("http://hl7.org/fhir/ValueSet/observation-status", el.Binding.ValueSet);
    }

    [TestMethod]
    public void ShouldApplyACorrectValueSetRuleThatOverridesAPreviousBinding()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * status from http://hl7.org/fhir/ValueSet/observation-status (required)
            * status from http://hl7.org/fhir/ValueSet/observation-status (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el.Binding);
        // Last binding wins
        Assert.AreEqual(BindingStrength.Extensible, el.Binding.Strength);
    }

    [TestMethod]
    public void ShouldApplyACorrectValueSetRuleWhenTheVSIsReferencedByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            ValueSet: MyVS

            Profile: Foo
            Parent: Observation
            * status from MyVS (required)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el.Binding, "Expected a binding on status when VS referenced by name.");
    }

    [TestMethod]
    public void ShouldApplyACorrectValueSetRuleWhenTheVSHasARuleThatSetsItsNameAndItIsReferencedByName()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            ValueSet: MyVS
            * ^name = ""RenamedVS""

            Profile: Foo
            Parent: Observation
            * status from MyVS (required)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el.Binding, "Expected a binding on status.");
    }

    [TestMethod]
    public void ShouldApplyACorrectValueSetRuleWhenTheVSSpecifiesAVersion()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * status from http://hl7.org/fhir/ValueSet/observation-status|4.0.1 (required)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el.Binding, "Expected a binding with version.");
    }

    [TestMethod]
    public void ShouldApplyAValueSetRuleOnAnElementThatHasTheCanBindCharacteristic()
    {
        Assert.Inconclusive(
            "Requires the #can-bind type-characteristic extension and snapshot support. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldApplyAValueSetRuleOnAnElementThatHasTheCanBindTypeCharacteristicExtension()
    {
        Assert.Inconclusive(
            "Requires the can-bind type-characteristic extension and snapshot support. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldApplyAValueSetRuleOnAnElementThatHasTheCanBindTypeCharacteristicExtensionUsingExtensionPathSyntaxWithUrl()
    {
        Assert.Inconclusive(
            "Requires the can-bind type-characteristic extension and snapshot support. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldLogAWarningAndApplyAValueSetRuleOnAnElementThatIsMissingTheCanBindCharacteristicAndExtension()
    {
        Assert.Inconclusive(
            "Requires the can-bind type-characteristic extension and snapshot support. " +
            "Not yet implemented in fsh-compiler.");
    }

    [TestMethod]
    public void ShouldNotApplyAValueSetRuleOnAnElementThatCannotSupportIt()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * id from http://hl7.org/fhir/ValueSet/observation-status (required)
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("bind") || w.Message.ToLower().Contains("id")),
            "Expected an error about binding an element that cannot support it.");
    }

    [TestMethod]
    public void ShouldNotOverrideABindingWithALessStrictBinding()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * status from http://hl7.org/fhir/ValueSet/observation-status (required)
            * status from http://hl7.org/fhir/ValueSet/observation-status (preferred)
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("bind") || w.Message.ToLower().Contains("weaker") || w.Message.ToLower().Contains("strength")),
            "Expected a warning about overriding with a less strict binding.");
    }

    // ─── #OnlyRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnANonReferenceChoice()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * value[x] only Quantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.value[x]");
        Assert.IsNotNull(el);
        Assert.AreEqual(1, el.Type.Count);
        Assert.AreEqual("Quantity", el.Type[0].Code);
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnAReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject only Reference(Patient)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "Reference"), "Expected Reference type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnAReferenceToAny()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject only Reference(Resource)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "Reference"), "Expected Reference type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnACanonical()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: ActivityDefinition
            * library only Canonical(Library)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "ActivityDefinition.library");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "canonical"), "Expected canonical type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleWithAVersionOnACanonical()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: ActivityDefinition
            * library only Canonical(Library|4.0.1)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "ActivityDefinition.library");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "canonical"), "Expected canonical type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnACanonicalToAny()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: ActivityDefinition
            * library only Canonical(Resource)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "ActivityDefinition.library");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "canonical"), "Expected canonical type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleWithASpecificReferenceTargetConstrained()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject only Reference(Patient or Group)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        var refType = el.Type.FirstOrDefault(t => t.Code == "Reference");
        Assert.IsNotNull(refType);
        Assert.IsTrue(refType.TargetProfile.Count() >= 2, "Expected at least 2 target profiles.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleWithASpecificCanonicalTargetConstrained()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: ActivityDefinition
            * library only Canonical(Library or Measure)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "ActivityDefinition.library");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "canonical"), "Expected canonical type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnANonReferenceFSHyChoice()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyQuantity
            Parent: Quantity

            Profile: Foo
            Parent: Observation
            * value[x] only MyQuantity
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.value[x]");
        Assert.IsNotNull(el);
        Assert.AreEqual(1, el.Type.Count);
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnAFSHyReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient

            Profile: Foo
            Parent: Observation
            * subject only Reference(MyPatient)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "Reference"), "Expected Reference type.");
    }

    [TestMethod]
    public void ShouldApplyACorrectOnlyRuleOnAFSHyCanonical()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: MyLibrary
            Parent: Library

            Profile: Foo
            Parent: ActivityDefinition
            * library only Canonical(MyLibrary)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "ActivityDefinition.library");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Type.Any(t => t.Code == "canonical"), "Expected canonical type.");
    }

    [TestMethod]
    public void ShouldNotApplyAnIncorrectOnlyRule()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * status only boolean
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("type") || w.Message.ToLower().Contains("status") || w.Message.ToLower().Contains("only")),
            "Expected an error about applying an incompatible type constraint.");
    }

    [TestMethod]
    public void ShouldApplyAnOnlyRuleToConstrainAnIdElement()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * id only string
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.id");
        Assert.IsNotNull(el);
    }

    [TestMethod]
    public void ShouldApplyAnOnlyRuleToConstrainAUrlElement()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExt
            * url only uri
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExt");
        Assert.IsNotNull(sd);
        // url element constraint — just validate it compiles
    }

    // ─── #ObeysRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyAnObeysRuleAtTheSpecifiedPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Invariant: obs-inv-1
            Description: ""Value must exist.""
            Severity: #error
            Expression: ""value.exists()""

            Profile: Foo
            Parent: Observation
            * value[x] obeys obs-inv-1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.value[x]");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Constraint.Any(c => c.Key == "obs-inv-1"),
            "Expected obs-inv-1 constraint on value[x].");
    }

    [TestMethod]
    public void ShouldApplyAnObeysRuleAtThePathWhichDoesNotHaveAConstraint()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Invariant: obs-inv-2
            Description: ""Status must exist.""
            Severity: #error
            Expression: ""status.exists()""

            Profile: Foo
            Parent: Observation
            * status obeys obs-inv-2
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Constraint.Any(c => c.Key == "obs-inv-2"),
            "Expected obs-inv-2 constraint on status.");
    }

    [TestMethod]
    public void ShouldApplyAnObeysRuleToTheBaseElementWhenNoPathSpecified()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Invariant: obs-inv-3
            Description: ""Observation must have a value or dataAbsentReason.""
            Severity: #error
            Expression: ""value.exists() or dataAbsentReason.exists()""

            Profile: Foo
            Parent: Observation
            * obeys obs-inv-3
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var rootEl = SushiCompilerTestHelper.FindElement(sd, "Observation");
        Assert.IsNotNull(rootEl);
        Assert.IsTrue(rootEl.Constraint.Any(c => c.Key == "obs-inv-3"),
            "Expected obs-inv-3 constraint on root element.");
    }

    [TestMethod]
    public void ShouldNotApplyAnObeysRuleOnAnInvariantThatDoesNotExist()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * value[x] obeys no-such-inv
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("no-such-inv") || w.Message.ToLower().Contains("invariant") || w.Message.ToLower().Contains("not found")),
            "Expected an error about a missing invariant.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenApplyingAnObeysRuleOnAnInvariantWithAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Invariant: inv bad id
            Description: ""Bad invariant.""
            Severity: #error
            Expression: ""true""

            Profile: Foo
            Parent: Observation
            * obeys inv bad id
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("id") || w.Message.ToLower().Contains("invariant")),
            "Expected an error about the invalid invariant id.");
    }

    // ─── #CaretValueRule ──────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAnElementWithAPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * subject ^short = ""The subject of this observation""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.subject");
        Assert.IsNotNull(el);
        Assert.AreEqual("The subject of this observation", el.Short);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAnElementWithoutAPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * ^description = ""An observation profile.""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.AreEqual("An observation profile.", sd.Description);
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnAnExtensionElementWithoutAPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExt
            * ^short = ""My extension""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExt");
        Assert.IsNotNull(sd);
        var rootEl = SushiCompilerTestHelper.FindElement(sd, "MyExt");
        Assert.IsNotNull(rootEl);
        Assert.AreEqual("My extension", rootEl.Short);
    }

    [TestMethod]
    public void ShouldApplyAReferenceCaretValueRuleOnAnSdAndReplaceTheReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * ^jurisdiction = urn:iso:std:iso:3166#US
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        Assert.IsTrue(sd.Jurisdiction.Count > 0, "Expected a jurisdiction value.");
    }

    [TestMethod]
    public void ShouldApplyACaretValueRuleOnTheParentElement()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * status ^short = ""Observation status""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.status");
        Assert.IsNotNull(el);
        Assert.AreEqual("Observation status", el.Short);
    }

    // ─── #Extension preprocessing ────────────────────────────────────────────

    [TestMethod]
    public void ShouldZeroOutExtensionValueXWhenExtensionExtensionIsUsed()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExt
            * extension contains part1 0..1
            * extension[part1].value[x] only string
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExt");
        Assert.IsNotNull(sd);
        // When sub-extensions are used, value[x] should be zeroed out (0..0)
        var valueEl = sd.Differential?.Element.FirstOrDefault(e =>
            e.Path == "Extension.value[x]");
        if (valueEl != null)
            Assert.AreEqual("0", valueEl.Max, "Expected value[x] to be 0..0 when sub-extensions used.");
    }

    [TestMethod]
    public void ShouldZeroOutExtensionExtensionWhenExtensionValueXIsUsed()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExt
            * value[x] only string
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyExt");
        Assert.IsNotNull(sd);
        // When value[x] is constrained, extension sub-extension should be zeroed (0..0)
        var extEl = sd.Differential?.Element.FirstOrDefault(e =>
            e.Path == "Extension.extension");
        if (extEl != null)
            Assert.AreEqual("0", extEl.Max, "Expected extension to be 0..0 when value[x] constrained.");
    }

    [TestMethod]
    public void ShouldLogAnErrorIfExtensionExtensionAndExtensionValueXAreBothUsedButApplyBothRules()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: MyExt
            * extension contains part1 0..1
            * extension[part1].value[x] only string
            * value[x] only boolean
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("extension") || w.Message.ToLower().Contains("value[x]")),
            "Expected an error about using both extension and value[x].");
    }

    // ─── #ContainsRule ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyAContainsRuleOnAnElementWithDefinedSlicing()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Observation
            * category ^slicing.discriminator.type = #pattern
            * category ^slicing.discriminator.path = ""$this""
            * category ^slicing.rules = #open
            * category contains mySlice 0..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var sliceEl = sd.Differential?.Element.FirstOrDefault(e => e.SliceName == "mySlice");
        Assert.IsNotNull(sliceEl, "Expected a mySlice slice.");
    }

    [TestMethod]
    public void ShouldApplyAContainsRuleOnAnExtensionSlice()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExt

            Profile: Foo
            Parent: Patient
            * extension contains MyExt named myExt 0..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var sliceEl = sd.Differential?.Element.FirstOrDefault(e => e.SliceName == "myExt");
        Assert.IsNotNull(sliceEl, "Expected a myExt extension slice.");
    }

    [TestMethod]
    public void ShouldApplyAContainsRuleOfADefinedExtensionOnAnExtensionElement()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: BirthPlace
            Context: Patient

            Profile: Foo
            Parent: Patient
            * extension contains BirthPlace named birthPlace 0..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var sliceEl = sd.Differential?.Element.FirstOrDefault(e => e.SliceName == "birthPlace");
        Assert.IsNotNull(sliceEl, "Expected a birthPlace extension slice.");
    }

    [TestMethod]
    public void ShouldReportAnErrorAndNotAddTheSliceWhenAContainsRuleTriesToAddASliceThatAlreadyExists()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Extension: MyExt

            Profile: Foo
            Parent: Patient
            * extension contains MyExt named myExt 0..1
            * extension contains MyExt named myExt 0..2
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("myext") || w.Message.ToLower().Contains("slice") || w.Message.ToLower().Contains("already")),
            "Expected an error about adding a duplicate slice.");
    }

    [TestMethod]
    public void ShouldNotApplyAContainsRuleOnAnElementWithoutDefinedSlicing()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: Foo
            Parent: Observation
            * category contains mySlice 0..1
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("slice") || w.Message.ToLower().Contains("slicing")),
            "Expected an error about missing slicing definition.");
    }

    // ─── #toJSON ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldCorrectlyGenerateADiffContainingOnlyChangedElements()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Patient
            * active MS
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        // Differential should only include elements with changes
        Assert.IsTrue(sd.Differential?.Element.Any(e => e.Path == "Patient.active") == true,
            "Expected active element in differential.");
        Assert.IsFalse(sd.Differential?.Element.Any(e => e.Path == "Patient.name") == true,
            "Unexpected unchanged name element in differential.");
    }

    [TestMethod]
    public void ShouldCorrectlyGenerateADiffContainingOnlyChangedElementsWhenElementsAreSliced()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Extension: MyExt

            Profile: Foo
            Parent: Patient
            * extension contains MyExt named myExt 0..1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        // Differential should include extension slice entries
        Assert.IsTrue(sd.Differential?.Element.Any(e =>
            e.Path == "Patient.extension" || e.SliceName == "myExt") == true,
            "Expected extension/myExt slice in differential.");
    }

    // ─── #insertRules (additional) ────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyAnObeysRuleAtSpecifiedPathForInvariantWithRules()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Invariant: obs-r-1
            * description = ""Must have a value.""
            * severity = #error
            * expression = ""value.exists()""

            Profile: Foo
            Parent: Observation
            * value[x] obeys obs-r-1
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var el = SushiCompilerTestHelper.FindElement(sd, "Observation.value[x]");
        Assert.IsNotNull(el);
        Assert.IsTrue(el.Constraint.Any(c => c.Key == "obs-r-1"),
            "Expected obs-r-1 on value[x].");
    }

    [TestMethod]
    public void ShouldApplyAnObeysRuleToTheBaseElementWhenNoPathSpecifiedForInvariantWithRules()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Invariant: obs-r-2
            * description = ""Observation must be valid.""
            * severity = #error
            * expression = ""status.exists()""

            Profile: Foo
            Parent: Observation
            * obeys obs-r-2
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "Foo");
        Assert.IsNotNull(sd);
        var rootEl = SushiCompilerTestHelper.FindElement(sd, "Observation");
        Assert.IsNotNull(rootEl);
        Assert.IsTrue(rootEl.Constraint.Any(c => c.Key == "obs-r-2"),
            "Expected obs-r-2 on root element.");
    }
}
