using fsh_processor.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using FhirCode = Hl7.Fhir.Model.Code;
using FhirCanonical = Hl7.Fhir.Model.Canonical;
using FhirCodeableReference = Hl7.Fhir.Model.CodeableReference;
using FshQuantity = fsh_processor.Models.Quantity;
using FshCode = fsh_processor.Models.Code;
using FshCanonical = fsh_processor.Models.Canonical;
using FshRatio = fsh_processor.Models.Ratio;
using FshCodeableReference = fsh_processor.Models.CodeableReference;

namespace fsh_compiler;

/// <summary>
/// Maps FSH <see cref="FshValue"/> instances to the corresponding Firely SDK <see cref="DataType"/>
/// instances for use in <c>fixed[x]</c>, <c>pattern[x]</c>, and caret-value rule assignments.
/// </summary>
public static class FhirValueMapper
{
    /// <summary>
    /// Converts a <see cref="FshValue"/> to a Firely <see cref="DataType"/>.
    /// Returns <c>null</c> when no mapping is defined for the value type.
    /// </summary>
    /// <param name="value">The FSH value to convert.</param>
    /// <param name="inspector">
    /// Optional <see cref="ModelInspector"/> used to dynamically instantiate version-specific
    /// FHIR types such as <c>Ratio</c> that are not available in the shared Conformance assembly.
    /// </param>
    /// <param name="aliasResolver">
    /// Optional function that resolves an FSH alias name (e.g. <c>$m49.htm</c>) to its
    /// canonical URL.  When <c>null</c>, alias names are used as-is.
    /// </param>
    public static DataType? ToDataType(FshValue? value, ModelInspector? inspector = null, Func<string, string>? aliasResolver = null) =>
        value switch
        {
            StringValue sv => new FhirString(sv.Value),
            NumberValue nv => new FhirDecimal(nv.Value),
            BooleanValue bv => new FhirBoolean(bv.Value),
            DateTimeValue dtv => new FhirDateTime(dtv.Value),
            TimeValue tv => new Time(tv.Value),
            FshCode c => CodeToDataType(c, aliasResolver),
            FshQuantity q => ToQuantity(q),
            RegexValue rv => new FhirString(rv.Pattern),
            Reference r => new ResourceReference(r.Type, r.Display),
            FshCanonical can => new FhirCanonical(can.Version is null ? can.Url : $"{can.Url}|{can.Version}"),
            FshCodeableReference cr => new FhirCodeableReference { Reference = new ResourceReference(cr.Type) },
            FshRatio r => CreateRatio(r, inspector),
            _ => null
        };

    /// <summary>
    /// Converts a <see cref="FshCode"/> to the most specific Firely <see cref="DataType"/>
    /// available given the information in the value:
    /// <list type="bullet">
    ///   <item>System-qualified codes (e.g. <c>$m49.htm#001 "World"</c>) produce a
    ///     <see cref="Coding"/> with <c>System</c>, <c>Code</c>, and optionally
    ///     <c>Display</c> populated.</item>
    ///   <item>Bare codes (e.g. <c>#active</c> or <c>active</c>) produce a
    ///     <see cref="FhirCode"/> with the code value only.</item>
    /// </list>
    /// </summary>
    internal static DataType CodeToDataType(FshCode c, Func<string, string>? aliasResolver)
    {
        var (system, code) = SplitCodeValue(c.Value);
        if (system is null)
            return new FhirCode(code);

        var resolvedSystem = aliasResolver?.Invoke(system) ?? system;
        return new Coding
        {
            System = resolvedSystem,
            Code = code,
            Display = c.Display
        };
    }

    /// <summary>
    /// Converts a <see cref="FshValue"/> to a Firely <see cref="DataType"/> specifically
    /// for caret-value rules where the target FHIR property type is known.
    /// Falls back to <see cref="ToDataType"/> when no special-casing applies.
    /// </summary>
    /// <param name="value">The FSH value to convert.</param>
    /// <param name="caretPath">The caret path indicating the target property.</param>
    /// <param name="inspector">
    /// Optional <see cref="ModelInspector"/> forwarded to <see cref="ToDataType"/> for
    /// version-specific type resolution.
    /// </param>
    /// <param name="aliasResolver">
    /// Optional alias resolver forwarded to <see cref="ToDataType"/>.
    /// </param>
    public static DataType? ToDataTypeForCaretPath(FshValue? value, string caretPath, ModelInspector? inspector = null, Func<string, string>? aliasResolver = null) =>
        caretPath switch
        {
            // Integer caret paths (e.g. ^min, ^max on ElementDefinition constraints)
            "^min" when value is NumberValue nv2 => new Integer((int)nv2.Value),
            _ => ToDataType(value, inspector, aliasResolver)
        };

