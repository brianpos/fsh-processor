// Ported from SUSHI test: FSHImporter.ValueSet.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI defaults Id to the entity name when not specified; fsh-processor does not.
//  - SUSHI uses first-wins for duplicate metadata; fsh-processor uses last-wins.
//  - fsh-processor stores CaretPath with "^" prefix; SUSHI strips it (normalized in SushiTestHelper).
//  - fsh-processor stores Strength with "()" wrapping; SUSHI strips them (normalized in SushiTestHelper).
//  - ConceptCode.Value in VsComponentRule retains the "#" prefix (e.g. "#lion"), matching parser output.
//  - VsCaretValueRule.CaretPath retains the "^" prefix.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class ValueSetTests
{
    // ─── #vsMetadata ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleValueSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
        ");
        Assert.AreEqual(1, SushiTestHelper.GetValueSets(doc).Count);
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual("MyVS", vs.Name);
        Assert.AreEqual(0, vs.Rules.Count);
    }

    [TestMethod]
    public void ShouldParseValueSetWithAllMetadataFields()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            Id: my-vs
            Title: ""My Value Set""
            Description: ""A value set for testing""
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual("MyVS", vs.Name);
        Assert.AreEqual("my-vs", vs.Id);
        Assert.AreEqual("My Value Set", vs.Title);
        Assert.AreEqual("A value set for testing", vs.Description);
    }

    [TestMethod]
    public void ShouldParseNumericValueSetNameAndId()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: 123
            Id: 456
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "123");
        Assert.AreEqual("123", vs.Name);
        Assert.AreEqual("456", vs.Id);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // SUSHI uses first-wins semantics for duplicate metadata; fsh-processor uses last-wins.
        var doc = SushiTestHelper.ParseDoc(@"
        ValueSet: MyVS
        Id: first-id
        Id: second-id
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        // fsh-processor last-wins: the second declaration overwrites the first.
        Assert.AreEqual("second-id", vs.Id);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipValueSetWithDuplicateName()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate value set name) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipValueSetWithDuplicateNameAcrossFiles()
    {
        Assert.Inconclusive("Not tested: multi-file parsing not supported by single-file parser");
    }

    // ─── #vsComponent – include/exclude concepts ─────────────────────────────

    [TestMethod]
    public void ShouldParseValueSetWithIncludeAllFromSystem()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * include codes from system http://loinc.org
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = (VsComponentRule)vs.Rules[0];
        Assert.IsTrue(rule.IsInclude ?? false, "Expected IsInclude=true");
        Assert.AreEqual("http://loinc.org", rule.FromSystem);
        Assert.AreEqual(0, rule.FromValueSets.Count);
        Assert.AreEqual(0, rule.Filters.Count);
    }

    [TestMethod]
    public void ShouldParseValueSetWithExcludeAllFromSystem()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * exclude codes from system http://loinc.org
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = (VsComponentRule)vs.Rules[0];
        Assert.IsFalse(rule.IsInclude ?? true, "Expected IsInclude=false");
        Assert.AreEqual("http://loinc.org", rule.FromSystem);
    }

    [TestMethod]
    public void ShouldParseValueSetWithIncludeFromValueSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * include codes from valueset OtherVS
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = (VsComponentRule)vs.Rules[0];
        Assert.IsTrue(rule.IsInclude ?? false, "Expected IsInclude=true");
        Assert.IsNull(rule.FromSystem);
        Assert.AreEqual(1, rule.FromValueSets.Count);
        Assert.AreEqual("OtherVS", rule.FromValueSets[0]);
    }

    [TestMethod]
    public void ShouldParseValueSetWithIncludeConceptFromSystem()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * http://loinc.org#1234-5
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = (VsComponentRule)vs.Rules[0];
        Assert.IsTrue(rule.IsConceptComponent, "Expected IsConceptComponent=true");
        Assert.IsNotNull(rule.ConceptCode);
    }

    [TestMethod]
    public void ShouldParseValueSetWithMultipleIncludesAndExcludes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * include codes from system http://loinc.org
            * exclude codes from system http://snomed.info/sct
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(2, vs.Rules.Count);
        var r0 = (VsComponentRule)vs.Rules[0];
        Assert.IsTrue(r0.IsInclude ?? false);
        Assert.AreEqual("http://loinc.org", r0.FromSystem);
        var r1 = (VsComponentRule)vs.Rules[1];
        Assert.IsFalse(r1.IsInclude ?? true);
        Assert.AreEqual("http://snomed.info/sct", r1.FromSystem);
    }

    // ─── #vsComponent – filter ───────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseValueSetWithFilter()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * codes from system http://snomed.info/sct where concept is-a #387207008
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = (VsComponentRule)vs.Rules[0];
        Assert.IsFalse(rule.IsConceptComponent, "Expected filter component");
        Assert.AreEqual("http://snomed.info/sct", rule.FromSystem);
        Assert.AreEqual(1, rule.Filters.Count);
        Assert.AreEqual("concept", rule.Filters[0].Property);
        Assert.AreEqual("is-a", rule.Filters[0].Operator);
    }

    // ─── #caretValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseValueSetCaretValueRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * ^copyright = ""Copyright info""
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = vs.Rules[0] as VsCaretValueRule;
        Assert.IsNotNull(rule, "Expected VsCaretValueRule");
        // fsh-processor retains the "^" prefix on CaretPath; SUSHI strips it.
        Assert.AreEqual("^copyright", rule.CaretPath);
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("Copyright info", ((StringValue)rule.Value!).Value);
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseInsertRuleOnValueSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: MyVS
            * insert CommonVSRules
        ");
        var vs = SushiTestHelper.GetValueSet(doc, "MyVS");
        Assert.AreEqual(1, vs.Rules.Count);
        var rule = vs.Rules[0] as VsInsertRule;
        Assert.IsNotNull(rule, "Expected VsInsertRule");
        Assert.AreEqual("CommonVSRules", rule.RuleSetReference);
    }

    // ─── Multiple value sets ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseMultipleValueSets()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            ValueSet: VS1
            Id: vs1

            ValueSet: VS2
            Id: vs2
        ");
        Assert.AreEqual(2, SushiTestHelper.GetValueSets(doc).Count);
        Assert.AreEqual("vs1", SushiTestHelper.GetValueSet(doc, "VS1").Id);
        Assert.AreEqual("vs2", SushiTestHelper.GetValueSet(doc, "VS2").Id);
    }
}
