// Ported from SUSHI: test/import/FSHImporter.Alias.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Alias.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI resolves aliases in rules (e.g. binding rule valueSet = resolved URL).
//    Our parser does NOT resolve aliases; the alias name is stored verbatim.
//  - SUSHI reports semantic errors for duplicate/invalid aliases (loggerSpy).
//    Our parser does not implement semantic validation; those tests are inconclusive.
//  - SUSHI's importText() supports multi-file alias sharing; our parser is single-file.

using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class AliasTests
{
    // ─── Basic alias storage ──────────────────────────────────────────────────

    [TestMethod]
    public void ShouldCollectAndReturnAliasesInResult()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: LOINC = http://loinc.org
            Alias: SCT = http://snomed.info/sct

            Profile: ObservationProfile
            Parent: Observation

            Alias: RXNORM = http://www.nlm.nih.gov/research/umls/rxnorm

            Profile: AnotherObservationProfile
            Parent: Observation

            Alias: UCUM = http://unitsofmeasure.org
        ");

        Assert.AreEqual(4, SushiTestHelper.GetAliases(doc).Count);
        Assert.AreEqual("http://loinc.org", SushiTestHelper.GetAlias(doc, "LOINC")?.Value);
        Assert.AreEqual("http://snomed.info/sct", SushiTestHelper.GetAlias(doc, "SCT")?.Value);
        Assert.AreEqual("http://www.nlm.nih.gov/research/umls/rxnorm", SushiTestHelper.GetAlias(doc, "RXNORM")?.Value);
        Assert.AreEqual("http://unitsofmeasure.org", SushiTestHelper.GetAlias(doc, "UCUM")?.Value);
    }

    [TestMethod]
    public void ShouldParseAliasesThatReplicateTheSyntaxOfACode()
    {
        // Alias value with a # fragment is lexed as a CODE token
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: LOINC = http://loinc.org#1234
        ");

        Assert.AreEqual(1, SushiTestHelper.GetAliases(doc).Count);
        Assert.AreEqual("http://loinc.org#1234", SushiTestHelper.GetAlias(doc, "LOINC")?.Value);
    }

    [TestMethod]
    public void ShouldReportWhenTheSameAliasIsDefinedTwiceWithDifferentValuesInTheSameFile()
    {
        // SUSHI semantic validation: duplicate alias with different value → error.
        // Our parser does not implement this check.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate alias with different value) not implemented in parser");
    }

    [TestMethod]
    public void ShouldReportWhenTheSameAliasIsDefinedTwiceWithDifferentValuesInDifferentFiles()
    {
        // SUSHI multi-file alias de-duplication semantics.
        // Our parser processes a single text at a time and does not implement this.
        Assert.Inconclusive("Not tested: multi-file alias resolution not supported by single-file parser");
    }

    [TestMethod]
    public void ShouldNotReportErrorWhenTheSameAliasIsDefinedMultipleTimesWithSameValues()
    {
        // Same alias defined twice with identical value → SUSHI allows this.
        // Our parser simply stores both; verify at least one is parseable.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: USCoreRace = http://hl7.org/fhir/us/core/StructureDefinition/us-core-race
            Alias: USCoreRace = http://hl7.org/fhir/us/core/StructureDefinition/us-core-race
        ");
        // ParseDoc succeeds (no parse error). Alias is present.
        var aliases = SushiTestHelper.GetAliases(doc).Where(a => a.Name == "USCoreRace").ToList();
        Assert.IsTrue(aliases.Count >= 1);
        Assert.AreEqual("http://hl7.org/fhir/us/core/StructureDefinition/us-core-race", aliases[0].Value);
    }

    // ─── Alias translation in binding rules ───────────────────────────────────

    [TestMethod]
    public void ShouldNotResolveAliasInBindingRuleWhenAliasIsDefinedBeforeItsUse()
    {
        // SUSHI resolves LOINC → http://loinc.org in rule.valueSet.
        // Our parser stores the alias name as-is.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: LOINC = http://loinc.org

            Profile: ObservationProfile
            Parent: Observation
            * code from LOINC
        ");
        var profile = SushiTestHelper.GetProfile(doc, "ObservationProfile");
        var rule = (ValueSetRule)profile.Rules[0];
        // Our parser keeps the alias name, not the resolved URL.
        Assert.AreEqual("LOINC", rule.ValueSetName);
    }

    [TestMethod]
    public void ShouldNotResolveAliasInBindingRuleWhenAliasIsDefinedAfterItsUse()
    {
        // SUSHI resolves LOINC regardless of declaration order.
        // Our parser stores the alias name as-is.
        var doc = SushiTestHelper.ParseDoc(@"
            Profile: ObservationProfile
            Parent: Observation
            * code from LOINC

            Alias: LOINC = http://loinc.org
        ");
        var profile = SushiTestHelper.GetProfile(doc, "ObservationProfile");
        var rule = (ValueSetRule)profile.Rules[0];
        Assert.AreEqual("LOINC", rule.ValueSetName);
    }

    [TestMethod]
    public void ShouldNotTranslateAnAliasWhenAliasDoesNotMatch()
    {
        // LAINC (typo) does not match any alias, so SUSHI keeps it as-is.
        // Our parser also keeps it as-is (no alias resolution).
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: LOINC = http://loinc.org

            Profile: ObservationProfile
            Parent: Observation
            * code from LAINC
        ");
        var profile = SushiTestHelper.GetProfile(doc, "ObservationProfile");
        var rule = (ValueSetRule)profile.Rules[0];
        Assert.AreEqual("LAINC", rule.ValueSetName);
    }

    [TestMethod]
    public void ShouldTranslateAnAliasFromAnyInputFile()
    {
        // Multi-file scenario: alias defined in file 2, used in file 1.
        // Our parser handles one file at a time and cannot share aliases between files.
        Assert.Inconclusive("Not tested: multi-file alias sharing not supported by single-file parser");
    }

    // ─── $ prefix alias error cases (semantic validation) ────────────────────

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAliasedCodePrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAliasedValueSetRulePrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAliasedReferencePrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAssignmentRuleAliasedReferencePrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnOnlyRuleAliasedReferencePrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    [TestMethod]
    public void ShouldNotLogAnErrorWhenAContainsRuleAliasedExtensionPrefixedWithDollarDoesNotResolve()
    {
        // SUSHI test verifies no error is logged. Our parser also parses without error.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: $MYEXTENSION = http://hl7.org/fhir/StructureDefinition/mypatient-extension.html

            Profile: ObservationProfile
            Parent: Observation
            * extension contains $TYPO 1..1
        ");
        // If ParseDoc doesn't throw, the parse succeeded with no parse errors.
        Assert.IsNotNull(doc);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAliasedContainsRuleTypePrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias in contains rule) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAliasedValueSetSystemPrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAnAliasedValueSetPrefixedWithDollarDoesNotResolve()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation ($-prefixed unresolved alias) not implemented in parser");
    }

    // ─── Version-qualified alias resolution ──────────────────────────────────

    [TestMethod]
    public void ShouldNotResolveAliasInCodeWithVersion()
    {
        // SUSHI resolves 'LOINC|123#foo' to FshCode(code='foo', system='http://loinc.org|123').
        // Our parser stores the raw unresolved CODE token value.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: LOINC = http://loinc.org

            Profile: ObservationProfile
            Parent: Observation
            * code = LOINC|123#foo
        ");
        var profile = SushiTestHelper.GetProfile(doc, "ObservationProfile");
        var rule = (FixedValueRule)profile.Rules[0];
        var codeValue = (Code)rule.Value!;
        // Alias not resolved; CODE token is the raw text
        Assert.AreEqual("LOINC|123#foo", codeValue.Value);
    }

    [TestMethod]
    public void ShouldNotResolveAliasInCodeWithEmptyVersion()
    {
        // SUSHI resolves 'LOINC|#foo' (empty version) to FshCode(code='foo', system='http://loinc.org').
        // Our parser stores the raw CODE token value.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: LOINC = http://loinc.org

            Profile: ObservationProfile
            Parent: Observation
            * code = LOINC|#foo
        ");
        var profile = SushiTestHelper.GetProfile(doc, "ObservationProfile");
        var rule = (FixedValueRule)profile.Rules[0];
        var codeValue = (Code)rule.Value!;
        Assert.AreEqual("LOINC|#foo", codeValue.Value);
    }

    [TestMethod]
    public void ShouldLogAnErrorWhenAliasContainsReservedCharacters()
    {
        // SUSHI logs an error for '|' in alias names.
        // Our parser does not implement this check.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (alias with reserved characters) not implemented in parser");
    }

    [TestMethod]
    public void ShouldResolveAnAliasWithAllSupportedCharacters()
    {
        // SUSHI resolves 'Foo_McBar-Baz_Jr.3#foo' → FshCode(code='foo', system='http://example.org').
        // Our parser stores the raw CODE token; alias not resolved.
        var doc = SushiTestHelper.ParseDoc(@"
            Alias: Foo_McBar-Baz_Jr.3 = http://example.org

            Profile: ObservationProfile
            Parent: Observation
            * code = Foo_McBar-Baz_Jr.3#foo
        ");
        var profile = SushiTestHelper.GetProfile(doc, "ObservationProfile");
        var rule = (FixedValueRule)profile.Rules[0];
        var codeValue = (Code)rule.Value!;
        Assert.AreEqual("Foo_McBar-Baz_Jr.3#foo", codeValue.Value);
    }

    [TestMethod]
    public void ShouldResolveButLogWarningWhenAliasContainsUnsupportedCharacters()
    {
        // SUSHI resolves 'B@dAlias#foo' and logs a warning about unsupported characters.
        // Our parser does not implement alias resolution or this warning check.
        Assert.Inconclusive("Not tested: SUSHI semantic validation (alias with unsupported characters) not implemented in parser");
    }

    [TestMethod]
    public void ShouldResolveButLogWarningWhenAliasContainsUnsupportedCharactersAndStartsWithDollar()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (alias with unsupported characters) not implemented in parser");
    }

    [TestMethod]
    public void ShouldResolveButLogWarningWhenAliasContainsOnlyADollar()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (alias with unsupported characters) not implemented in parser");
    }
}
