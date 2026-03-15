using Hl7.Fhir.Model;

namespace fsh_compiler_tester_r4;

/// <summary>
/// Tests compiling FSH Profile entities to FHIR R4 StructureDefinitions.
/// </summary>
[TestClass]
public class R4ProfileCompilerTests
{
    // ─── Basic profile metadata ───────────────────────────────────────────────

    [TestMethod]
    public void ShouldCompileSimpleProfile()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyPatient");
        Assert.AreEqual("MyPatient", sd.Name);
        Assert.AreEqual("Patient", sd.Type);
        Assert.AreEqual("Patient", sd.BaseDefinition);
        Assert.AreEqual(StructureDefinition.TypeDerivationRule.Constraint, sd.Derivation);
    }

    [TestMethod]
    public void ShouldCompileProfileWithFullMetadata()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient
            Id: my-patient
            Title: ""My Patient Profile""
            Description: ""A profile for testing""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyPatient");
        Assert.AreEqual("my-patient", sd.Id);
        Assert.AreEqual("My Patient Profile", sd.Title);
        Assert.AreEqual("A profile for testing", sd.Description);
    }

    [TestMethod]
    public void ShouldApplyCanonicalBase()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient
            Id: my-patient
        ");
        // Without canonical base, URL is just the id
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual("my-patient", sd.Url);
    }

    // ─── CardRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyCardinalityRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual(1, ed.Min);
        Assert.AreEqual("1", ed.Max);
    }

    [TestMethod]
    public void ShouldApplyMultipleCardinalityRules()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1
            * code 1..1
            * subject 0..0
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var status = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual(1, status.Min);
        Assert.AreEqual("1", status.Max);

        var subject = CompilerTestHelper.GetElement(sd, "subject");
        Assert.AreEqual(0, subject.Min);
        Assert.AreEqual("0", subject.Max);
    }

    [TestMethod]
    public void ShouldApplyUnboundedCardinalityRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component 0..*
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "component");
        Assert.AreEqual(0, ed.Min);
        Assert.AreEqual("*", ed.Max);
    }

    // ─── FlagRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyMustSupportFlag()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status MS
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsTrue(ed.MustSupport);
    }

    [TestMethod]
    public void ShouldApplySummaryFlag()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status SU
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsTrue(ed.IsSummary);
    }

    [TestMethod]
    public void ShouldApplyMultipleFlags()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status MS SU
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsTrue(ed.MustSupport);
        Assert.IsTrue(ed.IsSummary);
    }

    // ─── ValueSetRule ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyValueSetBindingWithStrength()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status from http://hl7.org/fhir/ValueSet/observation-status (required)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsNotNull(ed.Binding);
        Assert.AreEqual(BindingStrength.Required, ed.Binding.Strength);
        Assert.AreEqual("http://hl7.org/fhir/ValueSet/observation-status", ed.Binding.ValueSet);
    }

    [TestMethod]
    public void ShouldApplyValueSetBindingWithoutStrength()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status from ObsStatusVS
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsNotNull(ed.Binding);
        Assert.AreEqual("ObsStatusVS", ed.Binding.ValueSet);
        // Default strength is Preferred when not specified
        Assert.AreEqual(BindingStrength.Preferred, ed.Binding.Strength);
    }

    // ─── FixedValueRule ──────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyFixedString()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * category.text = ""Vital Signs""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "category.text");
        Assert.IsInstanceOfType<FhirString>(ed.Fixed);
        Assert.AreEqual("Vital Signs", ((FhirString)ed.Fixed!).Value);
    }

    [TestMethod]
    public void ShouldApplyFixedBoolean()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component.valueBoolean = true
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "component.valueBoolean");
        Assert.IsInstanceOfType<FhirBoolean>(ed.Fixed);
        Assert.IsTrue(((FhirBoolean)ed.Fixed!).Value);
    }

    [TestMethod]
    public void ShouldApplyFixedCode()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status = #final
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.IsInstanceOfType<Code>(ed.Fixed);
        Assert.AreEqual("final", ((Code)ed.Fixed!).Value);
    }

    // ─── OnlyRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyOnlyRuleWithOneType()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] only Quantity
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "value[x]");
        Assert.IsNotNull(ed.Type);
        Assert.AreEqual(1, ed.Type.Count);
        Assert.AreEqual("Quantity", ed.Type[0].Code);
    }

    [TestMethod]
    public void ShouldApplyOnlyRuleWithMultipleTypes()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] only Quantity or string or boolean
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "value[x]");
        Assert.AreEqual(3, ed.Type.Count);
        CollectionAssert.AreEqual(
            new[] { "Quantity", "string", "boolean" },
            ed.Type.Select(t => t.Code).ToArray());
    }

    // ─── ObeysRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyObeysRuleWithPath()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] obeys obs-1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "value[x]");
        Assert.IsNotNull(ed.Constraint);
        Assert.IsTrue(ed.Constraint.Any(c => c.Key == "obs-1"));
    }

    [TestMethod]
    public void ShouldApplyObeysRuleWithoutPath()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * obeys obs-1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        // Without path, the constraint is on the root element
        var root = sd.Differential?.Element.First();
        Assert.IsNotNull(root);
        Assert.IsTrue(root.Constraint?.Any(c => c.Key == "obs-1") == true);
    }

    // ─── CaretValueRule ──────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyCaretValueShortOnElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^short = ""Status code""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual("Status code", ed.Short);
    }

    [TestMethod]
    public void ShouldApplyCaretValueDefinitionOnElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^definition = ""The status of the result value.""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual("The status of the result value.", ed.Definition);
    }

    [TestMethod]
    public void ShouldApplyCaretValuePublisherOnSD()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^publisher = ""HL7""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual("HL7", sd.Publisher);
    }

    // ─── ContainsRule ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyContainsRuleWithSlicing()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component contains bpSystolic 1..1 and bpDiastolic 0..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var componentEd = CompilerTestHelper.GetElement(sd, "component");
        Assert.IsNotNull(componentEd.Slicing, "Slicing should be set on component");

        var systolic = CompilerTestHelper.GetSliceElement(sd, "component", "bpSystolic");
        Assert.AreEqual(1, systolic.Min);
        Assert.AreEqual("1", systolic.Max);

        var diastolic = CompilerTestHelper.GetSliceElement(sd, "component", "bpDiastolic");
        Assert.AreEqual(0, diastolic.Min);
        Assert.AreEqual("1", diastolic.Max);
    }

    // ─── PathRule ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyPathRule()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "component");
        Assert.IsNotNull(ed);
    }

    // ─── Multiple profiles ────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldCompileMultipleProfiles()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient

            Profile: MyObservation
            Parent: Observation
        ");
        Assert.AreEqual(2, resources.OfType<StructureDefinition>().Count());
        var patient = CompilerTestHelper.GetStructureDefinition(resources, "MyPatient");
        Assert.AreEqual("Patient", patient.Type);
        var obs = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        Assert.AreEqual("Observation", obs.Type);
    }

    // ─── CaretValueRule — ModelInspector dynamic dispatch ────────────────────
    // These properties were NOT in the original switch statements; they verify that
    // the ModelInspector-based FhirCaretValueWriter handles any FHIR property.

    [TestMethod]
    public void ShouldApplyCaretValueVersion()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^version = ""1.2.3""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual("1.2.3", sd.Version);
    }

    [TestMethod]
    public void ShouldApplyCaretValueExperimental()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^experimental = true
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.IsTrue(sd.Experimental);
    }

    [TestMethod]
    public void ShouldApplyCaretValueStatus()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^status = ""draft""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual(PublicationStatus.Draft, sd.Status);
    }

    [TestMethod]
    public void ShouldApplyCaretValuePurposeOnSD()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^purpose = ""For testing purposes""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual("For testing purposes", sd.Purpose);
    }

    [TestMethod]
    public void ShouldApplyCaretValueCommentOnElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^comment = ""See binding for allowed values""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual("See binding for allowed values", ed.Comment);
    }

    [TestMethod]
    public void ShouldApplyCaretValueRequirementsOnElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^requirements = ""Must always be set""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual("Must always be set", ed.Requirements);
    }

    [TestMethod]
    public void ShouldApplyCaretValueLabelOnElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^label = ""Status""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual("Status", ed.Label);
    }

    [TestMethod]
    public void ShouldApplyCaretValueIsModifierOnElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * modifierExtension ^isModifier = true
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "modifierExtension");
        Assert.IsTrue(ed.IsModifier);
    }

    [TestMethod]
    public void ShouldFallBackToExtensionForUnknownCaretPath()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^x-custom-property = ""some value""
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        // Unknown caret path should be stored as an extension
        var ext = sd.Extension?.FirstOrDefault(e => e.Url == "x-custom-property");
        Assert.IsNotNull(ext, "Unknown caret path should produce an extension");
    }

    // ─── Root element ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldAlwaysIncludeRootElement()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var root = sd.Differential?.Element.First();
        Assert.IsNotNull(root);
        Assert.AreEqual("Patient", root.Path);
    }
}
