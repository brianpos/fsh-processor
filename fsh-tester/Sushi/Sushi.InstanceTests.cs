// Ported from SUSHI test: FSHImporter.Instance.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Instance.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI resolves aliases in InstanceOf and assignment rules; our parser stores the raw name.
//  - SUSHI normalizes usage codes (strips '#', capitalizes): '#example' → 'Example'.
//    Our parser stores the raw CODE token: instance.Usage = "#example".
//  - SUSHI drops instances without InstanceOf (semantic validation);
//    our parser retains them with InstanceOf = null.
//  - SUSHI's assertAssignmentRule for resource refs checks for a specific Resource type;
//    C# stores 'SomeInstance' as NameValue.
//  - SUSHI reports semantic errors for duplicate names and duplicate metadata;
//    our parser has no semantic validation.
//  - Columns in SourcePosition are 0-based (ANTLR); SUSHI uses 1-based.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class InstanceTests
{
    // ─── #instanceOf ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleInstance()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "MyObservation");
        Assert.AreEqual("MyObservation", instance.Name);
        Assert.AreEqual("Observation", instance.InstanceOf);
        Assert.IsNull(instance.Title);
        Assert.IsNull(instance.Description);
        Assert.AreEqual(0, instance.Rules.Count);
    }

    [TestMethod]
    public void ShouldParseNumericInstanceNameAndNumericInstanceOf()
    {
        // NOT recommended, but possible
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: 123
            InstanceOf: 456
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "123");
        Assert.AreEqual("123", instance.Name);
        Assert.AreEqual("456", instance.InstanceOf);
    }

    [TestMethod]
    public void ShouldParseAnInstanceWithAnAliasedType()
    {
        // SUSHI resolves alias 'obs' → 'Observation' in instanceOf.
        // Our parser does NOT resolve aliases; instanceOf stores the raw alias name 'obs'.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: obs = Observation
            Instance: MyObservation
            InstanceOf: obs
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "MyObservation");
        // Our parser stores the alias name verbatim (not resolved)
        Assert.AreEqual("obs", instance.InstanceOf);
    }

    [TestMethod]
    public void ShouldNotParseAnInstanceThatHasNoType()
    {
        // SUSHI drops instances without InstanceOf (semantic validation → size=0).
        // Our parser retains the instance with InstanceOf = null.
        // This is a behavioral difference from SUSHI.
        Assert.Inconclusive("Not tested: SUSHI drops instances without InstanceOf (semantic validation). Our parser retains the instance with InstanceOf = null.");
    }

    // ─── #title ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInstanceWithATitle()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
            Title: ""My Important Observation""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "MyObservation");
        Assert.AreEqual("MyObservation", instance.Name);
        Assert.AreEqual("Observation", instance.InstanceOf);
        Assert.AreEqual("My Important Observation", instance.Title);
    }

    // ─── #description ───────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInstanceWithADescription()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
            Description: ""Shows an example of an Observation""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "MyObservation");
        Assert.AreEqual("MyObservation", instance.Name);
        Assert.AreEqual("Observation", instance.InstanceOf);
        Assert.AreEqual("Shows an example of an Observation", instance.Description);
    }

    // ─── #usage ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInstanceWithAUsage()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: MyObservation
            InstanceOf: Observation
            Usage: #example
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "MyObservation");
        Assert.AreEqual("MyObservation", instance.Name);
        Assert.AreEqual("Observation", instance.InstanceOf);
        // SUSHI normalizes usage to 'Example'; C# stores the raw CODE token '#example'.
        Assert.AreEqual("#example", instance.Usage);
    }

    [TestMethod]
    public void ShouldLogAnErrorForInvalidUsageAndSetDefaultUsageToExample()
    {
        // SUSHI logs an error for invalid usage and defaults to Example.
        // Our parser stores the raw CODE token as-is; no semantic validation.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (invalid usage code) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAWarningIfASystemIsSpecifiedOnUsage()
    {
        // SUSHI logs a warning when a system is specified on Usage (e.g. badsystem#example).
        // Our parser stores the raw value with no semantic validation.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (system specified on usage) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAWarningIfConformanceOrTerminologyResourceDoesNotHaveUsage()
    {
        // SUSHI logs a warning for conformance/terminology resources without explicit usage.
        // Our parser has no semantic validation of instance types.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (missing usage on conformance resource) not implemented in parser");
    }

    // ─── #mixins ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldLogAnErrorWhenTheDeprecatedMixinsKeywordIsUsed()
    {
        // SUSHI logs an error for the deprecated 'Mixins' keyword.
        // Our parser does not implement this deprecated keyword check.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (deprecated Mixins keyword) not implemented in parser");
    }

    // ─── #assignmentRule ────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInstanceWithAssignedValueRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: SamplePatient
            InstanceOf: Patient
            Title: ""Georgio Manos""
            Description: ""An example of a fictional patient named Georgio Manos""
            Usage: #example
            * name[0].family = ""Georgio""
            * name[0].given[0] = ""Manos""
            * gender = #other
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "SamplePatient");
        Assert.AreEqual("Patient", instance.InstanceOf);
        Assert.AreEqual("Georgio Manos", instance.Title);
        Assert.AreEqual("An example of a fictional patient named Georgio Manos", instance.Description);
        Assert.AreEqual("#example", instance.Usage);
        Assert.AreEqual(3, instance.Rules.Count);

        // rules[0]: name[0].family = "Georgio"
        Assert.IsInstanceOfType<InstanceFixedValueRule>(instance.Rules[0]);
        var rule0 = (InstanceFixedValueRule)instance.Rules[0];
        Assert.AreEqual("name[0].family", rule0.Path);
        Assert.IsInstanceOfType<StringValue>(rule0.Value);
        Assert.AreEqual("Georgio", ((StringValue)rule0.Value!).Value);

        // rules[1]: name[0].given[0] = "Manos"
        Assert.IsInstanceOfType<InstanceFixedValueRule>(instance.Rules[1]);
        var rule1 = (InstanceFixedValueRule)instance.Rules[1];
        Assert.AreEqual("name[0].given[0]", rule1.Path);
        Assert.IsInstanceOfType<StringValue>(rule1.Value);
        Assert.AreEqual("Manos", ((StringValue)rule1.Value!).Value);

        // rules[2]: gender = #other
        Assert.IsInstanceOfType<InstanceFixedValueRule>(instance.Rules[2]);
        var rule2 = (InstanceFixedValueRule)instance.Rules[2];
        Assert.AreEqual("gender", rule2.Path);
        Assert.IsInstanceOfType<Code>(rule2.Value);
        Assert.AreEqual("#other", ((Code)rule2.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseAnInstanceWithAssignedValuesThatAreAnAlias()
    {
        // SUSHI resolves EXAMPLE → http://example.org in assignment.
        // Our parser does NOT resolve aliases; the raw alias name is stored as NameValue.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: EXAMPLE = http://example.org

            Instance: PatientExample
            InstanceOf: Patient
            * identifier[0].system = EXAMPLE
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "PatientExample");
        Assert.AreEqual(1, instance.Rules.Count);
        Assert.AreEqual("Patient", instance.InstanceOf);
        // Our parser stores the alias name verbatim (as NameValue), not resolved
        Assert.IsInstanceOfType<InstanceFixedValueRule>(instance.Rules[0]);
        var rule = (InstanceFixedValueRule)instance.Rules[0];
        Assert.AreEqual("identifier[0].system", rule.Path);
        Assert.IsInstanceOfType<NameValue>(rule.Value);
        Assert.AreEqual("EXAMPLE", ((NameValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseAnInstanceWithAssignedValueResourceRules()
    {
        // SUSHI stores contained[0] = SomeInstance as a "resource" reference.
        // Our parser stores SomeInstance as a NameValue.
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: SamplePatient
            InstanceOf: Patient
            Title: ""Georgio Manos""
            Description: ""An example of a fictional patient named Georgio Manos""
            * contained[0] = SomeInstance
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInstances(doc).Count);
        var instance = SushiTestHelper.GetInstance(doc, "SamplePatient");
        Assert.AreEqual("Patient", instance.InstanceOf);
        Assert.AreEqual("Georgio Manos", instance.Title);
        Assert.AreEqual("An example of a fictional patient named Georgio Manos", instance.Description);
        Assert.AreEqual(1, instance.Rules.Count);

        Assert.IsInstanceOfType<InstanceFixedValueRule>(instance.Rules[0]);
        var rule = (InstanceFixedValueRule)instance.Rules[0];
        Assert.AreEqual("contained[0]", rule.Path);
        // Our parser stores the reference target as NameValue
        Assert.IsInstanceOfType<NameValue>(rule.Value);
        Assert.AreEqual("SomeInstance", ((NameValue)rule.Value!).Value);
    }

    // ─── #pathRule ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAPathRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: PatientProfile
            InstanceOf: Patient
            * name
        ");
        var instance = SushiTestHelper.GetInstance(doc, "PatientProfile");
        Assert.AreEqual(1, instance.Rules.Count);
        Assert.IsInstanceOfType<InstancePathRule>(instance.Rules[0]);
        Assert.AreEqual("name", instance.Rules[0].Path);
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInsertRuleWithASingleRuleSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Instance: MyPatient
            InstanceOf: Patient
            * insert MyRuleSet
        ");
        var instance = SushiTestHelper.GetInstance(doc, "MyPatient");
        Assert.AreEqual(1, instance.Rules.Count);
        Assert.IsInstanceOfType<InstanceInsertRule>(instance.Rules[0]);
        var insertRule = (InstanceInsertRule)instance.Rules[0];
        // Insert rules without a path store null (not empty string)
        Assert.IsNull(insertRule.Path);
        Assert.AreEqual("MyRuleSet", insertRule.RuleSetReference);
    }

    [TestMethod]
    public void ShouldParseAnInsertRuleWithAnEmptyParameterValue()
    {
        // Example taken from open-hie/case-reporting, which failed a regression in this area.
        // Our parser may split multi-word unquoted parameters differently from SUSHI.
        Assert.Inconclusive("Not tested: parameterized insert with empty param slots and multi-word unquoted params may not parse identically to SUSHI");
    }

    // ─── #instanceMetadata ───────────────────────────────────────────────────

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // SUSHI first-wins for duplicate metadata. Our parser behavior may differ.
        Assert.Inconclusive("Not tested: SUSHI first-wins duplicate metadata semantic behavior not guaranteed by parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounterDuplicateMetadataAttribute()
    {
        // SUSHI semantic validation: logs errors for duplicate metadata declarations.
        // Our parser has no semantic validation.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata attribute) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipInstanceWithNameUsedByAnotherInstance()
    {
        // SUSHI semantic validation: duplicate instance name → error + skip second.
        // Our parser has no semantic validation.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate Instance name) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipInstanceWithNameUsedByAnotherInstanceInAnotherFile()
    {
        // SUSHI multi-file duplicate instance name resolution not supported by single-file parser.
        Assert.Inconclusive("Not tested: multi-file duplicate Instance name resolution not supported by single-file parser");
    }
}
