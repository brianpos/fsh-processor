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
        // A compiler warning should be issued instead of silent skipping.
        var result = CompilerTestHelper.CompileDocResult(@"
            Instance: WeirdThing
            InstanceOf: SomeNonexistentType

            Profile: MyProfile
            Parent: Patient
        ");
        var success = (CompileResult<List<FhirResource>>.SuccessResult)result;
        // Profile should still compile
        Assert.AreEqual(1, success.Value.OfType<StructureDefinition>().Count());
        // A warning should be issued for the unresolvable InstanceOf
        Assert.IsTrue(success.Warnings.Any(w => w.EntityName == "WeirdThing"),
            "A warning should be emitted for the unresolvable instance type");
    }

    // ─── Profile-based InstanceOf (C-IN7) ────────────────────────────────────

    [TestMethod]
    public void ShouldCompileInstanceOfProfileDefinedInSameDoc()
    {
        // When InstanceOf references a Profile defined in the same document,
        // the compiler should walk the SD chain to find the base FHIR type and
        // produce a resource of that type.
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyPatient
            Parent: Patient
            Title: ""My Patient Profile""

            Instance: ExampleMyPatient
            InstanceOf: MyPatient
            Usage: #example
            * id = ""example-patient""
        ");

        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient, "Instance of a Patient profile should produce a Patient resource");
        Assert.AreEqual("example-patient", patient.Id, "Patient Id should be set from rule");
    }

    [TestMethod]
    public void ShouldSetMetaProfileWhenInstanceOfIsProfileName()
    {
        // When InstanceOf is a profile, the produced resource type should match the profile's base.
        // Without a CanonicalBase, meta.profile is not populated, but the resource must be the right type.
        var resources = CompilerTestHelper.CompileDoc(@"
            Profile: MyQuestionnaire
            Parent: Questionnaire
            Id: my-questionnaire

            Instance: ExampleQ
            InstanceOf: MyQuestionnaire
            Usage: #example
            * status = #active
        ");

        var questionnaire = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(questionnaire, "Instance of a Questionnaire profile should produce a Questionnaire");
        Assert.AreEqual("ExampleQ", questionnaire.Id, "Instance Id should default to entity name");
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

    // ─── Soft-index expansion (C-FP2) ────────────────────────────────────────

    [TestMethod]
    public void ShouldExpandSoftIndicesInInstancePath()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: ExamplePatient
            InstanceOf: Patient
            * name[+].family = ""Smith""
            * name[=].given[+] = ""John""
            * name[+].family = ""Jones""
        ");
        var patient = resources.OfType<Patient>().FirstOrDefault();
        Assert.IsNotNull(patient, "Patient instance should compile");
        // [+] on name → name[0], [=] on name → still name[0], [+] on name again → name[1]
        Assert.IsTrue(patient.Name.Count >= 2, "Should have at least 2 name entries");
        Assert.AreEqual("Smith", patient.Name[0].Family, "First name.family should be Smith");
        Assert.AreEqual("Jones", patient.Name[1].Family, "Second name.family should be Jones");
        Assert.IsTrue(patient.Name[0].GivenElement.Count > 0,
            "First name should have given element set by [=]");
    }

    // ─── Contained resource embedding (C-IN6) ────────────────────────────────

    [TestMethod]
    public void ShouldEmbedContainedInstanceByName()
    {
        // `* contained = VS1` should embed the named Instance as a contained resource.
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: MyQuestionnaire
            InstanceOf: Questionnaire
            Usage: #example
            * contained = VS1
            * status = #active

            Instance: VS1
            InstanceOf: ValueSet
            Usage: #example
            * status = #active
            * url = ""http://example.org/ValueSet/VS1""
            * name = ""VS1""
        ");

        var questionnaire = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(questionnaire, "Questionnaire should compile");

        // The ValueSet should be embedded, not emitted as standalone.
        var standalone = resources.OfType<Hl7.Fhir.Model.ValueSet>().ToList();
        Assert.AreEqual(1, standalone.Count, "ValueSet should still be emitted as standalone (#example)");

        Assert.IsNotNull(questionnaire.Contained, "Questionnaire.contained should not be null");
        Assert.AreEqual(1, questionnaire.Contained.Count, "Should have exactly one contained resource");

        var contained = questionnaire.Contained[0] as Hl7.Fhir.Model.ValueSet;
        Assert.IsNotNull(contained, "Contained resource should be a ValueSet");
        Assert.AreEqual("VS1", contained.Id, "Contained ValueSet Id should be VS1");
    }

    [TestMethod]
    public void ShouldEmbedInlineInstanceAsContained()
    {
        // Inline instances must not be emitted standalone, but CAN be contained.
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: MyQuestionnaire
            InstanceOf: Questionnaire
            Usage: #example
            * contained = InlineVS
            * status = #active

            Instance: InlineVS
            InstanceOf: ValueSet
            Usage: #inline
            * status = #draft
            * name = ""InlineVS""
        ");

        var questionnaire = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(questionnaire, "Questionnaire should compile");

        // #inline must NOT appear as standalone
        Assert.AreEqual(0, resources.OfType<Hl7.Fhir.Model.ValueSet>().Count(),
            "#inline ValueSet must not be emitted standalone");

        Assert.IsNotNull(questionnaire.Contained, "Questionnaire.contained should not be null");
        Assert.AreEqual(1, questionnaire.Contained.Count, "Inline instance should be contained");

        var contained = questionnaire.Contained[0] as Hl7.Fhir.Model.ValueSet;
        Assert.IsNotNull(contained, "Contained resource should be a ValueSet");
        Assert.AreEqual("InlineVS", contained.Id, "Contained ValueSet Id should be InlineVS");
    }

    [TestMethod]
    public void ShouldEmbedMultipleContainedInstances()
    {
        // Multiple `* contained[+] = <name>` rules should produce multiple contained entries.
        var resources = CompilerTestHelper.CompileDoc(@"
            Instance: MyQuestionnaire
            InstanceOf: Questionnaire
            Usage: #example
            * contained[+] = VS1
            * contained[+] = VS2
            * status = #active

            Instance: VS1
            InstanceOf: ValueSet
            Usage: #inline
            * status = #active

            Instance: VS2
            InstanceOf: ValueSet
            Usage: #inline
            * status = #active
        ");

        var questionnaire = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(questionnaire, "Questionnaire should compile");
        Assert.IsNotNull(questionnaire.Contained);
        Assert.AreEqual(2, questionnaire.Contained.Count, "Should have two contained resources");
        Assert.IsTrue(questionnaire.Contained.Any(c => c.Id == "VS1"), "VS1 should be contained");
        Assert.IsTrue(questionnaire.Contained.Any(c => c.Id == "VS2"), "VS2 should be contained");
    }

    // ─── RuleSet whitespace and Instance-context expansion regressions ────────

    /// <summary>
    /// Regression test: parameter values with internal whitespace (e.g. "Date of birth") must
    /// not have spaces stripped during substitution.
    /// Bug: ProcessRuleSetParamText used GetText() which dropped hidden-channel whitespace.
    /// Fix: use _tokenStream.GetText(Interval) to preserve whitespace between tokens.
    /// </summary>
    [TestMethod]
    public void ShouldPreserveSpacesInParameterizedRuleSetValues()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            RuleSet: item(linkId, text, type)
            * linkId = ""{linkId}""
            * text = ""{text}""
            * type = {type}

            Instance: MyQuestionnaire
            InstanceOf: Questionnaire
            Usage: #example
            * status = #active
            * item[+]
              * insert item(q1, Date of birth, #date)
        ");

        var q = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(q, "Questionnaire not found");
        Assert.AreEqual(1, q.Item.Count, "Should have one item");
        Assert.AreEqual("Date of birth", q.Item[0].Text, "Spaces inside parameter value must be preserved");
    }

    /// <summary>
    /// Regression test: parameter values with escaped close-paren (\)) must be unescaped
    /// correctly after whitespace-preserving extraction from the token stream.
    /// </summary>
    [TestMethod]
    public void ShouldUnescapeParenInParameterizedRuleSetValues()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            RuleSet: item(linkId, text, type)
            * linkId = ""{linkId}""
            * text = ""{text}""
            * type = {type}

            Instance: MyQuestionnaire
            InstanceOf: Questionnaire
            Usage: #example
            * status = #active
            * item[+]
              * insert item(q1, (internal use\), #string)
        ");

        var q = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(q, "Questionnaire not found");
        Assert.AreEqual(1, q.Item.Count, "Should have one item");
        Assert.AreEqual("(internal use)", q.Item[0].Text, "Escaped paren must be unescaped");
    }

    /// <summary>
    /// Regression test: non-parameterized rulesets inserted into an Instance must be
    /// applied correctly.  Previously they returned SdRule objects which were silently
    /// filtered out by OfType&lt;InstanceRule&gt;(), so extensions like `insert hidden`
    /// were never set on the compiled FHIR resource.
    /// </summary>
    [TestMethod]
    public void ShouldApplyNonParameterizedRuleSetInInstanceContext()
    {
        // Use an alias for the extension URL so the path is a plain identifier (avoids
        // the lexer complexity of embedding an absolute URL directly in a bracket path).
        var resources = CompilerTestHelper.CompileDocs(
            CompilerTestHelper.ParseDoc(@"Alias: $hidden = http://hl7.org/fhir/StructureDefinition/questionnaire-hidden"),
            CompilerTestHelper.ParseDoc(@"
                RuleSet: hidden
                * extension[$hidden].valueBoolean = true

                Instance: MyQuestionnaire
                InstanceOf: Questionnaire
                Usage: #example
                * status = #active
                * item[+]
                  * linkId = ""q1""
                  * text = ""Hidden item""
                  * type = #string
                  * insert hidden
            "));

        var q = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(q, "Questionnaire not found");
        Assert.AreEqual(1, q.Item.Count, "Should have one item");
        var hiddenExt = q.Item[0].Extension.FirstOrDefault(
            e => e.Url == "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden");
        Assert.IsNotNull(hiddenExt, "Hidden extension must be set by non-parameterized ruleset");
        Assert.AreEqual(true, ((FhirBoolean)hiddenExt.Value).Value, "Hidden extension value must be true");
    }

    /// <summary>
    /// Regression test: empty parameter values in a parameterized ruleset (e.g. a missing
    /// definition in <c>insert item(id,,Text,#group)</c>) must not produce empty-string
    /// properties in the compiled output (e.g. <c>"definition": ""</c>).
    /// This matches sushi behaviour where empty parameter values are simply skipped.
    /// </summary>
    [TestMethod]
    public void ShouldSkipEmptyStringFromEmptyParameterSubstitution()
    {
        var resources = CompilerTestHelper.CompileDoc(@"
            RuleSet: item(linkId, definition, text, type)
            * linkId = ""{linkId}""
            * definition = ""{definition}""
            * text = ""{text}""
            * type = {type}

            Instance: MyQuestionnaire
            InstanceOf: Questionnaire
            Usage: #example
            * status = #active
            * item[+]
              * insert item(relative,,Relative group,#group)
        ");

        var q = resources.OfType<Questionnaire>().FirstOrDefault();
        Assert.IsNotNull(q, "Questionnaire not found");
        Assert.AreEqual(1, q.Item.Count, "Should have one item");
        Assert.IsNull(q.Item[0].Definition, "Empty definition parameter must not produce an empty string on the item");
    }
}
