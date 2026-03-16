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
    public static DataType? ToDataType(FshValue? value, ModelInspector? inspector = null) =>
        value switch
        {
            StringValue sv => new FhirString(sv.Value),
            NumberValue nv => new FhirDecimal(nv.Value),
            BooleanValue bv => new FhirBoolean(bv.Value),
            DateTimeValue dtv => new FhirDateTime(dtv.Value),
            TimeValue tv => new Time(tv.Value),
            FshCode c => new FhirCode(c.Value.TrimStart('#')),
            FshQuantity q => ToQuantity(q),
            RegexValue rv => new FhirString(rv.Pattern),
            Reference r => new ResourceReference(r.Type),
            FshCanonical can => new FhirCanonical(can.Version is null ? can.Url : $"{can.Url}|{can.Version}"),
            FshCodeableReference cr => new FhirCodeableReference { Reference = new ResourceReference(cr.Type) },
            FshRatio r => CreateRatio(r, inspector),
            _ => null
        };

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
    public static DataType? ToDataTypeForCaretPath(FshValue? value, string caretPath, ModelInspector? inspector = null) =>
        caretPath switch
        {
            // Integer caret paths (e.g. ^min, ^max on ElementDefinition constraints)
            "^min" when value is NumberValue nv2 => new Integer((int)nv2.Value),
            _ => ToDataType(value, inspector)
        };

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

    private static Hl7.Fhir.Model.Quantity ToQuantity(FshQuantity q) =>
        new()
        {
            Value = q.Value,
            Unit = q.Unit
        };
}
