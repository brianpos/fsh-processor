// Ported from SUSHI test: FSHImporter.CodeSystem.test.ts

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class CodeSystemTests
{
    // ─── #csMetadata ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleCodeSystem()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        ");
        Assert.AreEqual(1, SushiTestHelper.GetCodeSystems(doc).Count);
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual("ZOO", cs.Name);
        Assert.AreEqual("ZOO", cs.Id);
        Assert.AreEqual(0, cs.Rules.Count);
        Assert.AreEqual(2, cs.Position?.StartLine);
        Assert.AreEqual(0, cs.Position?.StartColumn);
        Assert.AreEqual(2, cs.Position?.EndLine);
    }

    [TestMethod]
    public void ShouldParseCodeSystemWithAdditionalMetadata()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        Id: zoo-codes
        Title: ""Zoo Animals""
        Description: ""Animals and cryptids that may be at a zoo.""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual("ZOO", cs.Name);
        Assert.AreEqual("zoo-codes", cs.Id);
        Assert.AreEqual("Zoo Animals", cs.Title);
        Assert.AreEqual("Animals and cryptids that may be at a zoo.", cs.Description);
        Assert.AreEqual(0, cs.Rules.Count);
    }

    [TestMethod]
    public void ShouldParseNumericCodeSystemNameAndId()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: 123
        Id: 456
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "123");
        Assert.AreEqual("123", cs.Name);
        Assert.AreEqual("456", cs.Id);
    }

    [TestMethod]
    public void ShouldParseCodeSystemWithMultiLineDescription()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        Id: zoo-codes
        Title: ""Zoo Animals""
        Description: """"""
        Animals that may be present at the zoo. This includes
        animals that have been classified by biologists, as
        well as certain cryptids, such as:
        * quadrapedal cryptids
          * jackalope
          * hodag
        * bipedal cryptids
          * swamp ape
          * hopkinsville goblin
        """"""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        var expected = string.Join("\n", new[]
        {
            "Animals that may be present at the zoo. This includes",
            "animals that have been classified by biologists, as",
            "well as certain cryptids, such as:",
            "* quadrapedal cryptids",
            "  * jackalope",
            "  * hodag",
            "* bipedal cryptids",
            "  * swamp ape",
            "  * hopkinsville goblin"
        });
        Assert.AreEqual(expected, cs.Description);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        Id: zoo-codes
        Title: ""Zoo Animals""
        Description: ""Animals and cryptids that may be at a zoo.""
        Title: ""Duplicate Animals""
        Id: zoo-codes-again
        Description: ""Lions and tigers and bears!""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual("zoo-codes", cs.Id);
        Assert.AreEqual("Zoo Animals", cs.Title);
        Assert.AreEqual("Animals and cryptids that may be at a zoo.", cs.Description);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipCodeSystemWithDuplicateName()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate code system name) not implemented");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipCodeSystemWithDuplicateNameAcrossFiles()
    {
        Assert.Inconclusive("Not tested: multi-file parsing not supported by single-file parser");
    }

    // ─── #concept ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseCodeSystemWithOneConcept()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #lion
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(1, cs.Rules.Count);
        var concept = (Concept)cs.Rules[0];
        Assert.AreEqual(1, concept.Codes.Count);
        Assert.AreEqual("lion", concept.Codes[0]);
        Assert.IsNull(concept.Display);
        Assert.IsNull(concept.Definition);
        Assert.AreEqual(3, concept.Position?.StartLine);
        Assert.AreEqual(0, concept.Position?.StartColumn);
    }

    [TestMethod]
    public void ShouldParseCodeSystemWithOneConceptWithADisplayString()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #tiger ""Tiger""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(1, cs.Rules.Count);
        var concept = (Concept)cs.Rules[0];
        Assert.AreEqual("tiger", concept.Codes[0]);
        Assert.AreEqual("Tiger", concept.Display);
        Assert.IsNull(concept.Definition);
    }

    [TestMethod]
    public void ShouldParseCodeSystemWithOneConceptWithDisplayAndDefinition()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #bear ""Bear"" ""A member of family Ursidae.""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(1, cs.Rules.Count);
        var concept = (Concept)cs.Rules[0];
        Assert.AreEqual("bear", concept.Codes[0]);
        Assert.AreEqual("Bear", concept.Display);
        Assert.AreEqual("A member of family Ursidae.", concept.Definition);
    }

    [TestMethod]
    public void ShouldParseConceptWithMultiLineDefinition()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #gorilla ""Gorilla"" """"""
        Let there be no mistake
        about the greatest ape of all.
        """"""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(1, cs.Rules.Count);
        var concept = (Concept)cs.Rules[0];
        var expected = string.Join("\n", new[] { "Let there be no mistake", "about the greatest ape of all." });
        Assert.AreEqual("gorilla", concept.Codes[0]);
        Assert.AreEqual("Gorilla", concept.Display);
        Assert.AreEqual(expected, concept.Definition);
    }

    [TestMethod]
    public void ShouldParseCodeSystemWithMoreThanOneConcept()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #lion
        * #tiger ""Tiger""
        * #bear ""Bear"" ""A member of family Ursidae.""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(3, cs.Rules.Count);
        var c0 = (Concept)cs.Rules[0];
        Assert.AreEqual("lion", c0.Codes[0]);
        Assert.IsNull(c0.Display);
        var c1 = (Concept)cs.Rules[1];
        Assert.AreEqual("tiger", c1.Codes[0]);
        Assert.AreEqual("Tiger", c1.Display);
        var c2 = (Concept)cs.Rules[2];
        Assert.AreEqual("bear", c2.Codes[0]);
        Assert.AreEqual("Bear", c2.Display);
        Assert.AreEqual("A member of family Ursidae.", c2.Definition);
    }

    [TestMethod]
    public void ShouldParseCodeSystemWithHierarchicalCodes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #bear ""Bear"" ""A member of family Ursidae.""
        * #bear #sunbear ""Sun bear"" ""Helarctos malayanus""
        * #bear #sunbear #ursula ""Ursula the sun bear""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(3, cs.Rules.Count);

        var c0 = (Concept)cs.Rules[0];
        Assert.AreEqual(1, c0.Codes.Count);
        Assert.AreEqual("bear", c0.Codes[0]);

        var c1 = (Concept)cs.Rules[1];
        Assert.AreEqual(2, c1.Codes.Count);
        Assert.AreEqual("bear", c1.Codes[0]);
        Assert.AreEqual("sunbear", c1.Codes[1]);
        Assert.AreEqual("Sun bear", c1.Display);
        Assert.AreEqual("Helarctos malayanus", c1.Definition);

        var c2 = (Concept)cs.Rules[2];
        Assert.AreEqual(3, c2.Codes.Count);
        Assert.AreEqual("bear", c2.Codes[0]);
        Assert.AreEqual("sunbear", c2.Codes[1]);
        Assert.AreEqual("ursula", c2.Codes[2]);
        Assert.AreEqual("Ursula the sun bear", c2.Display);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenConceptIncludesASystemDeclaration()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (concept system declaration) not implemented");
    }

    // ─── #CaretValueRule ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseCodeSystemCaretValueRuleWithNoCodes()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * ^publisher = ""Matt""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(1, cs.Rules.Count);
        var rule = (CsCaretValueRule)cs.Rules[0];
        Assert.AreEqual(0, rule.Codes.Count);
        Assert.AreEqual("publisher", rule.CaretPath);
        Assert.IsInstanceOfType<StringValue>(rule.Value);
        Assert.AreEqual("Matt", ((StringValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseCodeSystemCaretValueRulesWithNoCodesAlongsideRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #lion
        * ^publisher = ""Damon""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(2, cs.Rules.Count);
        var c0 = (Concept)cs.Rules[0];
        Assert.AreEqual("lion", c0.Codes[0]);
        var rule = (CsCaretValueRule)cs.Rules[1];
        Assert.AreEqual("publisher", rule.CaretPath);
        Assert.AreEqual("Damon", ((StringValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseCodeSystemCaretValueRuleOnTopLevelConcept()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #anteater ""Anteater""
        * #anteater ^property[0].valueString = ""Their threat pose is really cute.""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(2, cs.Rules.Count);
        var c0 = (Concept)cs.Rules[0];
        Assert.AreEqual("anteater", c0.Codes[0]);
        var rule = (CsCaretValueRule)cs.Rules[1];
        Assert.AreEqual(1, rule.Codes.Count);
        Assert.AreEqual("#anteater", rule.Codes[0]);
        Assert.AreEqual("property[0].valueString", rule.CaretPath);
        Assert.AreEqual("Their threat pose is really cute.", ((StringValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseCodeSystemCaretValueRuleOnNestedConcept()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #anteater ""Anteater""
        * #anteater #northern ""Northern tamandua""
        * #anteater #northern ^property[0].valueString = ""They are strong climbers.""
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(3, cs.Rules.Count);
        var c0 = (Concept)cs.Rules[0];
        Assert.AreEqual("anteater", c0.Codes[0]);
        var c1 = (Concept)cs.Rules[1];
        Assert.AreEqual(2, c1.Codes.Count);
        Assert.AreEqual("anteater", c1.Codes[0]);
        Assert.AreEqual("northern", c1.Codes[1]);
        var rule = (CsCaretValueRule)cs.Rules[2];
        Assert.AreEqual(2, rule.Codes.Count);
        Assert.AreEqual("#anteater", rule.Codes[0]);
        Assert.AreEqual("#northern", rule.Codes[1]);
        Assert.AreEqual("property[0].valueString", rule.CaretPath);
        Assert.AreEqual("They are strong climbers.", ((StringValue)rule.Value!).Value);
    }

    [TestMethod]
    public void ShouldKeepRawValueOfCodeCaretValueRuleForNumberOrBoolean()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: ZOO
        * #anteater ""Anteater""
        * #anteater ^extension[0].valueInteger = 0.4500
        * #anteater ^extension[1].valueBoolean = true
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "ZOO");
        Assert.AreEqual(3, cs.Rules.Count);
        var r1 = (CsCaretValueRule)cs.Rules[1];
        Assert.AreEqual("extension[0].valueInteger", r1.CaretPath);
        Assert.IsInstanceOfType<NumberValue>(r1.Value);
        Assert.AreEqual(0.45m, ((NumberValue)r1.Value!).Value);
        var r2 = (CsCaretValueRule)cs.Rules[2];
        Assert.AreEqual("extension[1].valueBoolean", r2.CaretPath);
        Assert.IsInstanceOfType<BooleanValue>(r2.Value);
        Assert.IsTrue(((BooleanValue)r2.Value!).Value);
    }

    // ─── #insertRule ─────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseInsertRuleWithSingleRuleSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: MyCS
        * insert MyRuleSet
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "MyCS");
        Assert.AreEqual(1, cs.Rules.Count);
        var rule = (CsInsertRule)cs.Rules[0];
        Assert.AreEqual("MyRuleSet", rule.RuleSetReference);
        Assert.AreEqual(0, rule.Codes.Count);
    }

    [TestMethod]
    public void ShouldParseInsertRuleWithSingleRuleSetAndCodePath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
        CodeSystem: MyCS
        * #cookie ""Cookie""
        * #cookie insert MyRuleSet
        ");
        var cs = SushiTestHelper.GetCodeSystem(doc, "MyCS");
        Assert.AreEqual(2, cs.Rules.Count);
        var rule = (CsInsertRule)cs.Rules[1];
        Assert.AreEqual("MyRuleSet", rule.RuleSetReference);
        Assert.AreEqual(1, rule.Codes.Count);
        Assert.AreEqual("#cookie", rule.Codes[0]);
    }
}
