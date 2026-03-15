using fsh_processor;
using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester.Sushi;

/// <summary>
/// Shared utilities for SUSHI compatibility tests.
/// These tests are ported from the SUSHI compiler's test suite to verify
/// that fsh-processor parses FSH in a SUSHI-compatible way.
/// Source: https://github.com/FHIR/sushi/tree/main/test/import
///
/// Key model differences vs SUSHI:
/// - SUSHI resolves aliases in rules; fsh-processor keeps alias names as-is.
/// - Profile.Parent/Id/Title/Description are Metadata? (use .Value); other entities use string?.
/// - SourcePosition columns are 0-based (ANTLR convention); SUSHI uses 1-based columns.
/// - fsh-processor does not implement semantic validation (duplicate names, unresolved aliases).
/// </summary>
public static class SushiTestHelper
{
    /// <summary>
    /// Equivalent to SUSHI's importSingleText() + leftAlign().
    /// Trims common leading whitespace, parses, and returns the FshDoc on success
    /// or fails the test on parse error.
    /// </summary>
    public static FshDoc ParseDoc(string input)
    {
        var trimmed = LeftAlign(input);
        var result = FshParser.Parse(trimmed);
        if (result is ParseResult.Failure failure)
        {
            var msg = string.Join("; ", failure.Errors.Select(e => $"Line {e.Line}: {e.Message}"));
            Assert.Fail($"Parse failed: {msg}");
        }
        return ((ParseResult.Success)result).Document;
    }

    /// <summary>
    /// Parses input and expects a parse failure.
    /// </summary>
    public static bool ParseExpectFailure(string input)
    {
        var trimmed = LeftAlign(input);
        var result = FshParser.Parse(trimmed);
        return result is ParseResult.Failure;
    }

    // --- Entity helpers ---

    public static Profile GetProfile(FshDoc doc, string name)
    {
        var p = doc.Entities.OfType<Profile>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(p, $"Profile '{name}' not found");
        return p;
    }

    public static List<Profile> GetProfiles(FshDoc doc) =>
        doc.Entities.OfType<Profile>().ToList();

    public static fsh_processor.Models.Extension GetExtension(FshDoc doc, string name)
    {
        var e = doc.Entities.OfType<fsh_processor.Models.Extension>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(e, $"Extension '{name}' not found");
        return e;
    }

    public static List<fsh_processor.Models.Extension> GetExtensions(FshDoc doc) =>
        doc.Entities.OfType<fsh_processor.Models.Extension>().ToList();

    public static Instance GetInstance(FshDoc doc, string name)
    {
        var i = doc.Entities.OfType<Instance>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(i, $"Instance '{name}' not found");
        return i;
    }

    public static List<Instance> GetInstances(FshDoc doc) =>
        doc.Entities.OfType<Instance>().ToList();

    public static Invariant GetInvariant(FshDoc doc, string name)
    {
        var inv = doc.Entities.OfType<Invariant>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(inv, $"Invariant '{name}' not found");
        return inv;
    }

    public static List<Invariant> GetInvariants(FshDoc doc) =>
        doc.Entities.OfType<Invariant>().ToList();

    public static ValueSet GetValueSet(FshDoc doc, string name)
    {
        var vs = doc.Entities.OfType<ValueSet>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(vs, $"ValueSet '{name}' not found");
        return vs;
    }

    public static List<ValueSet> GetValueSets(FshDoc doc) =>
        doc.Entities.OfType<ValueSet>().ToList();

    public static CodeSystem GetCodeSystem(FshDoc doc, string name)
    {
        var cs = doc.Entities.OfType<CodeSystem>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(cs, $"CodeSystem '{name}' not found");
        return cs;
    }

    public static List<CodeSystem> GetCodeSystems(FshDoc doc) =>
        doc.Entities.OfType<CodeSystem>().ToList();