    /// <summary>
    /// Splits a raw FSH code value into an optional system and a code.
    /// <list type="bullet">
    ///   <item><c>#active</c> → (null, <c>"active"</c>)</item>
    ///   <item><c>active</c>  → (null, <c>"active"</c>)</item>
    ///   <item><c>$m49.htm#001</c> → (<c>"$m49.htm"</c>, <c>"001"</c>)</item>
    /// </list>
    /// </summary>
    internal static (string? System, string Code) SplitCodeValue(string rawValue)
    {
        if (rawValue.StartsWith('#'))
            return (null, StripQuotes(rawValue[1..]));

        var hashIdx = rawValue.IndexOf('#');
        return hashIdx >= 0
            ? (rawValue[..hashIdx], StripQuotes(rawValue[(hashIdx + 1)..]))
            : (null, rawValue);
    }

    /// <summary>
    /// Strips surrounding double-quotes from FSH quoted code identifiers such as
    /// <c>"Body Weight"</c> → <c>Body Weight</c>.  Codes without surrounding quotes
    /// are returned unchanged.
    /// </summary>
    private static string StripQuotes(string code) =>
        code.Length >= 2 && code[0] == '"' && code[^1] == '"'
            ? code[1..^1]
            : code;

    /// <summary>
    /// Dynamically creates a version-specific FHIR <c>Ratio</c> instance using the supplied
    /// <see cref="ModelInspector"/> and populates its <c>numerator</c> and <c>denominator</c>
    /// properties from the FSH <see cref="FshRatio"/> value.
    /// Returns <c>null</c> when <paramref name="inspector"/> is <c>null</c> or the
    /// <c>Ratio</c> type is not present in the inspector's model.
    /// </summary>
    private static DataType? CreateRatio(FshRatio fshRatio, ModelInspector? inspector)
    {
        if (inspector is null) return null;

        var ratioMap = inspector.FindClassMapping("Ratio");
        if (ratioMap is null) return null;

        if (Activator.CreateInstance(ratioMap.NativeType) is not DataType ratioInstance) return null;

        var classMap = inspector.FindClassMapping(ratioInstance.GetType());
        // classMap is null only when the inspector has no mapping for the created type,
        // which indicates a model misconfiguration rather than a user error.
        if (classMap is null) return ratioInstance;

        var numerator = RatioPartToQuantity(fshRatio.Numerator);
        if (numerator is not null)
            classMap.FindMappedElementByName("numerator")?.SetValue(ratioInstance, numerator);

        var denominator = RatioPartToQuantity(fshRatio.Denominator);
        if (denominator is not null)
            classMap.FindMappedElementByName("denominator")?.SetValue(ratioInstance, denominator);

        return ratioInstance;
    }

    /// <summary>
    /// Converts a <see cref="RatioPart"/> to a Firely <see cref="Hl7.Fhir.Model.Quantity"/>.
    /// Returns <c>null</c> when the part carries neither a numeric value nor a quantity.
    /// </summary>
    private static Hl7.Fhir.Model.Quantity? RatioPartToQuantity(RatioPart part)
    {
        if (part.QuantityValue is not null)
            return ToQuantity(part.QuantityValue);

        if (part.Value.HasValue)
            return new Hl7.Fhir.Model.Quantity { Value = part.Value };

        return null;
    }

    private static Hl7.Fhir.Model.Quantity ToQuantity(FshQuantity q)
    {
        var unit = q.Unit;

        // FSH UCUM units are wrapped in single quotes (e.g. 'a', 'mg').
        // Strip the quotes and populate Code + System (UCUM canonical URL)
        // rather than the human-readable Unit display field.
        if (unit.Length >= 2 && unit[0] == '\'' && unit[^1] == '\'')
        {
            var code = unit[1..^1];
            return new Hl7.Fhir.Model.Quantity
            {
                Value = q.Value,
                Code = code,
                System = "http://unitsofmeasure.org"
            };
        }

        return new Hl7.Fhir.Model.Quantity
        {
            Value = q.Value,
            Unit = unit
        };
    }
}
