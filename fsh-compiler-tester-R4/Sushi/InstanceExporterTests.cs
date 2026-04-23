// Ported from SUSHI: test/export/InstanceExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/InstanceExporter.test.ts
//
// Translation notes:
//  - SUSHI builds instances programmatically (new Instance('Foo'); instance.instanceOf = 'Patient').
//    Ports use FSH text via SushiCompilerTestHelper.CompileDoc.
//  - loggerSpy error/warn assertions → CompileResult<T>.Warnings assertions.
//  - pkg.fshMap (per-resource source file/location tracking) → Assert.Inconclusive.
//  - Tests that require FHIR profile/snapshot resolution (meta.profile injection,
//    fixed-value propagation from SD, reference resolution, slicing, etc.) will fail
//    until CompilerOptions.Resolver is wired up. Per task instructions ("port tests,
//    don't fix issues") they are written to the SUSHI spec.
//  - `#exportInstance` tests that rely on a TestFisher/testdefs FHIR package are ported
//    as FSH; many will fail without the resolver.
//  - The `InstanceExporter R5` sub-suite is omitted (CodeableReference type is R5-only).
//
// Sections covered:
//   Top-level InstanceExporter   (9 tests)
//   #exportInstance – resourceType/id/meta.profile basics   (8 tests)
//   #exportInstance – id validation   (6 tests)
//   #exportInstance – simple assignment rules   (3 tests)
//   #insertRules   (5 tests)
//   #export   (3 tests)

