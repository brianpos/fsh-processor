// Ported from SUSHI test: FSHImporter.Extension.test.ts

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class ExtensionTests
{
    // ─── #sdMetadata ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleExtension()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        ");
        Assert.AreEqual(1, SushiTestHelper.GetExtensions(doc).Count);
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual("SomeExtension", ext.Name);
        // P-EX1/P-EX2: SUSHI defaults Parent to "Extension" and Id to the entity Name.
        Assert.AreEqual("Extension", ext.Parent, "Parent should default to 'Extension'");
        Assert.AreEqual("SomeExtension", ext.Id, "Id should default to entity name");
    }

    [TestMethod]
    public void ShouldParseExtensionWithAdditionalMetadataProperties()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        Parent: ParentExtension
        Id: some-extension
        Title: ""Some Extension""
        Description: ""An extension on something""
        Context: ""some.fhirpath()""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetExtensions(doc).Count);
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual("SomeExtension", ext.Name);
        Assert.AreEqual("ParentExtension", ext.Parent);
        Assert.AreEqual("some-extension", ext.Id);
        Assert.AreEqual("Some Extension", ext.Title);
        Assert.AreEqual("An extension on something", ext.Description);
        Assert.AreEqual(1, ext.Contexts.Count);
        Assert.AreEqual("some.fhirpath()", ext.Contexts[0].Value);
        Assert.IsTrue(ext.Contexts[0].IsQuoted);
    }

    [TestMethod]
    public void ShouldParseNumericExtensionNameParentAndId()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: 123
        Parent: 456
        Id: 789
        ");
        Assert.AreEqual(1, SushiTestHelper.GetExtensions(doc).Count);
        var ext = SushiTestHelper.GetExtension(doc, "123");
        Assert.AreEqual("123", ext.Name);
        Assert.AreEqual("456", ext.Parent);
        Assert.AreEqual("789", ext.Id);
    }

    [TestMethod]
    public void ShouldParseExtensionWithMultipleContexts()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        Parent: ParentExtension
        Id: some-extension
        Context: ""some.fhirpath()"", Observation.component, http://example.org/MyPatient#identifier,
                 ""another.fhirpath(var, 0)""
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(4, ext.Contexts.Count);
        Assert.AreEqual("some.fhirpath()", ext.Contexts[0].Value);
        Assert.IsTrue(ext.Contexts[0].IsQuoted);
        Assert.AreEqual("Observation.component", ext.Contexts[1].Value);
        Assert.IsFalse(ext.Contexts[1].IsQuoted);
        Assert.AreEqual("http://example.org/MyPatient#identifier", ext.Contexts[2].Value);
        Assert.IsFalse(ext.Contexts[2].IsQuoted);
        Assert.AreEqual("another.fhirpath(var, 0)", ext.Contexts[3].Value);
        Assert.IsTrue(ext.Contexts[3].IsQuoted);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // X3: first-wins semantics — matches SUSHI behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        Id: first-id
        Id: second-id
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        // First declaration wins.
        Assert.AreEqual("first-id", ext.Id);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringADuplicateMetadataAttribute()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipExtensionWithDuplicateName()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate extension name) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipExtensionWithDuplicateNameAcrossFiles()
    {
        Assert.Inconclusive("Not tested: multi-file parsing not supported by single-file parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenDeprecatedMixinsKeywordIsUsed()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (Mixins keyword) not implemented");
    }

    // ─── #cardRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseSimpleCardRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension 0..0
        * value[x] 1..1
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(2, ext.Rules.Count);
        SushiTestHelper.AssertCardRule(ext.Rules[0], "extension", "0..0");
        SushiTestHelper.AssertCardRule(ext.Rules[1], "value[x]", "1..1");
    }

    [TestMethod]
    public void ShouldParseCardRulesWithFlags()
    {
        // X4: SUSHI splits "* value[x] 1..1 MS N" into a CardRule and a separate FlagRule (3 rules total).
        // fsh-processor now matches this behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension 0..0
        * value[x] 1..1 MS N
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(3, ext.Rules.Count);
        SushiTestHelper.AssertCardRule(ext.Rules[0], "extension", "0..0");
        SushiTestHelper.AssertCardRule(ext.Rules[1], "value[x]", "1..1");
        SushiTestHelper.AssertFlagRule(ext.Rules[2], "value[x]", "MS", "N");
    }

    // ─── #flagRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseSinglePathSingleValueFlagRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension MS
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        SushiTestHelper.AssertFlagRule(ext.Rules[0], "extension", "MS");
    }

    // ─── #BindingRule ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseValueSetRulesWithNamesAndStrength()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        Parent: ParentExtension
        * valueCodeableConcept from ExtensionValueSet (extensible)
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        SushiTestHelper.AssertBindingRule(ext.Rules[0], "valueCodeableConcept", "ExtensionValueSet", "extensible");
    }

    // ─── #assignmentRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAssignedValueBooleanRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * value[x] = true
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(ext.Rules[0], "value[x]");
        Assert.IsInstanceOfType<BooleanValue>(rule.Value);
        Assert.IsTrue(((BooleanValue)rule.Value!).Value);
        Assert.IsFalse(rule.Exactly);
    }

    [TestMethod]
    public void ShouldParseAssignedValueBooleanRuleWithExactlyModifier()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * value[x] = false (exactly)
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(ext.Rules[0], "value[x]");
        Assert.IsInstanceOfType<BooleanValue>(rule.Value);
        Assert.IsFalse(((BooleanValue)rule.Value!).Value);
        Assert.IsTrue(rule.Exactly);
    }

    // ─── #onlyRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnOnlyRuleWithOneType()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * value[x] only Quantity
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        SushiTestHelper.AssertOnlyRule(ext.Rules[0], "value[x]", "Quantity");
    }

    // ─── #containsRule ───────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseContainsRuleWithOneItem()
    {
        // SUSHI splits "* extension contains foo 1..1" into a ContainsRule + a CardRule (3 rules total).
        // fsh-processor emits only the ContainsRule with cardinality embedded (2 rules total).
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension 0..*
        * extension contains foo 1..1
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(2, ext.Rules.Count);
        var rule = SushiTestHelper.AssertContainsRule(ext.Rules[1], "extension", "foo");
        Assert.AreEqual("1..1", rule.Items[0].Cardinality);
    }

    [TestMethod]
    public void ShouldParseContainsRuleWithReservedWordCode()
    {
        // SUSHI splits "* extension contains code 1..1" into a ContainsRule + a CardRule (3 rules total).
        // fsh-processor emits only the ContainsRule with cardinality embedded (2 rules total).
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension 0..*
        * extension contains code 1..1
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(2, ext.Rules.Count);
        var rule = SushiTestHelper.AssertContainsRule(ext.Rules[1], "extension", "code");
        Assert.AreEqual("1..1", rule.Items[0].Cardinality);
    }

    [TestMethod]
    public void ShouldParseContainsRuleWithItemDeclaringAType()
    {
        // SUSHI splits "* extension contains MaxSizeExtension named max 1..1" into ContainsRule + CardRule.
        // fsh-processor emits only the ContainsRule with cardinality embedded (2 rules total).
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension 0..*
        * extension contains MaxSizeExtension named max 1..1
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(2, ext.Rules.Count);
        var rule = SushiTestHelper.AssertContainsRule(ext.Rules[1], "extension", "MaxSizeExtension");
        Assert.AreEqual("max", rule.Items[0].NamedAlias);
        Assert.AreEqual("1..1", rule.Items[0].Cardinality);
    }

    // ─── #caretValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseACaretValueRuleWithAPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * id ^short = ""foo""
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        var rule = SushiTestHelper.AssertCaretValueRule(ext.Rules[0], "id", "short");
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("foo", ((StringValue)rule.Value!).Value);
    }

    // ─── #obeysRule ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnObeysRuleWithAPathAndMultipleInvariants()
    {
        // X5: SUSHI splits "* extension obeys inv-1 and inv-2" into two separate ObeysRules.
        // fsh-processor now matches this behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension obeys inv-1 and inv-2
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(2, ext.Rules.Count);
        SushiTestHelper.AssertObeysRule(ext.Rules[0], "extension", "inv-1");
        SushiTestHelper.AssertObeysRule(ext.Rules[1], "extension", "inv-2");
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInsertRuleWithASingleRuleSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: MyExtension
        * insert MyRuleSet
        ");
        var ext = SushiTestHelper.GetExtension(doc, "MyExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        SushiTestHelper.AssertInsertRule(ext.Rules[0], "", "MyRuleSet");
    }
}
