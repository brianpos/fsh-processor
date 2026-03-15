using fsh_processor;
using fsh_processor.Models;

namespace fsh_compiler;

/// <summary>
/// Resolves <see cref="InsertRule"/> references by looking up the named <see cref="RuleSet"/>
/// in the compiler context and returning its rules (with parameter substitution when applicable).
/// </summary>
public static class RuleSetResolver
{
    /// <summary>
    /// Resolves an <see cref="InsertRule"/> and returns the expanded <see cref="FshRule"/> list.
    /// Returns an empty list if the referenced <see cref="RuleSet"/> is not found in the context.
    /// </summary>
    public static IReadOnlyList<FshRule> Resolve(InsertRule insertRule, CompilerContext context)
    {
        if (!context.RuleSets.TryGetValue(insertRule.RuleSetReference, out var ruleSet))
            return Array.Empty<FshRule>();

        if (!insertRule.IsParameterized || insertRule.Parameters.Count == 0)
            return ruleSet.Rules;

        return ResolveParameterized(ruleSet, insertRule.Parameters, context);
    }

    private static IReadOnlyList<FshRule> ResolveParameterized(
        RuleSet ruleSet,
        IReadOnlyList<string> parameters,
        CompilerContext context)
    {
        // Build the substitution map: positional parameter placeholders → actual values.
        // FSH parameterized rule sets use {%param%} syntax in their unparsed content.
        // We re-parse via a synthetic Profile wrapper after substitution.
        var paramNames = ruleSet.Parameters.Select(p => p.Value).ToList();
        if (paramNames.Count == 0 || parameters.Count == 0)
            return ruleSet.Rules;

        // Collect the raw text of each rule from hidden tokens (preserve source)
        var rawText = BuildRuleSetText(ruleSet, paramNames, parameters);
        if (string.IsNullOrWhiteSpace(rawText))
            return ruleSet.Rules;

        // Wrap in a synthetic Profile and re-parse to get the substituted rules
        var synthetic = $"Profile: _Synthetic_\nParent: DomainResource\n{rawText}\n";
        var result = FshParser.Parse(synthetic);
        if (result is ParseResult.Success success &&
            success.Document.Entities.OfType<Profile>().FirstOrDefault() is { } p)
        {
            return p.Rules;
        }

        // Fall back to unsubstituted rules on parse error
        return ruleSet.Rules;
    }

    private static string BuildRuleSetText(
        RuleSet ruleSet,
        IReadOnlyList<string> paramNames,
        IReadOnlyList<string> paramValues)
    {
        // TODO: Implement parameter substitution for parameterized rule sets.
        // The RuleSet.UnparsedContent field holds the raw text with {paramName} placeholders.
        // Substitute each placeholder with the corresponding value from paramValues,
        // then re-parse via a synthetic Profile wrapper to produce concrete FshRule instances.
        //
        // For the initial implementation this returns an empty string, causing parameterized
        // InsertRule references to fall back to the unsubstituted rules in ruleSet.Rules.
        if (ruleSet.UnparsedContent is null) return string.Empty;

        var content = ruleSet.UnparsedContent;
        for (int i = 0; i < paramNames.Count && i < paramValues.Count; i++)
        {
            content = content.Replace($"{{{paramNames[i]}}}", paramValues[i]);
        }
        return content;
    }
}
