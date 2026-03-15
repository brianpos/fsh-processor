// Ported from SUSHI test: FSHImporter.RuleSet.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.RuleSet.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI's assertAssignmentRule → FixedValueRule in a RuleSet (non-instance) context.
//  - SUSHI's assertConceptRule checks .code (stripping system); C# Concept.Codes stores the full code token (e.g. "ZOO#bear").
//  - SUSHI's assertCaretValueRule with code prefix ('['#lion']') → C# CodeCaretValueRule with Codes=["#lion"].
//  - AddElementRule: SUSHI sets both short and definition to the same value when only one STRING provided;
//    C# only sets ShortDescription; Definition remains null.
//  - SUSHI reports semantic errors for duplicate names; our parser has no semantic validation.
//  - Columns in SourcePosition are 0-based (ANTLR); SUSHI uses 1-based.
//  - SUSHI MappingRule: (comment → C# MappingMapRule.Language, language/FshCode → C# MappingMapRule.Code).

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class RuleSetTests
{
    [TestMethod]
    public void ShouldParseARuleSetWithARule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: OneRuleSet
            * active = true
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "OneRuleSet");
        Assert.AreEqual("OneRuleSet", ruleSet.Name);
        // Verify the single rule is a FixedValueRule assigning active = true.
        Assert.AreEqual(1, ruleSet.Rules.Count);
        Assert.IsInstanceOfType<FixedValueRule>(ruleSet.Rules[0]);
        var rule = (FixedValueRule)ruleSet.Rules[0];
        Assert.AreEqual("active", rule.Path);
        Assert.IsInstanceOfType<BooleanValue>(rule.Value);
        Assert.IsTrue(((BooleanValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseARuleSetWithANumericName()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: 123
            * active = true
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "123");
        Assert.AreEqual("123", ruleSet.Name);
    }

    [TestMethod]
    public void ShouldParseARuleSetWithMultipleRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: RuleRuleSet
            * gender from https://www.hl7.org/fhir/valueset-administrative-gender.html
            * active = true (exactly)
            * contact 1..1
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "RuleRuleSet");
        Assert.AreEqual("RuleRuleSet", ruleSet.Name);
        Assert.AreEqual(3, ruleSet.Rules.Count);

        // Strength not checked: our parser stores "" for binding rules without explicit strength
        SushiTestHelper.AssertBindingRule(ruleSet.Rules[0], "gender",
            "https://www.hl7.org/fhir/valueset-administrative-gender.html");

        Assert.IsInstanceOfType<FixedValueRule>(ruleSet.Rules[1]);
        var activeRule = (FixedValueRule)ruleSet.Rules[1];
        Assert.AreEqual("active", activeRule.Path);
        Assert.IsInstanceOfType<BooleanValue>(activeRule.Value);
        Assert.IsTrue(((BooleanValue)activeRule.Value!).Value);
        Assert.IsTrue(activeRule.Exactly);

        SushiTestHelper.AssertCardRule(ruleSet.Rules[2], "contact", "1..1");
    }

    [TestMethod]
    public void ShouldParseARuleSetWithAnInsertRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: RuleRuleSet
            * gender from https://www.hl7.org/fhir/valueset-administrative-gender.html
            * insert OtherRuleSet
            * contact 1..1
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "RuleRuleSet");
        Assert.AreEqual("RuleRuleSet", ruleSet.Name);
        Assert.AreEqual(3, ruleSet.Rules.Count);

        // Strength not checked: our parser stores "" for binding rules without explicit strength
        SushiTestHelper.AssertBindingRule(ruleSet.Rules[0], "gender",
            "https://www.hl7.org/fhir/valueset-administrative-gender.html");
        SushiTestHelper.AssertInsertRule(ruleSet.Rules[1], null!, "OtherRuleSet");
        SushiTestHelper.AssertCardRule(ruleSet.Rules[2], "contact", "1..1");
    }

    [TestMethod]
    public void ShouldParseARuleSetWithAnAddElementRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: RuleRuleSet
            * gender from https://www.hl7.org/fhir/valueset-administrative-gender.html
            * contact 1..1
            * newStuff 0..* string ""short for newStuff property""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "RuleRuleSet");
        Assert.AreEqual("RuleRuleSet", ruleSet.Name);
        Assert.AreEqual(3, ruleSet.Rules.Count);

        // Strength not checked: our parser stores "" for binding rules without explicit strength
        SushiTestHelper.AssertBindingRule(ruleSet.Rules[0], "gender",
            "https://www.hl7.org/fhir/valueset-administrative-gender.html");
        SushiTestHelper.AssertCardRule(ruleSet.Rules[1], "contact", "1..1");

        Assert.IsInstanceOfType<AddElementRule>(ruleSet.Rules[2]);
        var addEl = (AddElementRule)ruleSet.Rules[2];
        Assert.AreEqual("newStuff", addEl.Path);
        Assert.AreEqual("0..*", addEl.Cardinality);
        CollectionAssert.AreEqual(new[] { "string" }, addEl.TargetTypes.ToArray());
        Assert.AreEqual("short for newStuff property", addEl.ShortDescription);
        // Note: SUSHI sets both short and definition to the same value when only one STRING is provided;
        // C# only sets ShortDescription; Definition remains null.
    }

    [TestMethod]
    public void ShouldParseARuleSetWithAContentReferenceAddElementRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: RuleRuleSet
            * gender from https://www.hl7.org/fhir/valueset-administrative-gender.html
            * contact 1..1
            * newStuff 0..3 contentReference http://example.org/StructureDefinition/Stuff#Stuff.new ""short for newStuff property""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "RuleRuleSet");
        Assert.AreEqual("RuleRuleSet", ruleSet.Name);
        Assert.AreEqual(3, ruleSet.Rules.Count);

        // Strength not checked: our parser stores "" for binding rules without explicit strength
        SushiTestHelper.AssertBindingRule(ruleSet.Rules[0], "gender",
            "https://www.hl7.org/fhir/valueset-administrative-gender.html");
        SushiTestHelper.AssertCardRule(ruleSet.Rules[1], "contact", "1..1");

        Assert.IsInstanceOfType<AddCRElementRule>(ruleSet.Rules[2]);
        var addCrEl = (AddCRElementRule)ruleSet.Rules[2];
        Assert.AreEqual("newStuff", addCrEl.Path);
        Assert.AreEqual("0..3", addCrEl.Cardinality);
        Assert.AreEqual("http://example.org/StructureDefinition/Stuff#Stuff.new", addCrEl.ContentReference);
        Assert.AreEqual("short for newStuff property", addCrEl.ShortDescription);
    }

    [TestMethod]
    public void ShouldParseARuleSetWithAMappingRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: OneRuleSet
            * identifier.system -> ""Patient.identifier.system""
            * identifier.value -> ""Patient.identifier.value"" ""This is a comment"" #code
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "OneRuleSet");
        Assert.AreEqual("OneRuleSet", ruleSet.Name);
        Assert.AreEqual(2, ruleSet.Rules.Count);

        Assert.IsInstanceOfType<MappingMapRule>(ruleSet.Rules[0]);
        var mapRule0 = (MappingMapRule)ruleSet.Rules[0];
        Assert.AreEqual("identifier.system", mapRule0.Path);
        Assert.AreEqual("Patient.identifier.system", mapRule0.Target);
        Assert.IsNull(mapRule0.Language);
        Assert.IsNull(mapRule0.Code);

        Assert.IsInstanceOfType<MappingMapRule>(ruleSet.Rules[1]);
        var mapRule1 = (MappingMapRule)ruleSet.Rules[1];
        Assert.AreEqual("identifier.value", mapRule1.Path);
        Assert.AreEqual("Patient.identifier.value", mapRule1.Target);
        // SUSHI 'comment' → C# MappingMapRule.Language
        Assert.AreEqual("This is a comment", mapRule1.Language);
        // SUSHI 'language' FshCode → C# MappingMapRule.Code (raw CODE token)
        Assert.AreEqual("#code", mapRule1.Code);
    }

    [TestMethod]
    public void ShouldParseARuleSetWithRulesValueSetComponentsConceptRulesAndCaretValueRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: RuleRuleSet
            * gender from https://www.hl7.org/fhir/valueset-administrative-gender.html
            * #bear from system ZOO
            * #lion
            * #lion ^designation.value = ""Watch out for big cat!""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "RuleRuleSet");
        Assert.AreEqual("RuleRuleSet", ruleSet.Name);
        Assert.AreEqual(4, ruleSet.Rules.Count);

        // rules[0]: binding rule (strength not checked)
        SushiTestHelper.AssertBindingRule(ruleSet.Rules[0], "gender",
            "https://www.hl7.org/fhir/valueset-administrative-gender.html");

        // rules[1]: VS concept component (#bear from system ZOO)
        Assert.IsInstanceOfType<VsComponentRule>(ruleSet.Rules[1]);
        var vsComp = (VsComponentRule)ruleSet.Rules[1];
        Assert.IsTrue(vsComp.IsConceptComponent);
        Assert.AreEqual("ZOO", vsComp.FromSystem);
        Assert.IsNotNull(vsComp.ConceptCode);
        Assert.AreEqual("#bear", vsComp.ConceptCode!.Value);

        // rules[2]: Concept #lion
        Assert.IsInstanceOfType<Concept>(ruleSet.Rules[2]);
        var concept = (Concept)ruleSet.Rules[2];
        CollectionAssert.AreEqual(new[] { "#lion" }, concept.Codes.ToArray());
        Assert.IsNull(concept.Display);

        // rules[3]: CodeCaretValueRule #lion ^designation.value = "Watch out for big cat!"
        Assert.IsInstanceOfType<CodeCaretValueRule>(ruleSet.Rules[3]);
        var caretRule = (CodeCaretValueRule)ruleSet.Rules[3];
        CollectionAssert.AreEqual(new[] { "#lion" }, caretRule.Codes.ToArray());
        // Our parser stores the full caret path including the leading '^'
        Assert.AreEqual("^designation.value", caretRule.CaretPath);
        Assert.IsInstanceOfType<StringValue>(caretRule.Value);
        Assert.AreEqual("Watch out for big cat!", ((StringValue)caretRule.Value!).Value);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenParsingARuleSetWithNoRules()
    {
        // SUSHI logs a semantic error for an empty ruleset, but still adds it to the result.
        // Our parser requires at least one rule; an empty RuleSet fails to parse.
        Assert.Inconclusive("Not tested: empty RuleSet causes a parse error in our parser (grammar requires at least one rule)");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipTheRuleSetWhenEncounteredARuleSetWithANameUsedByAnotherRuleSet()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate RuleSet name) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipTheRuleSetWhenEncounteredAnRuleSetWithANameUsedByAnotherRuleSetInAnotherFile()
    {
        Assert.Inconclusive("Not tested: multi-file duplicate RuleSet name resolution not supported by single-file parser");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenConceptRuleHasOneCodeWithASystemNoDefinitionAndNoHierarchy()
    {
        // SUSHI says no error logged when a concept has a system and no definition/hierarchy.
        // C# Concept.Codes stores the full code token including system (e.g. "ZOO#bear").
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: VSRuleSet
            * ZOO#bear
            * ZOO#gator ""Alligator""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetRuleSets(doc).Count);
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "VSRuleSet");
        Assert.AreEqual(2, ruleSet.Rules.Count);

        // SUSHI assertConceptRule checks .code stripping system ('bear');
        // C# stores the full code token 'ZOO#bear'.
        Assert.IsInstanceOfType<Concept>(ruleSet.Rules[0]);
        var concept0 = (Concept)ruleSet.Rules[0];
        Assert.AreEqual(1, concept0.Codes.Count);
        Assert.AreEqual("ZOO#bear", concept0.Codes[0]);
        Assert.IsNull(concept0.Display);
        Assert.IsNull(concept0.Definition);

        Assert.IsInstanceOfType<Concept>(ruleSet.Rules[1]);
        var concept1 = (Concept)ruleSet.Rules[1];
        Assert.AreEqual(1, concept1.Codes.Count);
        Assert.AreEqual("ZOO#gator", concept1.Codes[0]);
        Assert.AreEqual("Alligator", concept1.Display);
        Assert.IsNull(concept1.Definition);
    }
}
