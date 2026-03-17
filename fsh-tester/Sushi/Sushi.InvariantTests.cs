// Ported from SUSHI: test/import/FSHImporter.Invariant.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Invariant.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI stores severity as FshCode (with .code property, stripping the '#').
//    Our parser stores the raw CODE token, so Severity = "#error" (includes '#').
//  - SUSHI discards InvariantPathRule from rules lists.
//    Our parser keeps InvariantPathRule in the rules list.
//  - SUSHI composes child-rule paths using parent path rules (indentation-based).
//    Our parser does not perform path composition; paths are stored as parsed.
//  - SUSHI reports duplicate-name errors (loggerSpy); our parser has no semantic validation.
//  - Columns in SourcePosition are 0-based (ANTLR); SUSHI uses 1-based.
//    Line positions are also affected by leftAlign trimming applied by ParseDoc.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class InvariantTests
{
    #region Invariant Metadata

    [TestMethod]
    public void ShouldParseTheSimplestPossibleInvariant()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: emp-1
            Severity: #error
            Description: ""This does not actually require anything.""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "emp-1");
        Assert.AreEqual("emp-1", invariant.Name);
        // Our parser stores the raw CODE token including '#'
        Assert.AreEqual("#error", invariant.Severity);
        Assert.AreEqual("This does not actually require anything.", invariant.Description);
    }

    [TestMethod]
    public void ShouldParseNumericInvariantName()
    {
        // NOT recommended, but possible
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: 123
            Severity: #error
            Description: ""This does not actually require anything.""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "123");
        Assert.AreEqual("123", invariant.Name);
    }

    [TestMethod]
    public void ShouldParseAnInvariantWithAdditionalMetadata()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: full-1
            Description: ""This resource must define a cage and aquarium.""
            Expression: ""cage.exists() and aquarium.exists()""
            XPath: ""exists(f:cage) and exists(f:aquarium)""
            Severity: #error
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "full-1");
        Assert.AreEqual("full-1", invariant.Name);
        Assert.AreEqual("This resource must define a cage and aquarium.", invariant.Description);
        Assert.AreEqual("cage.exists() and aquarium.exists()", invariant.Expression);
        Assert.AreEqual("exists(f:cage) and exists(f:aquarium)", invariant.XPath);
        Assert.AreEqual("#error", invariant.Severity);
    }

    [TestMethod]
    public void ShouldParseAnInvariantWithMultilineExpression()
    {
        // SUSHI supports multiline expressions (""" """).
        // Our grammar's expression rule only accepts a single-line STRING token,
        // so multiline expressions are not supported by the parser.
        Assert.Inconclusive("Not tested: multiline Expression (MULTILINE_STRING) not supported by the parser grammar (expression: KW_EXPRESSION STRING)");
    }

    [TestMethod]
    public void ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared()
    {
        // X3: first-wins semantics — matches SUSHI behaviour.
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: obs-1
            Severity: #error
            Description: ""First description.""
            Description: ""Second description.""
        ");
        var invariant = SushiTestHelper.GetInvariant(doc, "obs-1");
        // First declaration wins; the second is ignored.
        Assert.AreEqual("First description.", invariant.Description);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute()
    {
        // SUSHI semantic validation: logs errors for each duplicate metadata.
        // Our parser silently ignores duplicates (first-wins, no error logged).
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate metadata errors) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipInvariantWhenEncounteringADuplicateName()
    {
        // SUSHI semantic validation: duplicate invariant name → error + skip.
        // Our parser does not implement this check.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate invariant name) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipInvariantWhenDuplicateNameInAnotherFile()
    {
        // Multi-file scenario not supported by single-file parser.
        Assert.Inconclusive("Not tested: multi-file invariant name de-duplication not supported by single-file parser");
    }

    #endregion

    #region Assignment Rules in Invariants

    [TestMethod]
    public void ShouldParseAnInvariantWithAssignedValueRules()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: rules-1
            Severity: #error
            Description: ""This has some rules.""
            * requirements = ""This invariant exists because I willed it so.""
            * expression = ""name.exists()""
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "rules-1");
        Assert.AreEqual("rules-1", invariant.Name);
        Assert.AreEqual("#error", invariant.Severity);
        Assert.AreEqual("This has some rules.", invariant.Description);
        Assert.AreEqual(2, invariant.Rules.Count);

        var rule0 = (InvariantFixedValueRule)invariant.Rules[0];
        Assert.AreEqual("requirements", rule0.Path);
        Assert.AreEqual("This invariant exists because I willed it so.", ((StringValue)rule0.Value!).Value);

        var rule1 = (InvariantFixedValueRule)invariant.Rules[1];
        Assert.AreEqual("expression", rule1.Path);
        Assert.AreEqual("name.exists()", ((StringValue)rule1.Value!).Value);
    }

    [TestMethod]
    public void ShouldParseAnInvariantWithAssignedValuesThatAreAnAlias()
    {
        // SUSHI resolves SOURCE alias → http://example.org/something.
        // Our parser stores the raw name (alias not resolved).
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: SOURCE = http://example.org/something

            Invariant: rules-2
            Severity: #error
            Description: ""This has a rule.""
            * source = SOURCE
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "rules-2");
        Assert.AreEqual(1, invariant.Rules.Count);
        var rule = (InvariantFixedValueRule)invariant.Rules[0];
        Assert.AreEqual("source", rule.Path);
        // Alias not resolved; raw name token stored as NameValue
        Assert.AreEqual("SOURCE", ((NameValue)rule.Value!).Value);
    }

    #endregion

    #region Path Rules in Invariants

    [TestMethod]
    public void ShouldParseAPathRuleAndIncludeItInRules()
    {
        // SUSHI discards path rules from the invariant rules list.
        // Our parser includes InvariantPathRule in the rules list.
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: rules-3
            Severity: #error
            Description: ""This has a rule.""
            * requirements
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "rules-3");
        // Our parser keeps the path rule (SUSHI discards it)
        Assert.AreEqual(1, invariant.Rules.Count);
        Assert.IsInstanceOfType<InvariantPathRule>(invariant.Rules[0]);
        Assert.AreEqual("requirements", invariant.Rules[0].Path);
    }

    [TestMethod]
    public void ShouldUseAPathRuleToConstructAFullPath()
    {
        // P-FP1: path composition via indented rules.
        // Per spec §Indented Rules: the full path of an indented rule SHALL be obtained
        // by prepending the path from the preceding less-indented rule.
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: inv-path
            Severity: #error
            Description: ""Path composition test""
            * component
              * code = http://loinc.org#1234
        ");
        var invariant = SushiTestHelper.GetInvariant(doc, "inv-path");
        // PathRule sets context; FixedValueRule gets composed path.
        Assert.AreEqual(2, invariant.Rules.Count);
        Assert.IsInstanceOfType<InvariantPathRule>(invariant.Rules[0]);
        Assert.AreEqual("component", invariant.Rules[0].Path);
        Assert.IsInstanceOfType<InvariantFixedValueRule>(invariant.Rules[1]);
        Assert.AreEqual("component.code", invariant.Rules[1].Path);
    }

    [TestMethod]
    public void ShouldProperlyHandleSoftIndicesWithPathRules()
    {
        // P-FP1 + P-FP2: path composition combined with soft-index expansion.
        // Per spec §Soft Indexing: [+] increments the last index, [=] reuses it.
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: inv-soft
            Severity: #error
            Description: ""Soft index test""
            * identifier[+]
              * system = ""http://example.org/sys1""
              * value = ""id1""
            * identifier[+]
              * system = ""http://example.org/sys2""
              * value = ""id2""
        ");
        var invariant = SushiTestHelper.GetInvariant(doc, "inv-soft");
        // 6 rules: 2 PathRules + 4 FixedValueRules
        Assert.AreEqual(6, invariant.Rules.Count);
        // First [+] → identifier[0]
        Assert.AreEqual("identifier[0]", invariant.Rules[0].Path);
        Assert.AreEqual("identifier[0].system", invariant.Rules[1].Path);
        Assert.AreEqual("identifier[0].value", invariant.Rules[2].Path);
        // Second [+] → identifier[1]
        Assert.AreEqual("identifier[1]", invariant.Rules[3].Path);
        Assert.AreEqual("identifier[1].system", invariant.Rules[4].Path);
        Assert.AreEqual("identifier[1].value", invariant.Rules[5].Path);
    }

    #endregion

    #region Insert Rules in Invariants

    [TestMethod]
    public void ShouldParseAnInsertRule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Invariant: rules-4
            Severity: #error
            Description: ""This has a rule.""
            * insert MyRuleSet
        ");
        Assert.AreEqual(1, SushiTestHelper.GetInvariants(doc).Count);
        var invariant = SushiTestHelper.GetInvariant(doc, "rules-4");
        Assert.AreEqual(1, invariant.Rules.Count);
        var rule = (InvariantInsertRule)invariant.Rules[0];
        // SUSHI assertInsertRule uses path='' for no path; our model uses null
        Assert.IsNull(rule.Path);
        Assert.AreEqual("MyRuleSet", rule.RuleSetReference);
    }

    #endregion
}
