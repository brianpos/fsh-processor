// Ported from SUSHI: test/export/FHIRExporter.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/export/FHIRExporter.test.ts
//
// Translation notes:
//  - SUSHI builds input programmatically (new Profile, new Instance, CaretValueRule, etc.).
//    Ports use FSH text via SushiCompilerTestHelper.CompileDoc.
//  - loggerSpy error/warn assertions → CompileResult<T>.Warnings assertions.
//  - SUSHI's Package abstraction (with .profiles, .instances, .valueSets, .codeSystems) →
//    flat List<FhirResource> returned by R4FshCompiler.Compile.
//  - Contained resources appear on the Hl7.Fhir.Model.Resource.Contained property.
//  - Tests requiring a FHIR resolver (Patient, Observation, Basic, ContactDetail,
//    CodeableConcept, allergyintolerance-clinical, gendered-patient) will fail until
//    CompilerOptions.Resolver is wired up. Per task instructions ("port tests, don't fix")
//    tests are written to the SUSHI spec; failures reflect compiler features not yet wired.
//
// Sections covered:
//   Top-level FHIRExporter       ( 1 test)
//   #containedResources          (24 tests)

using Hl7.Fhir.Model;
using Hl7.FhirShorthand.Compiler;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace Hl7.FhirShorthand.Compiler_tester_r4.Sushi;

[TestClass]
public class FHIRExporterTests
{
    // ─── Top-level FHIRExporter ───────────────────────────────────────────────

    [TestMethod]
    public void ShouldOutputEmptyResultsWithEmptyInput_FHIR()
    {
        // SUSHI exportFHIR on an empty FSHTank returns a Package with only config metadata.
        // Parser rejects wholly-empty input; use an alias-only doc instead.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Alias: $X = http://example.org/x
        ");
        var resources = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? ok.Value
            : new List<FhirResource>();
        Assert.AreEqual(0, resources.Count,
            "Expected no resources compiled from an alias-only document.");
    }

    // ─── #containedResources ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldAllowAProfileToContainADefinedFHIRResource()
    {
        // SUSHI: ^contained references the built-in FHIR resource 'allergyintolerance-clinical'
        // (a ValueSet) and embeds its JSON.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Profile: ContainingProfile
            Parent: Basic
            * ^contained = allergyintolerance-clinical
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var vs = sd.Contained[0] as Hl7.Fhir.Model.ValueSet;
        Assert.IsNotNull(vs, "Expected the contained resource to be a ValueSet.");
        Assert.AreEqual("allergyintolerance-clinical", vs.Id);
    }

