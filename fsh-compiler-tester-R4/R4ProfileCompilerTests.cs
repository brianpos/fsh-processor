using fsh_compiler;
using fsh_processor;
using Hl7.Fhir.Model;
using FhirResource = Hl7.Fhir.Model.Resource;

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
        Assert.AreEqual("http://hl7.org/fhir/StructureDefinition/Patient", sd.BaseDefinition);
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
        // No '(exactly)' → pattern[x] per FSH spec
        Assert.IsInstanceOfType<FhirString>(ed.Pattern);
        Assert.AreEqual("Vital Signs", ((FhirString)ed.Pattern!).Value);
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
        // No '(exactly)' → pattern[x] per FSH spec
        Assert.IsInstanceOfType<FhirBoolean>(ed.Pattern);
        Assert.IsTrue(((FhirBoolean)ed.Pattern!).Value);
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
        // No '(exactly)' → pattern[x] per FSH spec
        Assert.IsInstanceOfType<Code>(ed.Pattern);
        Assert.AreEqual("final", ((Code)ed.Pattern!).Value);
    }

    [TestMethod]
    public void ShouldApplyPatternRatioWithQuantityParts()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * referenceRange.low = 3 'mg' : 1 'mg'
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "referenceRange.low");
        Assert.IsNotNull(ed.Pattern, "Pattern should be set");
        Assert.IsInstanceOfType<Ratio>(ed.Pattern);
        var ratio = (Ratio)ed.Pattern!;
        Assert.IsNotNull(ratio.Numerator);
        Assert.AreEqual(3m, ratio.Numerator.Value);
        // The FSH parser preserves UCUM unit tokens including their surrounding single quotes
        // (e.g. 'mg'), so the unit string in the model contains the quotes as written in FSH.
        Assert.AreEqual("'mg'", ratio.Numerator.Unit);
        Assert.IsNotNull(ratio.Denominator);
        Assert.AreEqual(1m, ratio.Denominator.Value);
        Assert.AreEqual("'mg'", ratio.Denominator.Unit);
    }

    [TestMethod]
    public void ShouldApplyFixedRatioWithNumericParts()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * referenceRange.low = 10 : 2 (exactly)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "referenceRange.low");
        Assert.IsNotNull(ed.Fixed, "Fixed should be set");
        Assert.IsInstanceOfType<Ratio>(ed.Fixed);
        var ratio = (Ratio)ed.Fixed!;
        Assert.IsNotNull(ratio.Numerator);
        Assert.AreEqual(10m, ratio.Numerator.Value);
        Assert.IsNotNull(ratio.Denominator);
        Assert.AreEqual(2m, ratio.Denominator.Value);
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

    // ─── pattern[x] vs fixed[x] ──────────────────────────────────────────────

    [TestMethod]
    public void ShouldApplyPatternWhenExactlyOmitted()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status = #final
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        // Without '(exactly)' keyword → pattern[x]
        Assert.IsInstanceOfType<Code>(ed.Pattern);
        Assert.IsNull(ed.Fixed, "Fixed should not be set without '(exactly)'");
        Assert.AreEqual("final", ((Code)ed.Pattern!).Value);
    }

    [TestMethod]
    public void ShouldApplyFixedWhenExactlyPresent()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status = #final (exactly)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        // With '(exactly)' keyword → fixed[x]
        Assert.IsInstanceOfType<Code>(ed.Fixed);
        Assert.IsNull(ed.Pattern, "Pattern should not be set with '(exactly)'");
        Assert.AreEqual("final", ((Code)ed.Fixed!).Value);
    }

    // ─── ObeysRule + Invariant ────────────────────────────────────────────────

    [TestMethod]
    public void ShouldPopulateConstraintFromInvariant()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Invariant: obs-1
            Description: ""Value must be present""
            Expression: ""value.exists()""
            Severity: #error

            Profile: MyObservation
            Parent: Observation
            * value[x] obeys obs-1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "value[x]");
        Assert.IsNotNull(ed.Constraint);
        var constraint = ed.Constraint.FirstOrDefault(c => c.Key == "obs-1");
        Assert.IsNotNull(constraint, "Constraint 'obs-1' should exist");
        Assert.AreEqual("Value must be present", constraint.Human);
        Assert.AreEqual("value.exists()", constraint.Expression);
        Assert.AreEqual(ConstraintSeverity.Error, constraint.Severity);
    }

    [TestMethod]
    public void ShouldMapWarningSeverityFromInvariant()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Invariant: obs-warn
            Description: ""A warning""
            Severity: #warning

            Profile: MyObservation
            Parent: Observation
            * obeys obs-warn
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var root = sd.Differential?.Element.First();
        Assert.IsNotNull(root);
        var constraint = root.Constraint?.FirstOrDefault(c => c.Key == "obs-warn");
        Assert.IsNotNull(constraint);
        Assert.AreEqual(ConstraintSeverity.Warning, constraint.Severity);
    }

    // ─── OnlyRule with Reference / Canonical ─────────────────────────────────

    [TestMethod]
    public void ShouldApplyOnlyRuleWithReferenceType()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * subject only Reference(Patient)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "subject");
        Assert.IsNotNull(ed.Type);
        Assert.AreEqual(1, ed.Type.Count);
        Assert.AreEqual("Reference", ed.Type[0].Code);
        CollectionAssert.Contains(ed.Type[0].TargetProfile.ToList(), "Patient");
    }

    [TestMethod]
    public void ShouldApplyOnlyRuleWithMultipleReferenceTargets()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyObservation
            Parent: Observation
            * subject only Reference(Patient or Practitioner)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "subject");
        Assert.IsNotNull(ed.Type);
        Assert.AreEqual(1, ed.Type.Count);
        Assert.AreEqual("Reference", ed.Type[0].Code);
        Assert.AreEqual(2, ed.Type[0].TargetProfile.Count());
        CollectionAssert.Contains(ed.Type[0].TargetProfile.ToList(), "Patient");
        CollectionAssert.Contains(ed.Type[0].TargetProfile.ToList(), "Practitioner");
    }

    [TestMethod]
    public void ShouldApplyOnlyRuleWithCanonicalType()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyQuestionnaire
            Parent: Questionnaire
            * item.answerValueSet only Canonical(ValueSet)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "item.answerValueSet");
        Assert.IsNotNull(ed.Type);
        Assert.AreEqual(1, ed.Type.Count);
        Assert.AreEqual("canonical", ed.Type[0].Code);
        CollectionAssert.Contains(ed.Type[0].TargetProfile.ToList(), "ValueSet");
    }

    [TestMethod]
    public void ShouldResolveAliasInReferenceTarget()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Alias: $Patient = http://hl7.org/fhir/StructureDefinition/Patient

            Profile: MyObservation
            Parent: Observation
            * subject only Reference($Patient)
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "subject");
        CollectionAssert.Contains(
            ed.Type[0].TargetProfile.ToList(),
            "http://hl7.org/fhir/StructureDefinition/Patient");
    }

    // ─── Multi-document compilation ──────────────────────────────────────────

    [TestMethod]
    public void ShouldCompileMultipleDocsWithSharedAliases()
    {
        var doc1 = fsh_processor.FshParser.Parse(CompilerTestHelper.LeftAlign(@"
            Alias: $Patient = http://hl7.org/fhir/StructureDefinition/Patient
        "));
        var doc2 = fsh_processor.FshParser.Parse(CompilerTestHelper.LeftAlign(@"
            Profile: MyObservation
            Parent: Observation
            * subject only Reference($Patient)
        "));

        var fshDoc1 = ((fsh_processor.Models.ParseResult.Success)doc1).Document;
        var fshDoc2 = ((fsh_processor.Models.ParseResult.Success)doc2).Document;

        var result = fsh_compiler_r4.R4FshCompiler.Compile(new[] { fshDoc1, fshDoc2 });
        Assert.IsTrue(result.IsSuccess, "Multi-doc compilation should succeed");
        var resources = ((CompileResult<List<FhirResource>>.SuccessResult)result).Value;
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyObservation");
        var ed = CompilerTestHelper.GetElement(sd, "subject");
        CollectionAssert.Contains(
            ed.Type[0].TargetProfile.ToList(),
            "http://hl7.org/fhir/StructureDefinition/Patient");
    }

    [TestMethod]
    public void ShouldCompileMultipleDocsWithSharedInvariant()
    {
        var doc1 = fsh_processor.FshParser.Parse(CompilerTestHelper.LeftAlign(@"
            Invariant: obs-1
            Description: ""Must have value""
            Expression: ""value.exists()""
            Severity: #error
        "));
        var doc2 = fsh_processor.FshParser.Parse(CompilerTestHelper.LeftAlign(@"
            Profile: MyObservation
            Parent: Observation
            * obeys obs-1
        "));

        var fshDoc1 = ((fsh_processor.Models.ParseResult.Success)doc1).Document;
        var fshDoc2 = ((fsh_processor.Models.ParseResult.Success)doc2).Document;

        var result = fsh_compiler_r4.R4FshCompiler.Compile(new[] { fshDoc1, fshDoc2 });
        Assert.IsTrue(result.IsSuccess);
        var resources = ((CompileResult<List<FhirResource>>.SuccessResult)result).Value;
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var root = sd.Differential?.Element.First();
        var constraint = root?.Constraint?.FirstOrDefault(c => c.Key == "obs-1");
        Assert.IsNotNull(constraint, "Cross-document invariant should be resolved");
        Assert.AreEqual("Must have value", constraint.Human);
    }

    // ─── InsertRule expansion ─────────────────────────────────────────────────

    [TestMethod]
    public void ShouldExpandInsertRuleFromRuleSet()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            RuleSet: CommonStatus
            * status 1..1 MS

            Profile: MyObservation
            Parent: Observation
            * insert CommonStatus
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        var ed = CompilerTestHelper.GetElement(sd, "status");
        Assert.AreEqual(1, ed.Min);
        Assert.AreEqual("1", ed.Max);
        Assert.IsTrue(ed.MustSupport);
    }

    // ─── ContainsRule with named alias (Gap 10) ──────────────────────────────

    [TestMethod]
    public void ShouldApplyContainsRuleWithNamedAlias()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient
            * extension contains http://example.org/ext named myExt 0..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyPatient");
        // Slice name should be 'myExt' (from the named alias), not 'http://example.org/ext'
        var sliceEd = CompilerTestHelper.GetSliceElement(sd, "extension", "myExt");
        Assert.IsNotNull(sliceEd, "Slice named 'myExt' should exist");
        Assert.AreEqual(0, sliceEd.Min);
        Assert.AreEqual("1", sliceEd.Max);
    }

    [TestMethod]
    public void ShouldSetTypeOnNamedSlice()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Alias: $MyExt = http://example.org/StructureDefinition/myExt

            Profile: MyPatient
            Parent: Patient
            * extension contains $MyExt named myExt 0..1
        ");
        var sd = CompilerTestHelper.GetStructureDefinition(resources, "MyPatient");
        var sliceEd = CompilerTestHelper.GetSliceElement(sd, "extension", "myExt");
        Assert.IsNotNull(sliceEd.Type, "Slice element should have Type set");
        Assert.AreEqual(1, sliceEd.Type.Count);
        Assert.AreEqual("http://example.org/StructureDefinition/myExt", sliceEd.Type[0].Code);
    }

    // ─── Compiler warnings (Gap 15) ──────────────────────────────────────────

    [TestMethod]
    public void ShouldEmitWarningForUnresolvedInsertRule()
    {
        var fsh = CompilerTestHelper.LeftAlign(@"
            Profile: MyPatient
            Parent: Patient
            * insert NonExistentRuleSet
        ");
        var doc = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<fsh_processor.Models.ParseResult.Success>(doc);
        var fshDoc = ((fsh_processor.Models.ParseResult.Success)doc).Document;

        var result = fsh_compiler_r4.R4FshCompiler.Compile(fshDoc);
        Assert.IsTrue(result.IsSuccess, "Should succeed despite unresolved rule set");
        Assert.IsTrue(result.Warnings.Count > 0, "Should emit at least one warning");
        Assert.IsTrue(result.Warnings.Any(w => w.Message.Contains("NonExistentRuleSet")),
            "Warning should mention the missing rule set name");
    }

    // ─── Profile metadata (Status, Abstract, Kind) ───────────────────────────

    [TestMethod]
    public void ShouldSetStatusActiveOnProfile()
    {
        var resources = CompilerTestHelper.CompileDoc(CompilerTestHelper.LeftAlign(@"
            Profile: MyPatient
            Parent: Patient
        "));
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual(PublicationStatus.Active, sd.Status,
            "Profile should have Status = active");
    }

    [TestMethod]
    public void ShouldSetAbstractFalseOnProfile()
    {
        var resources = CompilerTestHelper.CompileDoc(CompilerTestHelper.LeftAlign(@"
            Profile: MyPatient
            Parent: Patient
        "));
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.IsFalse(sd.Abstract, "Profile should have Abstract = false");
    }

    [TestMethod]
    public void ShouldSetKindResourceForProfileOfDomainResource()
    {
        var resources = CompilerTestHelper.CompileDoc(CompilerTestHelper.LeftAlign(@"
            Profile: MyPatient
            Parent: Patient
        "));
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.Resource, sd.Kind,
            "Profile of a resource should have Kind = resource");
    }

    [TestMethod]
    public void ShouldSetKindComplexTypeForProfileOfDatatype()
    {
        var resources = CompilerTestHelper.CompileDoc(CompilerTestHelper.LeftAlign(@"
            Profile: MyAddress
            Parent: Address
        "));
        var sd = CompilerTestHelper.GetStructureDefinition(resources);
        Assert.AreEqual(StructureDefinition.StructureDefinitionKind.ComplexType, sd.Kind,
            "Profile of a complex datatype should have Kind = complex-type");
    }

    [TestMethod]
    public void ShouldUseResourceTypePathSegmentInUrl()
    {
        var fsh = CompilerTestHelper.LeftAlign(@"
            Profile: MyPatient
            Id: my-patient
            Parent: Patient
        ");
        var doc = FshParser.Parse(fsh);
        var fshDoc = ((fsh_processor.Models.ParseResult.Success)doc).Document;
        var opts = new CompilerOptions
        {
            CanonicalBase = "http://example.org/fhir",
            FhirVersion = "4.0.1",
            Inspector = Hl7.Fhir.Model.ModelInfo.ModelInspector
        };
        var result = FshCompiler.Compile(fshDoc, opts);
        var sd = (StructureDefinition)((CompileResult<List<FhirResource>>.SuccessResult)result).Value[0];
        Assert.AreEqual("http://example.org/fhir/StructureDefinition/my-patient", sd.Url,
            "Profile URL should use /StructureDefinition/ segment");
    }
}