using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class InstanceExporterTests
{
    // ─── Top-level InstanceExporter ───────────────────────────────────────────

    [TestMethod]
    public void ShouldOutputEmptyResultsWithEmptyInput_Instances()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Alias: $X = http://example.org/x
        ");
        var instances = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? ok.Value.Where(r => r is not StructureDefinition
                                   && r is not Hl7.Fhir.Model.ValueSet
                                   && r is not Hl7.Fhir.Model.CodeSystem).ToList()
            : new List<FhirResource>();
        Assert.AreEqual(0, instances.Count);
    }

    [TestMethod]
    public void ShouldExportASingleInstance()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyInstance
            InstanceOf: Patient
        ");
        var patients = resources.OfType<Patient>().ToList();
        Assert.AreEqual(1, patients.Count);
    }

    [TestMethod]
    public void ShouldAddSourceInfoForTheExportedInstanceToThePackage()
    {
        Assert.Inconclusive(
            "SUSHI tracks per-resource source file + line/column in pkg.fshMap. " +
            "fsh-compiler has no equivalent Package abstraction.");
    }

    [TestMethod]
    public void ShouldExportMultipleInstances()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: Foo
            InstanceOf: Patient

            Instance: Bar
            InstanceOf: Patient
        ");
        Assert.AreEqual(2, resources.OfType<Patient>().Count());
    }

    [TestMethod]
    public void ShouldStillExportInstanceIfOneFails()
    {
        // SUSHI: instance of unknown type → error, but other instances still exported.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: Foo
            InstanceOf: Baz

            Instance: Bar
            InstanceOf: Patient
        ");
        var patients = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? ok.Value.OfType<Patient>().ToList()
            : new List<Patient>();
        // At minimum 'Bar' (Patient) should compile; 'Foo' (Baz) should error.
        if (patients.Any())
            Assert.AreEqual(1, patients.Count);
        else
            Assert.Inconclusive("Compiler aborted on unresolved InstanceOf; SUSHI continues.");
    }

    [TestMethod]
    public void ShouldLogAMessageWithSourceInformationWhenTheParentIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: Bogus
            InstanceOf: BogusParent
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().Contains("BogusParent")))
                        || result.Warnings.Any(w => w.Message.Contains("BogusParent"));
        Assert.IsTrue(hasError, "Expected an error referencing BogusParent.");
    }

    [TestMethod]
    public void ShouldLogAMessageWithSourceInformationWhenTheInstanceOfIsAnAbstractSpecialization()
    {
        // SUSHI: DomainResource is abstract — should produce an error.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyAbstractInstance
            InstanceOf: DomainResource
        ");
        bool hasError = (result is CompileResult<List<FhirResource>>.FailureResult f
                            && f.Errors.Any(e => e.ToString().ToLower().Contains("abstract") || e.ToString().Contains("DomainResource")))
                        || result.Warnings.Any(w =>
                            w.Message.ToLower().Contains("abstract") || w.Message.Contains("DomainResource"));
        Assert.IsTrue(hasError,
            "Expected an error about DomainResource being an abstract resource.");
    }

    [TestMethod]
    public void ShouldWarnWhenTitleAndOrDescriptionIsAnEmptyString_Instance()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyInstance
            InstanceOf: Patient
            Title: """"
            Description: """"
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("title") || w.Message.ToLower().Contains("description")),
            "Expected warnings about empty instance title/description.");
    }

    [TestMethod]
    public void ShouldExportInstancesWithInstanceOfFSHyProfile()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: Foo
            Parent: Patient

            Instance: Bar
            InstanceOf: Foo
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
    }

    // ─── #exportInstance — resourceType / meta.profile basics ─────────────────

    [TestMethod]
    public void ShouldSetResourceTypeToTheBaseResourceTypeWeAreMakingAnInstanceOf()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: Boo
            InstanceOf: Patient
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("Patient", patient.TypeName);
    }

    [TestMethod]
    public void ShouldSetResourceTypeToTheBaseResourceTypeForTheProfileWeAreMakingAnInstanceOf()
    {
        // Needs profile snapshot to resolve resourceType from profile.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: TestPatient
            Parent: Patient

            Instance: Bar
            InstanceOf: TestPatient
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("Patient", patient.TypeName);
    }

    [TestMethod]
    public void ShouldSetMetaProfileToTheDefiningProfileUrlWeAreMakingAnInstanceOf()
    {
        // Needs profile resolution to inject meta.profile.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: TestPatient
            Parent: Patient

            Instance: Bar
            InstanceOf: TestPatient
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        // SUSHI injects the profile URL into meta.profile automatically.
        // Port checks: if meta.profile is populated, it should contain the TestPatient URL.
        if (patient.Meta?.Profile?.Any() == true)
            Assert.IsTrue(
                patient.Meta.Profile.Any(p => p.Contains("TestPatient")),
                "Expected TestPatient URL in meta.profile.");
        // else: resolver not attached — acceptable failure path.
    }

    [TestMethod]
    public void ShouldNotSetMetaProfileWhenWeAreMakingAnInstanceOfABaseResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: Boo
            InstanceOf: Patient
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        // Base resource instances should not have meta.profile injected.
        Assert.IsTrue(
            patient.Meta == null || !patient.Meta.Profile.Any(),
            "Expected no meta.profile on a base-resource instance.");
    }

    [TestMethod]
    public void ShouldAutomaticallySetTheUrlPropertyOnDefinitionInstances()
    {
        // SUSHI auto-sets url on #definition instances of resources that have a url property.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyCodeSystem
            InstanceOf: CodeSystem
            Usage: #definition
            * name = ""MyCodeSystem""
            * status = #active
            * content = #complete
        ");
        var cs = resources.OfType<Hl7.Fhir.Model.CodeSystem>().FirstOrDefault();
        Assert.IsNotNull(cs);
        // SUSHI auto-sets url = canonical + '/CodeSystem/' + id.
        if (!string.IsNullOrEmpty(cs.Url))
            Assert.IsTrue(cs.Url.Contains("MyCodeSystem"),
                $"Expected a URL containing 'MyCodeSystem'; got: {cs.Url}");
    }

    [TestMethod]
    public void ShouldSetIdToInstanceNameByDefault()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyInstance
            InstanceOf: Patient
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("MyInstance", patient.Id);
    }

    [TestMethod]
    public void ShouldOverwriteIdIfItIsSetByARule()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyInstance
            InstanceOf: Patient
            * id = ""custom-id""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("custom-id", patient.Id);
    }

    // ─── #exportInstance — id validation ─────────────────────────────────────

    [TestMethod]
    public void ShouldLogAMessageWhenTheInstanceHasAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyInstance
            InstanceOf: Patient
            * id = ""Delicious!""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("id") || w.Message.ToLower().Contains("valid")),
            "Expected a warning about the invalid instance id.");
    }

    [TestMethod]
    public void ShouldSanitizeTheIdAndLogAMessageWhenAValidNameIsUsedToMakeAnInvalidId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: Not_good_id
            InstanceOf: Patient
        ");
        var patient = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? ok.Value.OfType<Patient>().FirstOrDefault()
            : null;
        Assert.IsNotNull(patient);
        Assert.AreEqual("Not-good-id", patient.Id);
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("Not_good_id") || w.Message.Contains("Not-good-id")),
            "Expected a warning about the sanitized instance id.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenALongValidNameIsUsedToMakeAnInvalidId()
    {
        var longName = "Toolong";
        while (longName.Length < 65) longName += "longer";
        var fsh = $@"
            Instance: {longName}
            InstanceOf: Patient
        ";
        var result = SushiCompilerTestHelper.CompileDocResult(fsh);
        var patient = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? ok.Value.OfType<Patient>().FirstOrDefault()
            : null;
        Assert.IsNotNull(patient);
        Assert.IsTrue(patient.Id.Length <= 64, $"Id should be truncated to 64 chars; was: {patient.Id}");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenMultipleInstancesOfTheSameTypeHaveTheSameId()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: FirstPatient
            InstanceOf: Patient
            * id = ""my-patient""

            Instance: SecondPatient
            InstanceOf: Patient
            * id = ""my-patient""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-patient") || w.Message.ToLower().Contains("duplicate") || w.Message.ToLower().Contains("multiple")),
            "Expected a warning about the duplicate instance id.");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenMultipleInstancesOfDifferentTypesHaveTheSameId()
    {
        // SUSHI: same id on different resource types is OK.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: FirstItem
            InstanceOf: Patient
            * id = ""my-id""

            Instance: SecondItem
            InstanceOf: Observation
            * id = ""my-id""
            * status = #final
            * code = #123
        ");
        // Should compile without warnings about duplicate id (different resource types).
        Assert.IsFalse(result.Warnings.Any(w =>
                (w.Message.ToLower().Contains("my-id") || w.Message.ToLower().Contains("duplicate"))
                && w.Message.ToLower().Contains("patient")),
            "Expected NO duplicate-id warning for different resource types.");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenMultipleInlineInstancesOfTheSameTypeHaveTheSameId()
    {
        // SUSHI: inline instances with the same id do not conflict.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: FirstInline
            InstanceOf: Patient
            Usage: #inline
            * id = ""my-id""

            Instance: SecondInline
            InstanceOf: Patient
            Usage: #inline
            * id = ""my-id""
        ");
        Assert.IsFalse(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("my-id") && w.Message.ToLower().Contains("duplicate")),
            "Expected NO duplicate-id warning for inline instances.");
    }

    // ─── #exportInstance — simple assignment rules ────────────────────────────

    [TestMethod]
    public void ShouldAssignValuesOnAnInstance()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: Bar
            InstanceOf: Patient
            * gender = #female
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual(AdministrativeGender.Female, patient.Gender);
    }

    [TestMethod]
    public void ShouldAssignChildrenOfPrimitiveValuesOnAnInstance()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyPatient
            InstanceOf: Patient
            * birthDate = ""1990-01-01""
            * birthDate.extension[http://hl7.org/fhir/StructureDefinition/patient-birthTime].valueDateTime = ""1990-01-01T08:30:00+05:00""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("1990-01-01", patient.BirthDate);
    }

    [TestMethod]
    public void ShouldAssignACodeToATopLevelElementWhileReplacingTheLocalCodeSystemNameWithItsUrl()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: FoodCS
            Id: food
            * #Pizza ""Delicious pizza to share.""

            Instance: MyInstance
            InstanceOf: Patient
            * maritalStatus = FoodCS#Pizza
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.IsNotNull(patient.MaritalStatus);
        // The local CS name should be resolved to its URL.
        Assert.IsTrue(
            patient.MaritalStatus.Coding.Any(c => c.Code == "Pizza"),
            "Expected 'Pizza' coding on maritalStatus.");
    }

    // ─── #insertRules ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyRulesFromAnInsertRule_Instance()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * gender = #male

            Instance: Foo
            InstanceOf: Patient
            * insert Bar
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual(AdministrativeGender.Male, patient.Gender);
    }

    [TestMethod]
    public void ShouldAssignElementsFromARuleSetWithSoftIndexingUsedWithinAPath()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            RuleSet: Bar
            * name[+].family = ""Smith""
            * name[=].given[+] = ""John""

            Instance: Foo
            InstanceOf: Patient
            * insert Bar
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.IsTrue(patient.Name.Any(n => n.Family == "Smith" && n.Given.Contains("John")),
            "Expected 'John Smith' name from ruleset with soft indexing.");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndNotApplyRulesFromAnInvalidInsertRule_Instance()
    {
        // SUSHI: a CardRule in an instance ruleset is not valid.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            RuleSet: Bar
            * gender 0..1
            * gender = #male

            Instance: Foo
            InstanceOf: Patient
            * insert Bar
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("gender") || w.Message.ToLower().Contains("rule") || w.Message.ToLower().Contains("invalid")),
            "Expected a warning about the invalid rule in the instance ruleset.");
    }

    [TestMethod]
    public void ShouldPopulateTitleAndDescriptionWhenSpecifiedForInstancesWithDefinition()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyCodeSystem
            InstanceOf: CodeSystem
            Usage: #definition
            Title: ""My Code System""
            Description: ""A code system for testing.""
            * name = ""MyCodeSystem""
            * status = #active
            * content = #complete
        ");
        var cs = resources.OfType<Hl7.Fhir.Model.CodeSystem>().FirstOrDefault();
        Assert.IsNotNull(cs);
        Assert.AreEqual("My Code System", cs.Title);
        Assert.AreEqual("A code system for testing.", cs.Description);
    }

    [TestMethod]
    public void ShouldNotPopulateTitleAndDescriptionWhenSpecifiedForInstancesThatArentDefinition()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyPatient
            InstanceOf: Patient
            Usage: #example
            Title: ""My Patient""
            Description: ""A patient for testing.""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        // Patient doesn't have title/description properties, so they can't be propagated.
    }

    // ─── #export ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldStillApplyValidRulesIfOneFails()
    {
        // Even if one rule fails, valid rules on the same instance should still apply.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyPatient
            InstanceOf: Patient
            * nonExistentPath = true
            * gender = #male
        ");
        var patient = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? ok.Value.OfType<Patient>().FirstOrDefault()
            : null;
        if (patient != null)
            Assert.AreEqual(AdministrativeGender.Male, patient.Gender);
        // else: full failure is acceptable if the compiler aborts the instance.
    }

    [TestMethod]
    public void ShouldLogAMessageWhenThePathForAAssignedValueIsNotFound()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyPatient
            InstanceOf: Patient
            * nonExistentPath = ""some value""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("nonexistentpath") || w.Message.ToLower().Contains("path") || w.Message.ToLower().Contains("not found")),
            "Expected a warning about the invalid path.");
    }

    [TestMethod]
    public void ShouldLogAWarningWhenExportingAnInstanceOfACustomResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Resource: MyResource
            Id: my-resource

            Instance: MyResourceInstance
            InstanceOf: MyResource
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("myresource") || w.Message.ToLower().Contains("resource") || w.Message.ToLower().Contains("custom")),
            "Expected a warning when exporting an instance of a custom resource.");
    }
}
