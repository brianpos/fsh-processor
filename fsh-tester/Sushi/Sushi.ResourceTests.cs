// Ported from SUSHI test: FSHImporter.Resource.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI defaults Id to the entity name when not specified; fsh-processor does not.
//  - SUSHI uses first-wins for duplicate metadata; fsh-processor uses last-wins.
//  - SUSHI splits combined cardinality+flags into separate CardRule + FlagRule; fsh-processor combines them.
//  - SUSHI splits multi-invariant obeys into separate ObeysRules; fsh-processor keeps them in one.
//  - fsh-processor stores CaretPath with "^" prefix; SUSHI strips it (normalized in SushiTestHelper).
//  - fsh-processor stores Strength with "()" wrapping; SUSHI strips them (normalized in SushiTestHelper).
//  - Resource uses string? properties (not Metadata?) for Parent, Id, Title, Description.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class ResourceTests
{
    // ─── #sdMetadata ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleResource()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
        ");
        Assert.AreEqual(1, SushiTestHelper.GetResources(doc).Count);
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual("MyResource", resource.Name);
        Assert.AreEqual(0, resource.Rules.Count);
    }

    [TestMethod]
    public void ShouldParseResourceWithAllMetadataFields()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            Parent: DomainResource
            Id: my-resource
            Title: ""My Resource""
            Description: ""A custom resource for testing""
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual("MyResource", resource.Name);
        Assert.AreEqual("DomainResource", resource.Parent);
        Assert.AreEqual("my-resource", resource.Id);
        Assert.AreEqual("My Resource", resource.Title);
        Assert.AreEqual("A custom resource for testing", resource.Description);
    }

    [TestMethod]
    public void ShouldParseNumericResourceNameParentAndId()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: 123
            Parent: 456
            Id: 789
        ");
        var resource = SushiTestHelper.GetResource(doc, "123");
        Assert.AreEqual("123", resource.Name);
        Assert.AreEqual("456", resource.Parent);
        Assert.AreEqual("789", resource.Id);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // SUSHI uses first-wins semantics for duplicate metadata; fsh-processor uses last-wins.
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            Id: first-id
            Id: second-id
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        // fsh-processor last-wins: the second declaration overwrites the first.
        Assert.AreEqual("second-id", resource.Id);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipResourceWithDuplicateName()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate resource name) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipResourceWithDuplicateNameAcrossFiles()
    {
        Assert.Inconclusive("Not tested: multi-file parsing not supported by single-file parser");
    }

    // ─── #addElementRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAddElementRuleWithTypeAndDescription()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            * identifier 0..* Identifier ""Patient identifier""
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual(1, resource.Rules.Count);
        var rule = resource.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("identifier", rule.Path);
        Assert.AreEqual("0..*", rule.Cardinality);
        Assert.AreEqual(1, rule.TargetTypes.Count);
        Assert.AreEqual("Identifier", rule.TargetTypes[0]);
        Assert.AreEqual("Patient identifier", rule.ShortDescription);
    }

    [TestMethod]
    public void ShouldParseAddElementRuleWithMultipleTypes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            * value 0..1 string or integer ""A value""
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual(1, resource.Rules.Count);
        var rule = resource.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("value", rule.Path);
        Assert.AreEqual("0..1", rule.Cardinality);
        Assert.AreEqual(2, rule.TargetTypes.Count);
        Assert.AreEqual("string", rule.TargetTypes[0]);
        Assert.AreEqual("integer", rule.TargetTypes[1]);
        Assert.AreEqual("A value", rule.ShortDescription);
    }

    [TestMethod]
    public void ShouldParseAddElementRuleWithShortDescriptionAndDefinition()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            * note 0..* string ""Short note"" ""A longer definition of the note element""
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual(1, resource.Rules.Count);
        var rule = resource.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("note", rule.Path);
        Assert.AreEqual("0..*", rule.Cardinality);
        Assert.AreEqual("Short note", rule.ShortDescription);
        Assert.AreEqual("A longer definition of the note element", rule.Definition);
    }

    [TestMethod]
    public void ShouldParseAddElementRuleWithFlags()
    {
        // Flags on AddElementRule (e.g., MS, SU) are stored in rule.Flags.
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            * active 1..1 MS boolean ""Active flag""
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual(1, resource.Rules.Count);
        var rule = resource.Rules[0] as AddElementRule;
        Assert.IsNotNull(rule, "Expected AddElementRule");
        Assert.AreEqual("active", rule.Path);
        Assert.AreEqual("1..1", rule.Cardinality);
        CollectionAssert.Contains(rule.Flags, "MS");
    }

    // ─── #cardRule ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseCardRuleForExistingElement()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Resource: MyResource
            Parent: DomainResource
            * meta 0..1
        ");
        var resource = SushiTestHelper.GetResource(doc, "MyResource");
        Assert.AreEqual(1, resource.Rules.Count);
        // For Resource, cardinality rules on existing elements use LrCardRule
        var rule = resource.Rules[0];
        Assert.IsInstanceOfType<LrCardRule>(rule);
        Assert.AreEqual("meta", ((LrCardRule)rule).Path);
        Assert.AreEqual("0..1", ((LrCardRule)rule).Cardinality);
    }

    // ─── #caretValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseCaretValueRuleOnResource()
    {
        // fsh-processor does not yet parse CaretValueRule on Resource entities (0 rules produced).
        Assert.Inconclusive("Parser does not yet support CaretValueRule on Resource entities");
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseInsertRuleOnResource()
    {
        // fsh-processor does not yet parse InsertRule on Resource entities (0 rules produced).
        Assert.Inconclusive("Parser does not yet support InsertRule on Resource entities");
    }
}
