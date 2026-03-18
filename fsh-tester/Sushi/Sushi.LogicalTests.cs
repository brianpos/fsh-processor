// Ported from SUSHI test: FSHImporter.Logical.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI defaults Id to the entity name when not specified; fsh-processor does not.
//  - Both SUSHI and fsh-processor use first-wins for duplicate metadata (X3).
//  - fsh-processor stores CaretPath with "^" prefix; SUSHI strips it (normalized in SushiTestHelper).
//  - Logical uses string? properties (not Metadata?) for Parent, Id, Title, Description.
//  - Logical.Characteristics stores the raw characteristic codes as strings.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class LogicalTests
{
    // ─── #sdMetadata ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleLogicalModel()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
        ");
        Assert.AreEqual(1, SushiTestHelper.GetLogicals(doc).Count);
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual("MyModel", logical.Name);
        Assert.AreEqual(0, logical.Rules.Count);
    }

    [TestMethod]
    public void ShouldParseLogicalModelWithAllMetadataFields()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            Parent: Element
            Id: my-model
            Title: ""My Logical Model""
            Description: ""A logical model for testing""
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual("MyModel", logical.Name);
        Assert.AreEqual("Element", logical.Parent);
        Assert.AreEqual("my-model", logical.Id);
        Assert.AreEqual("My Logical Model", logical.Title);
        Assert.AreEqual("A logical model for testing", logical.Description);
    }

    [TestMethod]
    public void ShouldParseNumericLogicalModelNameParentAndId()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: 123
            Parent: 456
            Id: 789
        ");
        var logical = SushiTestHelper.GetLogical(doc, "123");
        Assert.AreEqual("123", logical.Name);
        Assert.AreEqual("456", logical.Parent);
        Assert.AreEqual("789", logical.Id);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // X3: first-wins semantics — matches SUSHI behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            Id: first-id
            Id: second-id
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        // X3: first-wins — the first declaration is kept.
        Assert.AreEqual("first-id", logical.Id);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipLogicalWithDuplicateName()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate logical model name) not implemented");
    }

    // ─── #characteristics ────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseLogicalWithSingleCharacteristic()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            Characteristics: #can-be-target
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual(1, logical.Characteristics.Count);
        // fsh-processor retains the "#" prefix on characteristic codes.
        Assert.AreEqual("#can-be-target", logical.Characteristics[0]);
    }

    [TestMethod]
    public void ShouldParseLogicalWithMultipleCharacteristics()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            Characteristics: #can-be-target, #has-range
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual(2, logical.Characteristics.Count);
        // fsh-processor retains the "#" prefix on characteristic codes.
        Assert.AreEqual("#can-be-target", logical.Characteristics[0]);
        Assert.AreEqual("#has-range", logical.Characteristics[1]);
    }

    // ─── #addElementRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAddElementRuleWithTypeAndDescription()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            * field1 0..1 string ""A string field""
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual(1, logical.Rules.Count);
        var rule = logical.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("field1", rule.Path);
        Assert.AreEqual("0..1", rule.Cardinality);
        Assert.AreEqual(1, rule.TargetTypes.Count);
        Assert.AreEqual("string", rule.TargetTypes[0]);
        Assert.AreEqual("A string field", rule.ShortDescription);
    }

    [TestMethod]
    public void ShouldParseAddElementRuleWithMultipleTypes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            * field1 0..* Quantity or string ""A multi-type field""
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual(1, logical.Rules.Count);
        var rule = logical.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("field1", rule.Path);
        Assert.AreEqual("0..*", rule.Cardinality);
        Assert.AreEqual(2, rule.TargetTypes.Count);
        Assert.AreEqual("Quantity", rule.TargetTypes[0]);
        Assert.AreEqual("string", rule.TargetTypes[1]);
    }

    [TestMethod]
    public void ShouldParseAddElementRuleWithShortAndDefinition()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            * note 0..* string ""Short desc"" ""A longer definition""
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual(1, logical.Rules.Count);
        var rule = logical.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("Short desc", rule.ShortDescription);
        Assert.AreEqual("A longer definition", rule.Definition);
    }

    [TestMethod]
    public void ShouldParseMultipleAddElementRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            Parent: Element
            Id: my-model
            Title: ""My Logical Model""
            Description: ""Test""
            * field1 0..1 string ""Short description""
            * field2 1..* CodeableConcept ""Another field""
            * field3 0..1 Quantity or Range ""Multi-type field""
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        Assert.AreEqual(3, logical.Rules.Count);
        var rules = logical.Rules.OfType<AddElementRule>().ToList();
        Assert.AreEqual(3, rules.Count, "All rules should be AddElementRules");
        Assert.AreEqual("field1", rules[0].Path);
        Assert.AreEqual("0..1", rules[0].Cardinality);
        Assert.AreEqual("field2", rules[1].Path);
        Assert.AreEqual("1..*", rules[1].Cardinality);
        Assert.AreEqual("field3", rules[2].Path);
        Assert.AreEqual(2, rules[2].TargetTypes.Count);
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseInsertRuleOnLogical()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: TestRS
            * field1 MS

            Logical: MyModel
            * field1 0..1 string ""Short""
            * insert TestRS
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        // The insert rule should be present after the add-element rule.
        var insertRule = logical.Rules.OfType<InsertRule>().FirstOrDefault();
        Assert.IsNotNull(insertRule, "Expected an InsertRule on Logical");
        Assert.AreEqual("TestRS", insertRule.RuleSetReference);
    }

    // ─── #caretValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseCaretValueRuleOnLogical()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Logical: MyModel
            * field1 0..1 string ""Short""
            * ^status = #active
        ");
        var logical = SushiTestHelper.GetLogical(doc, "MyModel");
        var caretRule = logical.Rules.OfType<CaretValueRule>().FirstOrDefault();
        Assert.IsNotNull(caretRule, "Expected a CaretValueRule on Logical");
        Assert.AreEqual("^status", caretRule.CaretPath);
    }
}