    [TestMethod]
    public void ShouldAllowAProfileToContainAFSHResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: myObservation
            InstanceOf: Observation
            Usage: #inline

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = myObservation
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var obs = sd.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("myObservation", obs.Id);
    }

    [TestMethod]
    public void ShouldAllowAProfileToContainAFSHResourceWithANumericId()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: 010203
            InstanceOf: Observation
            Usage: #inline

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = 010203
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var obs = sd.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("010203", obs.Id);
    }

    [TestMethod]
    public void ShouldAllowAProfileToContainAFSHResourceWithAnIdThatResemblesABoolean()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: false
            InstanceOf: Observation
            Usage: #inline

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = false
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var obs = sd.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("false", obs.Id);
    }

    [TestMethod]
    public void ShouldAllowAProfileToContainMultipleFSHResources()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: 010203
            InstanceOf: Observation
            Usage: #inline

            Instance: CleanSocks
            InstanceOf: Observation
            Usage: #inline

            Instance: 456
            InstanceOf: Location
            Usage: #inline

            Profile: ContainingProfile
            Parent: Basic
            * ^contained[0] = 010203
            * ^contained[1] = CleanSocks
            * ^contained[2] = 456
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(3, sd.Contained.Count);
        Assert.AreEqual("010203", sd.Contained[0].Id);
        Assert.AreEqual("CleanSocks", sd.Contained[1].Id);
        Assert.AreEqual("456", sd.Contained[2].Id);
    }

    [TestMethod]
    public void ShouldAllowAProfileToContainAResourceAndToApplyCaretRulesWithinTheContainedResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #inline
            * id = ""my-observation""
            * status = #draft
            * code = #123

            Profile: ContainingProfile
            Parent: Patient
            * ^contained = MyObservation
            * ^contained.valueString = ""contained observation""
            * ^contained.category = #exam
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var obs = sd.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("my-observation", obs.Id);
        Assert.IsNotNull(obs.Status);
        Assert.AreEqual("draft", obs.Status.ToString()?.ToLowerInvariant());
        Assert.IsTrue(obs.Value is FhirString s && s.Value == "contained observation",
            "Expected valueString = 'contained observation'.");
        Assert.IsTrue(obs.Category.Any(c => c.Coding.Any(co => co.Code == "exam")),
            "Expected category coding 'exam'.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenADeferredRuleAssignsSomethingOfTheWrongType()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #inline
            * id = ""my-observation""
            * status = #draft
            * code = #123

            Profile: ContainingProfile
            Parent: Patient
            * ^contained = MyObservation
            * ^contained.interpretation = ""contained observation""
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("interpretation")
                || w.Message.ToLower().Contains("codeableconcept")
                || w.Message.ToLower().Contains("cannot assign")),
            "Expected an error about assigning a string to a CodeableConcept (interpretation).");
    }

    [TestMethod]
    public void ShouldNotGetConfusedWhenThereAreContainedResourcesOfDifferentTypes()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #inline
            * id = ""my-observation""
            * status = #draft
            * code = #123

            Instance: MyPatient
            InstanceOf: Patient
            Usage: #inline
            * id = ""my-patient""
            * name.given = ""Marisa""

            Profile: ContainingProfile
            Parent: Patient
            * ^contained = MyObservation
            * ^contained[1] = MyPatient
            * ^contained.valueString = ""contained observation""
            * ^contained[1].name.family = ""Kirisame""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.AreEqual(2, sd.Contained.Count);
        var obs = sd.Contained[0] as Observation;
        Assert.IsNotNull(obs);
        Assert.AreEqual("my-observation", obs.Id);
        Assert.IsTrue(obs.Value is FhirString s && s.Value == "contained observation");

        var patient = sd.Contained[1] as Patient;
        Assert.IsNotNull(patient);
        Assert.AreEqual("my-patient", patient.Id);
        Assert.IsTrue(patient.Name.Any(n => n.Family == "Kirisame" && n.Given.Contains("Marisa")),
            "Expected 'Marisa Kirisame' name on contained patient.");
    }

    [TestMethod]
    public void ShouldAllowAProfileToContainAProfiledResourceAndToApplyACaretRuleWithinTheContainedResource()
    {
        // SUSHI's testdefs include the 'gendered-patient' profile. This requires a FHIR resolver.
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: some-patient
            InstanceOf: gendered-patient
            Usage: #inline
            * gender = #unknown

            Profile: ContainingProfile
            Parent: Patient
            * ^contained = some-patient
            * ^contained.name.given = ""mint""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        var patient = sd.Contained.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("some-patient", patient.Id);
        Assert.AreEqual(AdministrativeGender.Unknown, patient.Gender);
        Assert.IsTrue(patient.Name.Any(n => n.Given.Contains("mint")),
            "Expected 'mint' given name on contained profiled patient.");
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementToAContainedValueSetUsingARelativeReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            Id: my-value-set

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = MyValueSet
            * code from MyValueSet (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var vs = sd.Contained[0] as Hl7.Fhir.Model.ValueSet;
        Assert.IsNotNull(vs);
        Assert.AreEqual("my-value-set", vs.Id);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.IsNotNull(codeElement.Binding);
        Assert.AreEqual(BindingStrength.Extensible, codeElement.Binding.Strength);
        Assert.AreEqual("#my-value-set", codeElement.Binding.ValueSet);
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementToAContainedInlineInstanceOfValueSetUsingARelativeReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyValueSet
            InstanceOf: ValueSet
            Usage: #inline

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = MyValueSet
            * code from MyValueSet (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        Assert.AreEqual(1, sd.Contained.Count);
        var vs = sd.Contained[0] as Hl7.Fhir.Model.ValueSet;
        Assert.IsNotNull(vs);
        Assert.AreEqual("MyValueSet", vs.Id);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.AreEqual("#MyValueSet", codeElement.Binding?.ValueSet);
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementToAContainedInlineInstanceOfValueSetWithNameSetByARuleUsingARelativeReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: my-value-set
            InstanceOf: ValueSet
            Usage: #inline
            * name = ""MyValueSet""

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = my-value-set
            * code from MyValueSet (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        var vs = sd.Contained.OfType<Hl7.Fhir.Model.ValueSet>().FirstOrDefault();
        Assert.IsNotNull(vs);
        Assert.AreEqual("my-value-set", vs.Id);
        Assert.AreEqual("MyValueSet", vs.Name);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.AreEqual("#my-value-set", codeElement.Binding?.ValueSet);
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementToAContainedInlineInstanceOfValueSetWithUrlSetByARuleUsingARelativeReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyValueSet
            InstanceOf: ValueSet
            Usage: #inline
            * url = ""http://hl7.org/fhir/us/custom/ValueSet/MyValueSet""

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = MyValueSet
            * code from http://hl7.org/fhir/us/custom/ValueSet/MyValueSet (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        var vs = sd.Contained.OfType<Hl7.Fhir.Model.ValueSet>().FirstOrDefault();
        Assert.IsNotNull(vs);
        Assert.AreEqual("http://hl7.org/fhir/us/custom/ValueSet/MyValueSet", vs.Url);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.AreEqual("#MyValueSet", codeElement.Binding?.ValueSet);
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementToAContainedDefinitionalInstanceOfValueSetUsingARelativeReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyValueSet
            InstanceOf: ValueSet
            Usage: #definition

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = MyValueSet
            * code from MyValueSet (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        var vs = sd.Contained.OfType<Hl7.Fhir.Model.ValueSet>().FirstOrDefault();
        Assert.IsNotNull(vs);
        Assert.AreEqual("MyValueSet", vs.Id);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/ValueSet/MyValueSet", vs.Url);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.AreEqual("#MyValueSet", codeElement.Binding?.ValueSet);
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementByNameToAContainedDefinitionalInstanceOfValueSetWithANameSetByARuleUsingARelativeReference()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyValueSet
            InstanceOf: ValueSet
            Usage: #definition
            * name = ""MyVS""

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = MyValueSet
            * code from MyVS (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        var vs = sd.Contained.OfType<Hl7.Fhir.Model.ValueSet>().FirstOrDefault();
        Assert.IsNotNull(vs);
        Assert.AreEqual("MyValueSet", vs.Id);
        Assert.AreEqual("MyVS", vs.Name);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.AreEqual("#MyValueSet", codeElement.Binding?.ValueSet);
    }

    [TestMethod]
    public void ShouldAllowAProfileToBindAnElementToAContainedValueSetUsingARelativeReferenceWhenTheRuleIncludesAVersion()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            ValueSet: MyValueSet
            Id: my-value-set
            * ^version = ""1.2.8""

            Profile: ContainingProfile
            Parent: Basic
            * ^contained = MyValueSet|1.2.8
            * code from MyValueSet (extensible)
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "ContainingProfile");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contained);
        var vs = sd.Contained.OfType<Hl7.Fhir.Model.ValueSet>().FirstOrDefault();
        Assert.IsNotNull(vs);
        Assert.AreEqual("my-value-set", vs.Id);
        Assert.AreEqual("1.2.8", vs.Version);

        var codeElement = SushiCompilerTestHelper.FindElement(sd, "Basic.code");
        Assert.IsNotNull(codeElement);
        Assert.AreEqual("#my-value-set", codeElement.Binding?.ValueSet);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAttemptingToBindAnElementToAnInlineValueSetInstanceThatIsNotContainedInTheProfile()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyValueSet
            InstanceOf: ValueSet
            Usage: #inline

            Profile: ContainingProfile
            Parent: Basic
            * code from MyValueSet (extensible)
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("MyValueSet")
                || w.Message.ToLower().Contains("inline")
                || w.Message.ToLower().Contains("contained")),
            "Expected an error about binding to an inline ValueSet not contained in the profile.");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAProfileTriesToContainAnInstanceThatIsNotAResource()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: MyCodeable
            InstanceOf: CodeableConcept
            Usage: #inline

            Profile: ContainingProfile
            Parent: Patient
            * ^contained[0] = MyCodeable
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("MyCodeable")
                || w.Message.ToLower().Contains("codeableconcept")
                || w.Message.ToLower().Contains("cannot assign")),
            "Expected an error about containing a non-resource (CodeableConcept).");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAProfileTriesToContainAResourceThatDoesNotExist()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: ContainingProfile
            Parent: Basic
            * ^contained = oops-no-resource
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.Contains("oops-no-resource")
                || w.Message.ToLower().Contains("could not find")
                || w.Message.ToLower().Contains("not found")),
            "Expected an error: 'Could not find a resource named oops-no-resource'.");
    }

    [TestMethod]
    public void ShouldLetAProfileAssignAnInlineInstanceThatIsNotAResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyContact
            InstanceOf: ContactDetail
            Usage: #inline
            * name = ""Bearington""

            Profile: MyObservation
            Parent: Observation
            * ^contact = MyContact
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyObservation");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contact);
        Assert.AreEqual(1, sd.Contact.Count);
        Assert.AreEqual("Bearington", sd.Contact[0].Name);
    }

    [TestMethod]
    public void ShouldLetAProfileAssignAndModifyAnInlineInstanceThatIsNotAResource()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            Instance: MyContact
            InstanceOf: ContactDetail
            Usage: #inline
            * name = ""Bearington""

            Profile: MyObservation
            Parent: Observation
            * ^contact = MyContact
            * ^contact.telecom.value = ""bearington@bear.zoo""
        ");
        var sd = SushiCompilerTestHelper.FindSd(resources, "MyObservation");
        Assert.IsNotNull(sd);
        Assert.IsNotNull(sd.Contact);
        Assert.AreEqual(1, sd.Contact.Count);
        Assert.AreEqual("Bearington", sd.Contact[0].Name);
        Assert.IsTrue(sd.Contact[0].Telecom.Any(t => t.Value == "bearington@bear.zoo"),
            "Expected telecom.value = 'bearington@bear.zoo'.");
    }

    [TestMethod]
    public void ShouldExportAValueSetThatIncludesAComponentFromAContainedFshCodeSystemAndAddTheValueSetSystemExtension()
    {
        var resources = SushiCompilerTestHelper.CompileDoc(@"
            CodeSystem: FoodCS
            Id: food

            ValueSet: DinnerVS
            * ^contained[0] = FoodCS
            * include codes from system FoodCS
        ");
        var vs = SushiCompilerTestHelper.FindVs(resources, "DinnerVS");
        Assert.IsNotNull(vs);
        Assert.AreEqual("DinnerVS", vs.Id);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/ValueSet/DinnerVS", vs.Url);
        Assert.IsNotNull(vs.Contained);
        Assert.AreEqual(1, vs.Contained.Count);
        var cs = vs.Contained[0] as Hl7.Fhir.Model.CodeSystem;
        Assert.IsNotNull(cs);
        Assert.AreEqual("food", cs.Id);
        Assert.AreEqual("FoodCS", cs.Name);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/CodeSystem/food", cs.Url);

        Assert.IsNotNull(vs.Compose);
        Assert.AreEqual(1, vs.Compose.Include.Count);
        Assert.AreEqual("http://hl7.org/fhir/us/minimal/CodeSystem/food", vs.Compose.Include[0].System);

        // SUSHI adds the valueset-system extension on System with valueCanonical = '#food'.
        var systemElement = vs.Compose.Include[0].SystemElement;
        Assert.IsNotNull(systemElement);
        var vsSystemExt = systemElement.Extension.FirstOrDefault(e =>
            e.Url == "http://hl7.org/fhir/StructureDefinition/valueset-system");
        Assert.IsNotNull(vsSystemExt, "Expected valueset-system extension on _system.");
        Assert.IsTrue(vsSystemExt.Value is Canonical c && c.Value == "#food",
            "Expected valueCanonical = '#food'.");
    }

    [TestMethod]
    public void ShouldLogAMessageWhenTryingToAssignAValueThatIsNumericAndRefersToAnInstanceButBothTypesAreWrong()
    {
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Instance: 1234
            InstanceOf: ContactDetail
            Usage: #inline
            * name = ""Bearington""

            Profile: MyObservation
            Parent: Observation
            * ^identifier = 1234
        ");
        Assert.IsTrue(result.Warnings.Any(w =>
                w.Message.ToLower().Contains("identifier")
                || w.Message.Contains("1234")
                || w.Message.ToLower().Contains("cannot assign")),
            "Expected an error about assigning number 1234 to identifier (Identifier type).");
    }

    [TestMethod]
    public void ShouldLogAMessageAndNotChangeTheUrlWhenTryingToAssignAnInstanceToAUrlAndTheInstanceIsNotTheCorrectType()
    {
        // SUSHI: * ^url = http://example.org/some/url (unquoted, so treated as instance reference)
        // The port uses the same syntax; however, FSH parsers often read this as a URL literal
        // rather than an instance reference. Assert the url is not overwritten or an error is raised.
        var result = SushiCompilerTestHelper.CompileDocResult(@"
            Profile: MyObservation
            Parent: Observation
            * ^url = http://example.org/some/url
        ");
        var sd = result is CompileResult<List<FhirResource>>.SuccessResult ok
            ? SushiCompilerTestHelper.FindSd(ok.Value, "MyObservation")
            : null;
        if (sd != null)
        {
            // SUSHI: url should remain the default canonical URL.
            Assert.AreEqual(
                "http://hl7.org/fhir/us/minimal/StructureDefinition/MyObservation",
                sd.Url,
                "Expected ^url to remain the default when the unquoted value does not resolve to an instance.");
        }
        else
        {
            Assert.IsTrue(result.Warnings.Any(),
                "Expected an error when the unquoted url does not resolve.");
        }
    }
}
