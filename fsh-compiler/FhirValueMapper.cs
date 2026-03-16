using fsh_processor.Models;
using Hl7.Fhir.Model;
using FhirCode = Hl7.Fhir.Model.Code;
using FhirCanonical = Hl7.Fhir.Model.Canonical;
using FshQuantity = fsh_processor.Models.Quantity;
using FshCode = fsh_processor.Models.Code;
using FshCanonical = fsh_processor.Models.Canonical;

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
    public static DataType? ToDataType(FshValue? value) =>
        value switch
        {
            StringValue sv => new FhirString(sv.Value),
            NumberValue nv => new FhirDecimal(nv.Value),
            BooleanValue bv => new FhirBoolean(bv.Value),
            DateTimeValue dtv => new FhirDateTime(dtv.Value),
            TimeValue tv => new Time(tv.Value),
            FshCode c => new FhirCode(c.Value.TrimStart('#')),
            FshQuantity q => ToQuantity(q),
            // Ratio requires version-specific DataType not available in Hl7.Fhir.Conformance; returns null.
            RegexValue rv => new FhirString(rv.Pattern),
            Reference r => new ResourceReference(r.Type),
            FshCanonical can => new FhirCanonical(can.Version is null ? can.Url : $"{can.Url}|{can.Version}"),
            _ => null
        };

    /// <summary>
    /// Converts a <see cref="FshValue"/> to a Firely <see cref="DataType"/> specifically
    /// for caret-value rules where the target FHIR property type is known.
    /// Falls back to <see cref="ToDataType"/> when no special-casing applies.
    /// </summary>
    public static DataType? ToDataTypeForCaretPath(FshValue? value, string caretPath) =>
        caretPath switch
        {
            // Integer caret paths (e.g. ^min, ^max on ElementDefinition constraints)
            "^min" when value is NumberValue nv2 => new Integer((int)nv2.Value),
            _ => ToDataType(value)
        };

    private static Hl7.Fhir.Model.Quantity ToQuantity(FshQuantity q) =>
        new()
        {
            Value = q.Value,
            Unit = q.Unit
        };
}
