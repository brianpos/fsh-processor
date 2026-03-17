// Ported from SUSHI test: FSHImporter.Profile.test.ts
//
// Key differences vs SUSHI:
//  - Profile metadata (Parent, Id, Title, Description) are Metadata? objects; use .Value to get the string.
//  - Both SUSHI and fsh-processor use first-wins for duplicate metadata (X3).
//  - Both SUSHI and fsh-processor split combined cardinality+flags into CardRule + FlagRule (X4).
//  - Both SUSHI and fsh-processor split multi-invariant obeys into separate ObeysRules (X5).
//  - fsh-processor stores CaretPath with "^" prefix; SUSHI strips it (normalized in SushiTestHelper).
//  - fsh-processor stores Strength with "()" wrapping; SUSHI strips them (normalized in SushiTestHelper).
//  - Columns in SourcePosition are 0-based (ANTLR); SUSHI uses 1-based.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class ProfileTests
{
    // ─── #sdMetadata ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleProfile()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyPatient
            Parent: Patient
        ");
        Assert.AreEqual(1, SushiTestHelper.GetProfiles(doc).Count);
        var profile = SushiTestHelper.GetProfile(doc, "MyPatient");
        Assert.AreEqual("MyPatient", profile.Name);
        Assert.AreEqual("Patient", profile.Parent?.Value);
        // P-PR1: SUSHI defaults Id to the entity Name when not specified.
        Assert.AreEqual("MyPatient", profile.Id?.Value, "Id should default to entity name");
    }

    [TestMethod]
    public void ShouldParseProfileWithAllMetadataFields()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyPatient
            Parent: Patient
            Id: my-patient
            Title: ""My Patient Profile""
            Description: ""A profile for testing""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyPatient");
        Assert.AreEqual("MyPatient", profile.Name);
        Assert.AreEqual("Patient", profile.Parent?.Value);
        Assert.AreEqual("my-patient", profile.Id?.Value);
        Assert.AreEqual("My Patient Profile", profile.Title?.Value);
        Assert.AreEqual("A profile for testing", profile.Description?.Value);
    }

    [TestMethod]
    public void ShouldParseNumericProfileNameAndParent()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: 123
            Parent: 456
            Id: 789
        ");
        var profile = SushiTestHelper.GetProfile(doc, "123");
        Assert.AreEqual("123", profile.Name);
        Assert.AreEqual("456", profile.Parent?.Value);
        Assert.AreEqual("789", profile.Id?.Value);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // X3: first-wins semantics — matches SUSHI behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            Id: first-id
            Id: second-id
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        // First declaration wins; the second is ignored.
        Assert.AreEqual("first-id", profile.Id?.Value);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipProfileWithDuplicateName()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate profile name) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipProfileWithDuplicateNameAcrossFiles()
    {
        Assert.Inconclusive("Not tested: multi-file parsing not supported by single-file parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenDeprecatedMixinsKeywordIsUsed()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (Mixins keyword) not implemented");
    }

    // ─── Multiple profiles ────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseMultipleProfiles()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyPatient
            Parent: Patient

            Profile: MyObservation
            Parent: Observation
        ");
        Assert.AreEqual(2, SushiTestHelper.GetProfiles(doc).Count);
        var p1 = SushiTestHelper.GetProfile(doc, "MyPatient");
        Assert.AreEqual("Patient", p1.Parent?.Value);
        var p2 = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual("Observation", p2.Parent?.Value);
    }

    // ─── #cardRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseSimpleCardRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1
            * code 1..1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(2, profile.Rules.Count);
        SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
        SushiTestHelper.AssertCardRule(profile.Rules[1], "code", "1..1");
    }

    [TestMethod]
    public void ShouldParseCardRulesWithFlags()
    {
        // Per the FSH spec and grammar (cardRule: STAR path CARD flag*), a combined cardinality
        // and flag rule is a single CardRule with both Cardinality and Flags populated.
        // The spec grammar does not require splitting; SUSHI's split is an internal design choice.
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1 MS
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var cardRule = SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
        CollectionAssert.AreEqual(new[] { "MS" }, cardRule.Flags.ToArray());
    }

    // ─── #flagRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseSingleFlagRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status MS
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertFlagRule(profile.Rules[0], "status", "MS");
    }

    [TestMethod]
    public void ShouldParseMultipleFlagsOnSinglePath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status MS SU
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertFlagRule(profile.Rules[0], "status", "MS", "SU");
    }

    // ─── #valueSetRule ───────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseValueSetBindingRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status from http://hl7.org/fhir/ValueSet/observation-status (required)
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertBindingRule(profile.Rules[0], "status",
            "http://hl7.org/fhir/ValueSet/observation-status", "required");
    }

    [TestMethod]
    public void ShouldParseValueSetBindingRuleWithoutStrength()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status from ObservationStatusVS
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertBindingRule(profile.Rules[0], "status", "ObservationStatusVS");
    }

    // ─── #fixedValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAssignedValueStringRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * category.text = ""Vital Signs""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "category.text");
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("Vital Signs", ((StringValue)rule.Value!).Value);
        Assert.IsFalse(rule.Exactly);
    }

    [TestMethod]
    public void ShouldParseAssignedValueStringRuleWithExactly()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * category.text = ""Vital Signs"" (exactly)
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "category.text");
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("Vital Signs", ((StringValue)rule.Value!).Value);
        Assert.IsTrue(rule.Exactly);
    }

    [TestMethod]
    public void ShouldParseAssignedValueBooleanRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component.valueBoolean = true
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "component.valueBoolean");
        Assert.IsInstanceOfType<BooleanValue>(rule.Value);
        Assert.IsTrue(((BooleanValue)rule.Value!).Value);
        Assert.IsFalse(rule.Exactly);
    }

    [TestMethod]
    public void ShouldParseAssignedValueCodeRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status = #final
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "status");
        Assert.IsInstanceOfType<Code>(rule.Value);
        // fsh-processor retains the "#" prefix on code values.
        Assert.AreEqual("#final", ((Code)rule.Value!).Value);
    }

    // ─── #onlyRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseOnlyRuleWithOneType()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] only Quantity
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertOnlyRule(profile.Rules[0], "value[x]", "Quantity");
    }

    [TestMethod]
    public void ShouldParseOnlyRuleWithMultipleTypes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] only Quantity or string or boolean
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertOnlyRule(profile.Rules[0], "value[x]", "Quantity", "string", "boolean");
    }

    // ─── #containsRule ───────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseContainsRuleWithOneItem()
    {
        // SUSHI splits "* component contains bpSystolic 1..1" into a ContainsRule + a CardRule.
        // fsh-processor combines them into a single ContainsRule with cardinality on the item.
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component contains bpSystolic 1..1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertContainsRule(profile.Rules[0], "component", "bpSystolic");
        Assert.AreEqual("1..1", rule.Items[0].Cardinality);
    }

    // ─── #caretValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseCaretValueRuleWithAPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^short = ""Status""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertCaretValueRule(profile.Rules[0], "status", "short");
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("Status", ((StringValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseCaretValueRuleWithoutAPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * ^publisher = ""HL7""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertCaretValueRule(profile.Rules[0], "", "publisher");
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("HL7", ((StringValue)rule.Value!).Value);
    }

    // ─── #obeysRule ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseObeysRuleWithAPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] obeys obs-1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertObeysRule(profile.Rules[0], "value[x]", "obs-1");
    }

    [TestMethod]
    public void ShouldParseObeysRuleWithoutAPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * obeys obs-1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertObeysRule(profile.Rules[0], "", "obs-1");
    }

    [TestMethod]
    public void ShouldParseObeysRuleWithMultipleInvariants()
    {
        // Per the FSH spec and grammar (obeysRule: STAR path? KW_OBEYS name (KW_AND name)*),
        // multiple invariants on one rule are stored in a single ObeysRule.InvariantNames list.
        // The spec text also describes this as one rule: "* obeys {Inv1} and {Inv2}..."
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * obeys obs-1 and obs-2
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertObeysRule(profile.Rules[0], "", "obs-1", "obs-2");
    }

    // ─── #pathRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParsePathRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component
            * component.code 1..1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(2, profile.Rules.Count);
        SushiTestHelper.AssertPathRule(profile.Rules[0], "component");
        SushiTestHelper.AssertCardRule(profile.Rules[1], "component.code", "1..1");
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseInsertRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * insert MyRuleSet
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertInsertRule(profile.Rules[0], "", "MyRuleSet");
    }

    [TestMethod]
    public void ShouldParseInsertRuleWithPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component insert MyRuleSet
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertInsertRule(profile.Rules[0], "component", "MyRuleSet");
    }

    // ─── Mixed rules ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseMixOfRulesInProfile()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1
            * code 1..1 MS
            * value[x] only Quantity
            * valueQuantity.system = ""http://unitsofmeasure.org""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.IsTrue(profile.Rules.Count >= 3, "Expected at least 3 rules");
        SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
    }
}
