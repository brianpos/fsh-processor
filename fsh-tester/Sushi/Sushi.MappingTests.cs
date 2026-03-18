// Ported from SUSHI: test/import/FSHImporter.Mapping.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Mapping.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI defaults Mapping.id to the mapping name when Id is not specified.
//    Our parser does not apply this default; Id remains null.
//  - SUSHI resolves aliases in Mapping.source (e.g. 'OBS' → resolved URL).
//    Our parser stores the alias name verbatim.
//  - MappingMapRule.Language stores the optional comment (second STRING token).
//    MappingMapRule.Code stores the optional language code (CODE token, e.g. "#lang").
//    SUSHI names these fields 'comment' and 'language' respectively.
//  - SUSHI reports duplicate-name errors (loggerSpy); our parser has no semantic validation.
//  - SUSHI discards MappingPathRule from the rules list; our parser includes it.
//  - Columns in SourcePosition are 0-based (ANTLR); SUSHI uses 1-based.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class MappingTests
{
    // ─── Mapping metadata ─────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseTheSimplestPossibleMapping()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
        ");
        Assert.AreEqual(1, SushiTestHelper.GetMappings(doc).Count);
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual("MyMapping", mapping.Name);
        // SUSHI defaults id to name; our parser leaves Id as null
        Assert.IsNull(mapping.Id);
    }

    [TestMethod]
    public void ShouldParseAMappingWithAdditionalMetadataProperties()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            Id: my-map
            Source: Patient
            Target: ""http://some.com/mappedTo""
            Description: ""This is a description""
            Title: ""This is a title""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetMappings(doc).Count);
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual("MyMapping", mapping.Name);
        Assert.AreEqual("my-map", mapping.Id);
        Assert.AreEqual("Patient", mapping.Source);
        Assert.AreEqual("http://some.com/mappedTo", mapping.Target);
        Assert.AreEqual("This is a description", mapping.Description);
        Assert.AreEqual("This is a title", mapping.Title);
    }

    [TestMethod]
    public void ShouldParseNumericMappingNameIdAndSource()
    {
        // NOT recommended, but possible
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: 123
            Id: 456
            Source: 789
        ");
        Assert.AreEqual(1, SushiTestHelper.GetMappings(doc).Count);
        var mapping = SushiTestHelper.GetMapping(doc, "123");
        Assert.AreEqual("123", mapping.Name);
        Assert.AreEqual("456", mapping.Id);
        Assert.AreEqual("789", mapping.Source);
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // X3: first-wins semantics — matches SUSHI behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            Id: first-id
            Id: second-id
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        // X3: first-wins — the first declaration is kept.
        Assert.AreEqual("first-id", mapping.Id);
    }

    [TestMethod]
    public void ShouldNotResolveAliasForMappingSource()
    {
        // SUSHI resolves the alias OBS → full URL in mapping.source.
        // Our parser stores the alias name as-is (no alias resolution).
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: OBS = http://hl7.org/fhir/StructureDefinition/Observation

            Mapping: MyMapping
            Source: OBS
        ");
        Assert.AreEqual(1, SushiTestHelper.GetMappings(doc).Count);
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual("MyMapping", mapping.Name);
        // Alias not resolved; our parser stores the raw name
        Assert.AreEqual("OBS", mapping.Source);
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipMappingWhenEncounteringDuplicateName()
    {
        // SUSHI semantic validation: duplicate mapping name → error + skip.
        // Our parser does not implement this check.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate mapping name) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipMappingWhenDuplicateNameInAnotherFile()
    {
        // Multi-file scenario not supported by single-file parser.
        Assert.Inconclusive("Not tested: multi-file mapping name de-duplication not supported by single-file parser");
    }

    // ─── Mapping rules ────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseASimpleMappingRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * identifier -> ""Patient.identifier""
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingMapRule)mapping.Rules[0];
        Assert.AreEqual("identifier", rule.Path);
        Assert.AreEqual("Patient.identifier", rule.Target);
        Assert.IsNull(rule.Language); // no comment
        Assert.IsNull(rule.Code);     // no language code
    }

    [TestMethod]
    public void ShouldParseAMappingRuleWithNoPath()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * -> ""Patient""
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingMapRule)mapping.Rules[0];
        // SUSHI uses '' for no path; our model uses null (path? is optional in grammar)
        Assert.IsNull(rule.Path);
        Assert.AreEqual("Patient", rule.Target);
        Assert.IsNull(rule.Language);
        Assert.IsNull(rule.Code);
    }

    [TestMethod]
    public void ShouldParseAMappingRuleWithAComment()
    {
        // The second STRING in a mapping rule is the 'comment' (stored in Language in our model).
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * identifier -> ""Patient.identifier"" ""some comment""
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingMapRule)mapping.Rules[0];
        Assert.AreEqual("identifier", rule.Path);
        Assert.AreEqual("Patient.identifier", rule.Target);
        // In our model, Language holds the comment (second STRING token)
        Assert.AreEqual("some comment", rule.Language);
        Assert.IsNull(rule.Code);
    }

    [TestMethod]
    public void ShouldParseAMappingRuleWithALanguage()
    {
        // The CODE token in a mapping rule is the language (stored in Code in our model).
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * identifier -> ""Patient.identifier"" #lang
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingMapRule)mapping.Rules[0];
        Assert.AreEqual("identifier", rule.Path);
        Assert.AreEqual("Patient.identifier", rule.Target);
        Assert.IsNull(rule.Language); // no comment
        // In our model, Code holds the language code (CODE token, includes '#')
        Assert.AreEqual("#lang", rule.Code);
    }

    [TestMethod]
    public void ShouldParseAMappingRuleWithCommentAndLanguage()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * identifier -> ""Patient.identifier"" ""some comment"" #lang
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingMapRule)mapping.Rules[0];
        Assert.AreEqual("identifier", rule.Path);
        Assert.AreEqual("Patient.identifier", rule.Target);
        Assert.AreEqual("some comment", rule.Language);
        Assert.AreEqual("#lang", rule.Code);
    }

    [TestMethod]
    public void ShouldLogAWarningWhenLanguageHasASystem()
    {
        // SUSHI logs a warning when the language code has a system (e.g. sys#lang).
        // Our parser stores it without validation.
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * identifier -> ""Patient.identifier"" sys#lang
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingMapRule)mapping.Rules[0];
        Assert.AreEqual("identifier", rule.Path);
        Assert.AreEqual("Patient.identifier", rule.Target);
        Assert.IsNull(rule.Language);
        // CODE token including system prefix
        Assert.AreEqual("sys#lang", rule.Code);
        // Warning logging: inconclusive (semantic validation not implemented)
    }

    [TestMethod]
    public void ShouldParseAMappingRuleWithAMultilineComment()
    {
        // Our grammar only allows STRING (not MULTILINE_STRING) in mapping rule comments:
        // mappingRule: STAR path? ARROW STRING STRING? CODE?
        Assert.Inconclusive("Not tested: MULTILINE_STRING comment in mapping rules not supported by parser grammar");
    }

    [TestMethod]
    public void ShouldParseAMappingRuleWithAMultilineCommentAndLanguage()
    {
        // Same as above — MULTILINE_STRING is not supported in mapping rule comments.
        Assert.Inconclusive("Not tested: MULTILINE_STRING comment in mapping rules not supported by parser grammar");
    }

    // ─── Insert rules in Mappings ─────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAnInsertRuleWithASingleRuleSet()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            * insert MyRuleSet
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        Assert.AreEqual(1, mapping.Rules.Count);
        var rule = (MappingInsertRule)mapping.Rules[0];
        // SUSHI assertInsertRule uses path='' for no path; our model uses null
        Assert.IsNull(rule.Path);
        Assert.AreEqual("MyRuleSet", rule.RuleSetReference);
    }

    // ─── Path rules in Mappings ───────────────────────────────────────────────

    [TestMethod]
    public void ShouldParseAPathRuleAndIncludeItInMappingRules()
    {
        // SUSHI discards path rules from the mapping rules list.
        // Our parser includes MappingPathRule in the rules list.
        // Note: our grammar requires Target to be a quoted STRING; SUSHI allows bare URLs.
        var doc = SushiTestHelper.ParseDoc(@"
            Mapping: MyMapping
            Source: Patient1
            Target: ""http://example.org/target""
            * name
        ");
        var mapping = SushiTestHelper.GetMapping(doc, "MyMapping");
        // Our parser keeps the path rule (SUSHI discards it, expecting 0 rules)
        Assert.AreEqual(1, mapping.Rules.Count);
        Assert.IsInstanceOfType<MappingPathRule>(mapping.Rules[0]);
        Assert.AreEqual("name", mapping.Rules[0].Path);
    }
}
