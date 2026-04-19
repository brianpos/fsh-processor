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
        {
            // When expanding into an Instance context, re-parse the ruleset body using an
            // Instance wrapper so that rules become InstanceRule types (e.g.
            // InstanceFixedValueRule) instead of SdRule types (e.g. FixedValueRule).
            // Without this, non-parameterized rulesets like `hidden` would produce
            // FixedValueRule objects that are filtered out by OfType<InstanceRule>() in
            // ApplyInstanceRules, resulting in the expansion silently doing nothing.
            if (useInstanceWrapper && !string.IsNullOrWhiteSpace(ruleSet.UnparsedContent))
                return ResolveParameterized(ruleSet, [], context, useInstanceWrapper: true);

            return ruleSet.Rules;
        }

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

        // Non-parameterized rulesets called with useInstanceWrapper: re-parse as Instance
        // without any substitution so that the rules become InstanceRule types.
        if (paramNames.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(ruleSet.UnparsedContent))
                return ruleSet.Rules;

            return ReparseAsWrapper(ruleSet.UnparsedContent, useInstanceWrapper, ruleSet.Rules);
        }

        if (parameters.Count == 0)
            return ruleSet.Rules;

        // Collect the raw text of each rule from hidden tokens (preserve source)
        var rawText = BuildRuleSetText(ruleSet, paramNames, parameters);
        if (string.IsNullOrWhiteSpace(rawText))
            return ruleSet.Rules;

        return ReparseAsWrapper(rawText, useInstanceWrapper, ruleSet.Rules);
    }

    /// <summary>
    /// Wraps <paramref name="rawText"/> in a synthetic entity, re-parses it, and returns the
    /// resulting rules.  Falls back to <paramref name="fallback"/> on parse failure.
    /// </summary>
    private static IReadOnlyList<FshRule> ReparseAsWrapper(
        string rawText,
        bool useInstanceWrapper,
        IReadOnlyList<FshRule> fallback)
    {
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
        return fallback;
    }

    private static string BuildRuleSetText(
        RuleSet ruleSet,
        IReadOnlyList<string> paramNames,
        IReadOnlyList<string> paramValues)
    {
        if (ruleSet.UnparsedContent is null) return string.Empty;

        var content = ruleSet.UnparsedContent;
        for (int i = 0; i < paramNames.Count && i < paramValues.Count; i++)
        {
            var placeholder = $"{{{paramNames[i]}}}";
            var value = paramValues[i];

            // When a parameter value contains whitespace and is NOT already a quoted string
            // (i.e. it was originally a [[...]] double-bracket string whose brackets were
            // stripped when the outer insert rule was parsed), substituting it as plain text
            // into a position that will be re-parsed as a raw ruleset parameter causes the
            // FSH lexer to tokenise each word separately, silently dropping the spaces.
            //
            // To preserve whitespace across nested insert expansions:
            //   1. Replace occurrences of "{param}" (inside double-quotes in the template)
            //      with the plain value "value" — the surrounding quotes from the template
            //      make it a valid FSH string.
            //   2. Replace any remaining bare {param} occurrences with [[value]] so that the
            //      re-parse treats it as a DOUBLE_BRACKET_STRING and keeps the whitespace.
            //
            // The quoted replacement (step 1) must run before the bare replacement (step 2)
            // so that already-quoted occurrences are handled correctly and do not receive
            // the extra [[ ]] wrapping.
            //
            // Exception: FSH quoted codes (#"code with spaces") already carry their own
            // quoting via the # prefix followed by a double-quoted string — they are valid
            // FSH tokens and must NOT be wrapped in [[...]] because DOUBLE_BRACKET_STRING is
            // not accepted in code-value positions by the FSH parser.
            if (!value.StartsWith('"') && !value.StartsWith("[[") &&
                !value.StartsWith("#\"") &&
                value.Any(char.IsWhiteSpace))
            {
                content = content.Replace($"\"{placeholder}\"", $"\"{value}\"");
                content = content.Replace(placeholder, $"[[{value}]]");
            }
            else
            {
                content = content.Replace(placeholder, value);
            }
        }
        return content;
    }
}
