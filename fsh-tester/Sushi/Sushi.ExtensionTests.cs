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
        Assert.AreEqual("Extension", ext.Parent);
        Assert.AreEqual("SomeExtension", ext.Id);
        Assert.AreEqual(2, ext.Position?.StartLine);
        Assert.AreEqual(8, ext.Position?.StartColumn);
        Assert.AreEqual(2, ext.Position?.EndLine);
        Assert.AreEqual(31, ext.Position?.EndColumn);
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
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        Parent: ParentExtension
        Id: some-extension
        Title: ""Some Extension""
        Description: ""An extension on something""
        Context: SomeElement
        Parent: DuplicateParentExtension
        Id: some-duplicate-extension
        Title: ""Some Duplicate Extension""
        Description: ""A duplicated extension on something""
        Context: SomeOtherElement
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual("ParentExtension", ext.Parent);
        Assert.AreEqual("some-extension", ext.Id);
        Assert.AreEqual("Some Extension", ext.Title);
        Assert.AreEqual("An extension on something", ext.Description);
        Assert.AreEqual(1, ext.Contexts.Count);
        Assert.AreEqual("SomeElement", ext.Contexts[0].Value);
        Assert.IsFalse(ext.Contexts[0].IsQuoted);
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
        * valueBoolean = true
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(ext.Rules[0], "valueBoolean");
        Assert.IsInstanceOfType<BooleanValue>(rule.Value);
        Assert.IsTrue(((BooleanValue)rule.Value!).Value);
        Assert.IsFalse(rule.Exactly);
    }

    [TestMethod]
    public void ShouldParseAssignedValueBooleanRuleWithExactlyModifier()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * valueBoolean = true (exactly)
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(1, ext.Rules.Count);
        var rule = SushiTestHelper.AssertFixedValueRule(ext.Rules[0], "valueBoolean");
        Assert.IsInstanceOfType<BooleanValue>(rule.Value);
        Assert.IsTrue(((BooleanValue)rule.Value!).Value);
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
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension contains foo 1..1
        * extension[foo].value[x] only Quantity
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(3, ext.Rules.Count);
        SushiTestHelper.AssertContainsRule(ext.Rules[0], "extension", "foo");
        SushiTestHelper.AssertCardRule(ext.Rules[1], "extension[foo]", "1..1");
        SushiTestHelper.AssertOnlyRule(ext.Rules[2], "extension[foo].value[x]", "Quantity");
    }

    [TestMethod]
    public void ShouldParseContainsRuleWithReservedWordCode()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Extension: SomeExtension
        * extension contains code 1..1
        * extension[code].value[x] only Quantity
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(3, ext.Rules.Count);
        SushiTestHelper.AssertContainsRule(ext.Rules[0], "extension", "code");
        SushiTestHelper.AssertCardRule(ext.Rules[1], "extension[code]", "1..1");
        SushiTestHelper.AssertOnlyRule(ext.Rules[2], "extension[code].value[x]", "Quantity");
    }

    [TestMethod]
    public void ShouldParseContainsRuleWithItemDeclaringAType()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        Alias: MaxSizeExtension = http://hl7.org/fhir/StructureDefinition/maxSize
        Extension: SomeExtension
        * extension contains MaxSizeExtension named max 1..1
        * extension[max].value[x] MS N
        ");
        var ext = SushiTestHelper.GetExtension(doc, "SomeExtension");
        Assert.AreEqual(3, ext.Rules.Count);
        var contains = SushiTestHelper.AssertContainsRule(ext.Rules[0], "extension", "max");
        Assert.AreEqual("MaxSizeExtension", contains.Items[0].NamedAlias);
        SushiTestHelper.AssertCardRule(ext.Rules[1], "extension[max]", "1..1");
        SushiTestHelper.AssertFlagRule(ext.Rules[2], "extension[max].value[x]", "MS", "N");
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
