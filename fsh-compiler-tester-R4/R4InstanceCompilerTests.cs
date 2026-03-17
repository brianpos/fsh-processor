using fsh_compiler;
using fsh_compiler_r4;
using fsh_processor;
using fsh_processor.Models;
using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH Instance entities to FHIR R4 resource instances.
/// </summary>
[TestClass]
public class R4InstanceCompilerTests
{
    // ─── Basic instance compilation ───────────────────────────────────────────

    [TestMethod]
    public void ShouldCompileSimplePatientInstance()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
            Usage: #example
            * status = #active
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient, "Patient instance not found");
    }

    [TestMethod]
    public void ShouldSetSimpleStringProperty()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
            * id = ""my-patient""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.AreEqual("my-patient", patient.Id);
    }

    [TestMethod]
    public void ShouldSetNestedProperty()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
            * name[0].family = ""Smith""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.IsTrue(patient.Name.Count > 0, "Name list should have entries");
        Assert.AreEqual("Smith", patient.Name[0].Family);
    }

    [TestMethod]
    public void ShouldSetIndexedListProperty()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
            * name[0].given[0] = ""Jane""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.IsTrue(patient.Name.Count > 0);
        Assert.IsTrue(patient.Name[0].GivenElement.Count > 0);
        Assert.AreEqual("Jane", patient.Name[0].Given.First());
    }

    [TestMethod]
    public void ShouldReturnNullWithoutInspector()
    {
        var fsh = CompilerTestHelper.LeftAlign(@"
            Instance: ExamplePatient
            InstanceOf: Patient
        ");
        var doc = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(doc);
        var fshDoc = ((ParseResult.Success)doc).Document;
        var instance = fshDoc.Entities.OfType<Instance>().First();

        // No inspector → should return null
        var result = FshCompiler.BuildInstance(instance, CompilerContext.Build(fshDoc), new CompilerOptions());
        Assert.IsNull(result, "Should return null when no inspector is provided");
    }

    [TestMethod]
    public void ShouldCompileInstanceWithBooleanProperty()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
            * active = true
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient);
        Assert.IsTrue(patient.Active);
    }

    [TestMethod]
    public void ShouldCompileMultipleInstances()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: PatientA
            InstanceOf: Patient
            * id = ""patient-a""

            Instance: PatientB
            InstanceOf: Patient
            * id = ""patient-b""
        ");
        var patients = resources.OfType<Patient>().ToList();
        Assert.AreEqual(2, patients.Count);
    }

    [TestMethod]
    public void ShouldSkipInstanceWithUnknownType()
    {
        // An unknown InstanceOf type should not fail; it should just produce no resource.
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: WeirdThing
            InstanceOf: SomeNonexistentType

            Profile: MyProfile
            Parent: Patient
        ");
        // Profile should still compile
        Assert.AreEqual(1, resources.OfType<StructureDefinition>().Count());
    }

    // ─── Instance metadata (Id, meta.profile, Usage) ─────────────────────────

    [TestMethod]
    public void ShouldSetInstanceIdFromEntityName()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient, "Patient instance not found");
        Assert.AreEqual("ExamplePatient", patient.Id,
            "Instance Id should default to entity Name");
    }

    [TestMethod]
    public void ShouldSetMetaProfileFromInstanceOf()
    {
        // When InstanceOf is an absolute URL, it should be set as meta.profile.
        var fsh = CompilerTestHelper.LeftAlign(@"
            Instance: ExamplePatient
            InstanceOf: http://hl7.org/fhir/StructureDefinition/Patient
        ");
        var doc = fsh_processor.FshParser.Parse(fsh);
        var fshDoc = ((ParseResult.Success)doc).Document;
        var result = R4FshCompiler.Compile(fshDoc);
        var patient = ((CompileResult<List<FhirResource>>.SuccessResult)result).Value
            .OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient, "Patient instance should compile from a URL InstanceOf");
        Assert.IsNotNull(patient.Meta, "Meta should be set");
        Assert.IsTrue(patient.Meta.Profile.Any(p => p.Contains("Patient")),
            "meta.profile should contain the InstanceOf URL");
    }

    [TestMethod]
    public void ShouldSkipInlineInstance()
    {
        // #inline instances MUST NOT be emitted as standalone resources.
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: InlinePatient
            InstanceOf: Patient
            Usage: #inline

            Profile: MyProfile
            Parent: Patient
        ");
        var patients = resources.OfType<Patient>().ToList();
        Assert.AreEqual(0, patients.Count, "#inline instance should not be emitted");
        Assert.AreEqual(1, resources.OfType<StructureDefinition>().Count(),
            "Profile should still compile");
    }
}