    public static Mapping GetMapping(FshDoc doc, string name)
    {
        var m = doc.Entities.OfType<Mapping>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(m, $"Mapping '{name}' not found");
        return m;
    }

    public static List<Mapping> GetMappings(FshDoc doc) =>
        doc.Entities.OfType<Mapping>().ToList();

    public static RuleSet GetRuleSet(FshDoc doc, string name)
    {
        var rs = doc.Entities.OfType<RuleSet>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(rs, $"RuleSet '{name}' not found");
        return rs;
    }

    public static List<RuleSet> GetRuleSets(FshDoc doc) =>
        doc.Entities.OfType<RuleSet>().ToList();

    public static Logical GetLogical(FshDoc doc, string name)
    {
        var l = doc.Entities.OfType<Logical>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(l, $"Logical '{name}' not found");
        return l;
    }

    public static List<Logical> GetLogicals(FshDoc doc) =>
        doc.Entities.OfType<Logical>().ToList();

    public static fsh_processor.Models.Resource GetResource(FshDoc doc, string name)
    {
        var r = doc.Entities.OfType<fsh_processor.Models.Resource>().FirstOrDefault(x => x.Name == name);
        Assert.IsNotNull(r, $"Resource '{name}' not found");
        return r;
    }

    public static List<fsh_processor.Models.Resource> GetResources(FshDoc doc) =>
        doc.Entities.OfType<fsh_processor.Models.Resource>().ToList();

    public static Alias? GetAlias(FshDoc doc, string name) =>
        doc.Entities.OfType<Alias>().FirstOrDefault(a => a.Name == name);

    public static List<Alias> GetAliases(FshDoc doc) =>
        doc.Entities.OfType<Alias>().ToList();

    // --- Rule assertion helpers ---

    public static CardRule AssertCardRule(FshRule rule, string path, string cardinality)
    {
        Assert.IsInstanceOfType<CardRule>(rule, $"Expected CardRule at '{path}'");
        var r = (CardRule)rule;
        Assert.AreEqual(path, r.Path, "CardRule.Path");
        Assert.AreEqual(cardinality, r.Cardinality, "CardRule.Cardinality");
        return r;
    }

    public static FlagRule AssertFlagRule(FshRule rule, string path, params string[] flags)
    {
        Assert.IsInstanceOfType<FlagRule>(rule, $"Expected FlagRule at '{path}'");
        var r = (FlagRule)rule;
        Assert.AreEqual(path, r.Path, "FlagRule.Path");
        CollectionAssert.AreEquivalent(flags, r.Flags.ToArray(), "FlagRule.Flags");
        return r;
    }

    public static OnlyRule AssertOnlyRule(FshRule rule, string path, params string[] types)
    {
        Assert.IsInstanceOfType<OnlyRule>(rule, $"Expected OnlyRule at '{path}'");
        var r = (OnlyRule)rule;
        Assert.AreEqual(path, r.Path, "OnlyRule.Path");
        CollectionAssert.AreEqual(types, r.TargetTypes.ToArray(), "OnlyRule.TargetTypes");
        return r;
    }

    public static ValueSetRule AssertBindingRule(FshRule rule, string path, string valueSet, string? strength = null)
    {
        Assert.IsInstanceOfType<ValueSetRule>(rule, $"Expected ValueSetRule at '{path}'");
        var r = (ValueSetRule)rule;
        Assert.AreEqual(path, r.Path, "ValueSetRule.Path");
        Assert.AreEqual(valueSet, r.ValueSetName, "ValueSetRule.ValueSetName");
        if (strength != null)
        {
            // fsh-processor stores Strength with parentheses e.g. "(extensible)"; SUSHI strips them.
            var normalizedStrength = r.Strength?.Trim('(', ')');
            Assert.AreEqual(strength, normalizedStrength, "ValueSetRule.Strength");
        }
        return r;
    }

