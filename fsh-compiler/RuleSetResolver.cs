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
    public static IReadOnlyList<FshRule> Resolve(InsertRule insertRule, CompilerContext context) =>
        Resolve(insertRule.RuleSetReference, insertRule.IsParameterized, insertRule.Parameters, context);

    /// <summary>
    /// Resolves a rule set by name and returns the expanded <see cref="FshRule"/> list.
    /// Returns an empty list if the referenced <see cref="RuleSet"/> is not found in the context.
    /// </summary>
    /// <param name="ruleSetReference">The rule set name to look up.</param>
    /// <param name="isParameterized">Whether the insert rule has parameters to substitute.</param>
    /// <param name="parameters">The parameter values for substitution.</param>
    /// <param name="context">The compiler context that holds the rule set registry.</param>
    /// <param name="useInstanceWrapper">
    /// When <c>true</c>, the re-parse synthetic wrapper is an <c>Instance</c> entity so that
    /// the substituted rules are parsed as <see cref="InstanceRule"/> instances rather than
    /// <see cref="SdRule"/> instances.  Use this when expanding a rule set for an Instance.
    /// </param>
    public static IReadOnlyList<FshRule> Resolve(
        string ruleSetReference,
        bool isParameterized,
        IReadOnlyList<string> parameters,
        CompilerContext context,
        bool useInstanceWrapper = false)
    {
        if (!context.RuleSets.TryGetValue(ruleSetReference, out var ruleSet))
            return Array.Empty<FshRule>();

        if (!isParameterized || parameters.Count == 0)
            return ruleSet.Rules;

        return ResolveParameterized(ruleSet, parameters, context, useInstanceWrapper);
    }

    private static IReadOnlyList<FshRule> ResolveParameterized(
        RuleSet ruleSet,
        IReadOnlyList<string> parameters,
        CompilerContext context,
        bool useInstanceWrapper = false)
    {
        // Build the substitution map: positional parameter placeholders → actual values.
        // FSH parameterized rule sets use {%param%} syntax in their unparsed content.
        // We re-parse via a synthetic entity wrapper after substitution.
        var paramNames = ruleSet.Parameters.Select(p => p.Value).ToList();
        if (paramNames.Count == 0 || parameters.Count == 0)
            return ruleSet.Rules;

        // Collect the raw text of each rule from hidden tokens (preserve source)
        var rawText = BuildRuleSetText(ruleSet, paramNames, parameters);
        if (string.IsNullOrWhiteSpace(rawText))
            return ruleSet.Rules;

        // Wrap in a synthetic entity and re-parse to get the substituted rules.
        // Use an Instance wrapper when expanding for an Instance so that paths like
        // `input[+]` are parsed as InstanceRule/InstanceFixedValueRule rather than SdRule.
        // Pass preserveSoftIndices:true so [+]/[=] tokens are preserved for the compiler
        // to resolve against the outer soft-index context (not frozen at [0] during re-parse).
        string synthetic = useInstanceWrapper
            ? $"Instance: _Synthetic_\nInstanceOf: DomainResource\n{rawText}\n"
            : $"Profile: _Synthetic_\nParent: DomainResource\n{rawText}\n";

        var result = FshParser.Parse(synthetic, preserveSoftIndices: true);
        if (result is ParseResult.Success success)
        {
            if (useInstanceWrapper)
            {
                var inst = success.Document.Entities.OfType<Instance>().FirstOrDefault();
                if (inst != null) return inst.Rules;
            }
            else
            {
                var prof = success.Document.Entities.OfType<Profile>().FirstOrDefault();
                if (prof != null) return prof.Rules;
            }
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
