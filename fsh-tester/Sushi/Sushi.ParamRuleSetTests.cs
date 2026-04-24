// Ported from SUSHI test: FSHImporter.ParamRuleSet.test.ts
// Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.ParamRuleSet.test.ts
//
// Key differences vs SUSHI:
//  - SUSHI's FSHImporter.paramRuleSets is a separate map from the doc's ruleSets.
//    C# stores parameterized rule sets in the same document as regular RuleSets, with IsParameterized=true.
//  - SUSHI's result.parameters is a plain string array; C# uses List<RuleSetParameter> with .Value property.
//  - SUSHI's result.contents holds the raw template text; C# stores it in RuleSet.UnparsedContent.
//  - SUSHI reports semantic errors for duplicate names and unused parameters;
//    our parser has no semantic validation.
//  - SUSHI multi-file import not supported by single-file ParseDoc; those tests are inconclusive.

using Hl7.FhirShorthand.Serialization.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

[TestClass]
public class ParamRuleSetTests
{
    [TestMethod]
    public void ShouldParseAParamRuleSetWithARule()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: MyRuleSet (system, strength)
            * code from {system} {strength}
            * pig from egg
        ");
        Assert.HasCount(1, SushiTestHelper.GetRuleSets(doc));
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "MyRuleSet");
        Assert.AreEqual("MyRuleSet", ruleSet.Name);
        Assert.IsTrue(ruleSet.IsParameterized);
        Assert.HasCount(2, ruleSet.Parameters);
        Assert.AreEqual("system", ruleSet.Parameters[0].Value);
        Assert.AreEqual("strength", ruleSet.Parameters[1].Value);
        Assert.IsNotNull(ruleSet.UnparsedContent);
    }

    [TestMethod]
    public void ShouldParseAParamRuleSetWithANumericName()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: 123 (system, strength)
            * code from {system} {strength}
        ");
        Assert.HasCount(1, SushiTestHelper.GetRuleSets(doc));
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "123");
        Assert.AreEqual("123", ruleSet.Name);
        Assert.IsTrue(ruleSet.IsParameterized);
        Assert.HasCount(2, ruleSet.Parameters);
        Assert.AreEqual("system", ruleSet.Parameters[0].Value);
        Assert.AreEqual("strength", ruleSet.Parameters[1].Value);
    }

    [TestMethod]
    public void ShouldParseAParamRuleSetWhenThereIsNoSpaceBetweenRulesetNameAndParameterList()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: MyRuleSet(system, strength)
            * code from {system} {strength}
            * pig from egg
        ");
        Assert.HasCount(1, SushiTestHelper.GetRuleSets(doc));
        var ruleSet = SushiTestHelper.GetRuleSet(doc, "MyRuleSet");
        Assert.AreEqual("MyRuleSet", ruleSet.Name);
        Assert.IsTrue(ruleSet.IsParameterized);
        Assert.HasCount(2, ruleSet.Parameters);
        Assert.AreEqual("system", ruleSet.Parameters[0].Value);
        Assert.AreEqual("strength", ruleSet.Parameters[1].Value);
        Assert.IsNotNull(ruleSet.UnparsedContent);
    }

    [TestMethod]
    public void ShouldStopParsingParamRuleSetContentsWhenTheNextEntityIsDefined()
    {
        var doc = SushiTestHelper.ParseDoc(@"
            RuleSet: FirstRuleSet (system, strength)
            * code from http://example.org/{system}/info.html {strength}
            * pig from egg

            Profile: MyObservation
            Parent: Observation

            RuleSet: SecondRuleSet (min)
            * stuff {min}..

            Alias: $Something = http://example.org/Something

            RuleSet: ThirdRuleSet(cookie)
            * code from {cookie}

            Extension: MyExtension

            RuleSet: FourthRuleSet(toast)
            * reason ^short = {toast}

            Instance: ExampleObservation
            InstanceOf: MyObservation

            RuleSet: FifthRuleSet(strength, system)
            * code from {system} {strength}

            ValueSet: MyValueSet

            RuleSet: SixthRuleSet(content)
            * ^description = {content}

            Invariant: cat-1
            Severity: #error
            Description: ""Some invariant""

            RuleSet: SeventhRuleSet(even, more)
            * content[+] = {even}
            * content[+] = {more}

            CodeSystem: MyCodeSystem

            RuleSet: EighthRuleSet(continuation)
            * continuation = {continuation} (exactly)

            Mapping: SomeMapping

            RuleSet: NinthRuleSet(tiring)
            * code from {tiring}

            Logical: MyLogical

            RuleSet: TenthRuleSet(conclusion)
            * valueString = {conclusion}

            Resource: MyResource
        ");

        // Verify non-ruleset entities
        Assert.IsNotNull(SushiTestHelper.GetProfile(doc, "MyObservation"));
        Assert.IsNotNull(SushiTestHelper.GetAlias(doc, "$Something"));
        Assert.IsNotNull(SushiTestHelper.GetExtension(doc, "MyExtension"));
        Assert.IsNotNull(SushiTestHelper.GetInstance(doc, "ExampleObservation"));
        Assert.IsNotNull(SushiTestHelper.GetValueSet(doc, "MyValueSet"));
        Assert.IsNotNull(SushiTestHelper.GetInvariant(doc, "cat-1"));
        Assert.IsNotNull(SushiTestHelper.GetCodeSystem(doc, "MyCodeSystem"));
        Assert.IsNotNull(SushiTestHelper.GetMapping(doc, "SomeMapping"));
        Assert.IsNotNull(SushiTestHelper.GetLogical(doc, "MyLogical"));
        Assert.IsNotNull(SushiTestHelper.GetResource(doc, "MyResource"));

        // Verify 10 parameterized rulesets
        var ruleSets = SushiTestHelper.GetRuleSets(doc);
        Assert.HasCount(10, ruleSets);

        var first = SushiTestHelper.GetRuleSet(doc, "FirstRuleSet");
        Assert.AreEqual("FirstRuleSet", first.Name);
        Assert.IsTrue(first.IsParameterized);
        Assert.HasCount(2, first.Parameters);
        Assert.AreEqual("system", first.Parameters[0].Value);
        Assert.AreEqual("strength", first.Parameters[1].Value);

        var second = SushiTestHelper.GetRuleSet(doc, "SecondRuleSet");
        Assert.AreEqual("SecondRuleSet", second.Name);
        Assert.IsTrue(second.IsParameterized);
        Assert.HasCount(1, second.Parameters);
        Assert.AreEqual("min", second.Parameters[0].Value);

        var third = SushiTestHelper.GetRuleSet(doc, "ThirdRuleSet");
        Assert.AreEqual("ThirdRuleSet", third.Name);
        Assert.IsTrue(third.IsParameterized);
        Assert.HasCount(1, third.Parameters);
        Assert.AreEqual("cookie", third.Parameters[0].Value);

        var fourth = SushiTestHelper.GetRuleSet(doc, "FourthRuleSet");
        Assert.AreEqual("FourthRuleSet", fourth.Name);
        Assert.IsTrue(fourth.IsParameterized);
        Assert.HasCount(1, fourth.Parameters);
        Assert.AreEqual("toast", fourth.Parameters[0].Value);

        var fifth = SushiTestHelper.GetRuleSet(doc, "FifthRuleSet");
        Assert.AreEqual("FifthRuleSet", fifth.Name);
        Assert.IsTrue(fifth.IsParameterized);
        Assert.HasCount(2, fifth.Parameters);
        Assert.AreEqual("strength", fifth.Parameters[0].Value);
        Assert.AreEqual("system", fifth.Parameters[1].Value);

        var sixth = SushiTestHelper.GetRuleSet(doc, "SixthRuleSet");
        Assert.AreEqual("SixthRuleSet", sixth.Name);
        Assert.IsTrue(sixth.IsParameterized);
        Assert.HasCount(1, sixth.Parameters);
        Assert.AreEqual("content", sixth.Parameters[0].Value);

        var seventh = SushiTestHelper.GetRuleSet(doc, "SeventhRuleSet");
        Assert.AreEqual("SeventhRuleSet", seventh.Name);
        Assert.IsTrue(seventh.IsParameterized);
        Assert.HasCount(2, seventh.Parameters);
        Assert.AreEqual("even", seventh.Parameters[0].Value);
        Assert.AreEqual("more", seventh.Parameters[1].Value);

        var eighth = SushiTestHelper.GetRuleSet(doc, "EighthRuleSet");
        Assert.AreEqual("EighthRuleSet", eighth.Name);
        Assert.IsTrue(eighth.IsParameterized);
        Assert.HasCount(1, eighth.Parameters);
        Assert.AreEqual("continuation", eighth.Parameters[0].Value);

        var ninth = SushiTestHelper.GetRuleSet(doc, "NinthRuleSet");
        Assert.AreEqual("NinthRuleSet", ninth.Name);
        Assert.IsTrue(ninth.IsParameterized);
        Assert.HasCount(1, ninth.Parameters);
        Assert.AreEqual("tiring", ninth.Parameters[0].Value);

        var tenth = SushiTestHelper.GetRuleSet(doc, "TenthRuleSet");
        Assert.AreEqual("TenthRuleSet", tenth.Name);
        Assert.IsTrue(tenth.IsParameterized);
        Assert.HasCount(1, tenth.Parameters);
        Assert.AreEqual("conclusion", tenth.Parameters[0].Value);
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipTheParamRuleSetWhenEncounteredAParamRuleSetWithANameUsedByAnotherParamRuleSet()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (duplicate parameterized RuleSet name) not implemented in parser");
    }

    [TestMethod]
    public void ShouldLogAnErrorAndSkipTheParamRuleSetWhenEncounteredAnParamRuleSetWithANameUsedByAnotherParamRuleSetInAnotherFile()
    {
        Assert.Inconclusive("Not tested: multi-file duplicate parameterized RuleSet name resolution not supported by single-file parser");
    }

    [TestMethod]
    public void ShouldLogAWarningWhenAParamRuleSetHasParametersThatAreNotUsedInTheContents()
    {
        Assert.Inconclusive("Not tested: SUSHI semantic validation (unused parameters warning) not implemented in parser");
    }
}