    public static ObeysRule AssertObeysRule(FshRule rule, string path, params string[] invariants)
    {
        Assert.IsInstanceOfType<ObeysRule>(rule, $"Expected ObeysRule at '{path}'");
        var r = (ObeysRule)rule;
        // fsh-processor stores Path as null when there is no path; treat null as equivalent to "".
        Assert.AreEqual(path ?? string.Empty, r.Path ?? string.Empty, "ObeysRule.Path");
        CollectionAssert.AreEqual(invariants, r.InvariantNames.ToArray(), "ObeysRule.InvariantNames");
        return r;
    }

    public static ContainsRule AssertContainsRule(FshRule rule, string path, params string[] itemNames)
    {
        Assert.IsInstanceOfType<ContainsRule>(rule, $"Expected ContainsRule at '{path}'");
        var r = (ContainsRule)rule;
        Assert.AreEqual(path, r.Path, "ContainsRule.Path");
        CollectionAssert.AreEqual(itemNames, r.Items.Select(i => i.Name).ToArray(), "ContainsRule items");
        return r;
    }

    public static CaretValueRule AssertCaretValueRule(FshRule rule, string path, string caretPath)
    {
        Assert.IsInstanceOfType<CaretValueRule>(rule, $"Expected CaretValueRule at '{path}' ^{caretPath}");
        var r = (CaretValueRule)rule;
        // fsh-processor stores Path as null when there is no path; treat null as equivalent to "".
        Assert.AreEqual(path ?? string.Empty, r.Path ?? string.Empty, "CaretValueRule.Path");
        // fsh-processor stores CaretPath with leading "^" (e.g. "^short"); SUSHI strips it.
        Assert.AreEqual(caretPath, r.CaretPath?.TrimStart('^'), "CaretValueRule.CaretPath");
        return r;
    }

    public static InsertRule AssertInsertRule(FshRule rule, string path, string ruleSetRef, string[]? parameters = null)
    {
        Assert.IsInstanceOfType<InsertRule>(rule, $"Expected InsertRule at '{path}'");
        var r = (InsertRule)rule;
        // fsh-processor stores Path as null when there is no path; treat null as equivalent to "".
        Assert.AreEqual(path ?? string.Empty, r.Path ?? string.Empty, "InsertRule.Path");
        Assert.AreEqual(ruleSetRef, r.RuleSetReference, "InsertRule.RuleSetReference");
        if (parameters != null)
            CollectionAssert.AreEqual(parameters, r.Parameters.ToArray(), "InsertRule.Parameters");
        return r;
    }

    public static FixedValueRule AssertFixedValueRule(FshRule rule, string path)
    {
        Assert.IsInstanceOfType<FixedValueRule>(rule, $"Expected FixedValueRule at '{path}'");
        var r = (FixedValueRule)rule;
        Assert.AreEqual(path, r.Path, "FixedValueRule.Path");
        return r;
    }

    public static PathRule AssertPathRule(FshRule rule, string path)
    {
        Assert.IsInstanceOfType<PathRule>(rule, $"Expected PathRule at '{path}'");
        var r = (PathRule)rule;
        Assert.AreEqual(path, r.Path, "PathRule.Path");
        return r;
    }

    // --- leftAlign equivalent ---

    /// <summary>
    /// Removes common leading whitespace from a multiline string.
    /// Equivalent to SUSHI's leftAlign() utility used in tests.
    /// </summary>
    public static string LeftAlign(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var lines = input.Split('\n');

        // Skip leading/trailing blank lines
        int start = 0;
        while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start]))
            start++;
        int end = lines.Length - 1;
        while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
            end--;

        if (start > end) return string.Empty;

        lines = lines[start..(end + 1)];

        // Find minimum indentation of all non-empty lines
        int minIndent = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        var stripped = lines.Select(l =>
            l.Length >= minIndent ? l[minIndent..] : l.TrimStart());

        return string.Join("\n", stripped) + "\n";
    }
}
