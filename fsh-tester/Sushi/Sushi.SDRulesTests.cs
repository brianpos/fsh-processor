// Ported from SUSHI test: FSHImporter.SDRules.test.ts
//
// Tests for Structure Definition rules (CardRule, FlagRule, OnlyRule, ValueSetRule,
// FixedValueRule/AssignmentRule, ContainsRule, ObeysRule, CaretValueRule, InsertRule, PathRule)
// as applied within Profile and Extension entities.
//
// Key differences vs SUSHI:
//  - SUSHI splits combined cardinality+flags into a CardRule + FlagRule (parser does not yet).
//  - SUSHI splits multi-invariant obeys into separate ObeysRules (parser does not yet).
//  - SUSHI splits contains+cardinality into ContainsRule + CardRule (parser does not yet).
//  - fsh-processor stores CaretPath with "^" prefix; SUSHI strips it (normalized in SushiTestHelper).
//  - fsh-processor stores Strength with "()" wrapping; SUSHI strips them (normalized in SushiTestHelper).
//  - fsh-processor retains "#" prefix on code values in FixedValueRule.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class SDRulesTests
{
    // ─── #cardRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseSimpleCardRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
    }

    [TestMethod]
    public void ShouldParseCardRuleWithZeroMax()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * bodySite 0..0
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertCardRule(profile.Rules[0], "bodySite", "0..0");
    }

    [TestMethod]
    public void ShouldParseCardRuleWithUnlimitedMax()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component 0..*
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertCardRule(profile.Rules[0], "component", "0..*");
    }

    [TestMethod]
    public void ShouldParseCardRuleWithCombinedFlags()
    {
        // SUSHI splits "* status 1..1 MS" into a CardRule + a separate FlagRule.
        // fsh-processor combines cardinality and flags into a single CardRule.
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1 MS
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
        CollectionAssert.Contains(rule.Flags, "MS");
    }

    [TestMethod]
    public void ShouldParseMultipleCardRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status 1..1
            * code 1..1
            * subject 1..1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(3, profile.Rules.Count);
        SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
        SushiTestHelper.AssertCardRule(profile.Rules[1], "code", "1..1");
        SushiTestHelper.AssertCardRule(profile.Rules[2], "subject", "1..1");
    }

    // ─── #flagRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseSinglePathFlagRule()
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

    [TestMethod]
    public void ShouldParseMustSupportFlag()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status MS
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        var rule = (FlagRule)profile.Rules[0];
        CollectionAssert.Contains(rule.Flags, "MS");
    }

    [TestMethod]
    public void ShouldParseNarrativeFlag()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * text N
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        SushiTestHelper.AssertFlagRule(profile.Rules[0], "text", "N");
    }

    [TestMethod]
    public void ShouldParseMultipleFlagPaths()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status and code MS
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = (FlagRule)profile.Rules[0];
        Assert.AreEqual("status", rule.Path);
        CollectionAssert.Contains(rule.AdditionalPaths, "code");
        CollectionAssert.Contains(rule.Flags, "MS");
    }

    // ─── #valueSetRule ───────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseValueSetRuleWithStrength()
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
    public void ShouldParseValueSetRuleWithAllStrengths()
    {
        foreach (var (fsh, strength) in new[]
        {
            ("required", "required"),
            ("extensible", "extensible"),
            ("preferred", "preferred"),
            ("example", "example"),
        })
        {
            var doc = SushiTestHelper.ParseDoc($@"
                Profile: MyObservation
                Parent: Observation
                * status from MyVS ({fsh})
            ");
            var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
            SushiTestHelper.AssertBindingRule(profile.Rules[0], "status", "MyVS", strength);
        }
    }

    [TestMethod]
    public void ShouldParseValueSetRuleWithoutStrength()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status from MyValueSet
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        SushiTestHelper.AssertBindingRule(profile.Rules[0], "status", "MyValueSet");
    }

    // ─── #assignmentRule / fixedValueRule ────────────────────────────────────

    [TestMethod]
    public void ShouldParseAssignedValueStringRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * category.text = ""Vital Signs""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
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
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "category.text");
        Assert.IsTrue(rule.Exactly);
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
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "status");
        Assert.IsInstanceOfType<Code>(rule.Value);
        // fsh-processor retains the "#" prefix on code values.
        Assert.AreEqual("#final", ((Code)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseAssignedValueNumberRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * referenceRange.low.value = 3.5
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "referenceRange.low.value");
        Assert.IsInstanceOfType<NumberValue>(rule.Value);
        Assert.AreEqual(3.5m, ((NumberValue)rule.Value!).Value);
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
    public void ShouldParseAssignedValueReferenceRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * subject = Reference(MyPatient)
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        var rule = SushiTestHelper.AssertFixedValueRule(profile.Rules[0], "subject");
        Assert.IsInstanceOfType<Reference>(rule.Value);
        Assert.AreEqual("MyPatient", ((Reference)rule.Value!).Type);
    }

    // ─── #onlyRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseOnlyRuleWithSingleType()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] only Quantity
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        SushiTestHelper.AssertOnlyRule(profile.Rules[0], "value[x]", "Quantity");
    }

    [TestMethod]
    public void ShouldParseOnlyRuleWithMultipleTypes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] only Quantity or string
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        SushiTestHelper.AssertOnlyRule(profile.Rules[0], "value[x]", "Quantity", "string");
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
    public void ShouldParseCaretValueRuleWithPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^short = ""Status code""
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertCaretValueRule(profile.Rules[0], "status", "short");
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("Status code", ((StringValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseCaretValueRuleWithoutPath()
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

    [TestMethod]
    public void ShouldParseCaretValueRuleWithIntegerValue()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * status ^min = 0
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertCaretValueRule(profile.Rules[0], "status", "min");
        Assert.IsInstanceOfType<NumberValue>(rule.Value);
        Assert.AreEqual(0m, ((NumberValue)rule.Value!).Value);
    }

    // ─── #obeysRule ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseObeysRuleWithPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * value[x] obeys obs-1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        SushiTestHelper.AssertObeysRule(profile.Rules[0], "value[x]", "obs-1");
    }

    [TestMethod]
    public void ShouldParseObeysRuleWithoutPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * obeys obs-1
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        SushiTestHelper.AssertObeysRule(profile.Rules[0], "", "obs-1");
    }

    [TestMethod]
    public void ShouldParseObeysRuleWithMultipleInvariants()
    {
        // SUSHI splits "* obeys obs-1 and obs-2" into two separate ObeysRules.
        // fsh-processor keeps both invariants in a single ObeysRule.
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * obeys obs-1 and obs-2
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = SushiTestHelper.AssertObeysRule(profile.Rules[0], "", "obs-1", "obs-2");
        Assert.AreEqual(2, rule.InvariantNames.Count);
    }

    // ─── #pathRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParsePathRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * component
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        SushiTestHelper.AssertPathRule(profile.Rules[0], "component");
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseInsertRuleWithSingleRuleSet()
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

    [TestMethod]
    public void ShouldParseInsertRuleWithParameterizedRuleSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyObservation
            Parent: Observation
            * insert MyRuleSet(param1, param2)
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyObservation");
        Assert.AreEqual(1, profile.Rules.Count);
        var rule = (InsertRule)profile.Rules[0];
        Assert.AreEqual("MyRuleSet", rule.RuleSetReference);
        Assert.IsTrue(rule.IsParameterized);
        Assert.AreEqual(2, rule.Parameters.Count);
    }

    // ─── Mixed rules ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseMixedSDRulesInProfile()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: MyBP
            Parent: Observation
            * status 1..1
            * code 1..1
            * subject 1..1
            * value[x] only Quantity
            * valueQuantity.system = ""http://unitsofmeasure.org""
            * ^publisher = ""HL7""
            * insert CommonRules
        ");
        var profile = SushiTestHelper.GetProfile(doc, "MyBP");
        Assert.AreEqual(7, profile.Rules.Count);
        SushiTestHelper.AssertCardRule(profile.Rules[0], "status", "1..1");
        SushiTestHelper.AssertCardRule(profile.Rules[1], "code", "1..1");
        SushiTestHelper.AssertCardRule(profile.Rules[2], "subject", "1..1");
        SushiTestHelper.AssertOnlyRule(profile.Rules[3], "value[x]", "Quantity");
        SushiTestHelper.AssertFixedValueRule(profile.Rules[4], "valueQuantity.system");
        SushiTestHelper.AssertCaretValueRule(profile.Rules[5], "", "publisher");
        SushiTestHelper.AssertInsertRule(profile.Rules[6], "", "CommonRules");
    }
}
