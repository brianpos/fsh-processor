using System.Reflection;
using fsh_processor.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using FhirExtension = Hl7.Fhir.Model.Extension;
using FshCode = fsh_processor.Models.Code;

namespace fsh_compiler;

/// <summary>
/// Uses the Firely SDK's <see cref="ModelInspector"/> to dynamically set caret-value properties
/// on FHIR <see cref="Base"/> instances, eliminating the need for hard-coded per-property switch
/// statements.  Any property defined in the FHIR conformance model is supported automatically.
/// </summary>
/// <remarks>
/// <para>
/// The inspector is built once from the assembly that contains <see cref="StructureDefinition"/>
/// (i.e. <c>Hl7.Fhir.Conformance</c>), covering all conformance types including
/// <see cref="ElementDefinition"/>, <see cref="ValueSet"/>, and <see cref="CodeSystem"/>.
/// </para>
/// <para>
/// <see cref="TrySet"/> looks up the FHIR element name via
/// <see cref="ClassMapping.FindMappedElementByName"/>, converts the FSH value to the correct
/// Firely primitive type using <see cref="PropertyMapping.ImplementingType"/>, and calls
/// <see cref="PropertyMapping.SetValue"/> on the target instance.
/// </para>
/// </remarks>
public static class FhirCaretValueWriter
{
    // Built once; covers StructureDefinition, ElementDefinition, ValueSet, CodeSystem, etc.
    private static readonly ModelInspector _inspector =
        ModelInspector.ForAssembly(typeof(StructureDefinition).Assembly);

    /// <summary>
    /// Attempts to set <paramref name="elementName"/> on <paramref name="target"/> using the
    /// value from <paramref name="fshValue"/>.
    /// </summary>
    /// <param name="target">The FHIR resource or element to update.</param>
    /// <param name="elementName">FHIR element name, e.g. <c>"publisher"</c> or <c>"mustSupport"</c>.</param>
    /// <param name="fshValue">The FSH value to set.</param>
    /// <returns>
    /// <c>true</c> when a matching property was found in the model and a compatible value was
    /// produced; <c>false</c> when the property does not exist in the model or the value type
    /// is incompatible, in which case the caller should fall back (e.g. to an extension).
    /// </returns>
    public static bool TrySet(Base target, string elementName, FshValue? fshValue)
    {
        if (fshValue is null) return false;

        var classMap = _inspector.FindClassMapping(target.GetType());
        if (classMap is null) return false;

        var propMap = classMap.FindMappedElementByName(elementName);
        if (propMap is null) return false;

        var converted = ConvertValue(fshValue, propMap.ImplementingType);
        if (converted is null) return false;

        propMap.SetValue(target, converted);
        return true;
    }

    // ─── Value conversion ────────────────────────────────────────────────────

    private static object? ConvertValue(FshValue fshValue, Type targetType)
    {
        // Code<TEnum> — parse the string as the enum member name
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Code<>))
        {
            var enumType = targetType.GetGenericArguments()[0];
            var strVal = GetStringFromFshValue(fshValue);
            if (strVal is null) return null;
            if (Enum.TryParse(enumType, strVal, ignoreCase: true, out var enumValue))
                return Activator.CreateInstance(targetType, enumValue);
            return null;
        }

        return fshValue switch
        {
            StringValue sv => CreatePrimitive(targetType, sv.Value),
            FshCode c => CreatePrimitive(targetType, c.Value.TrimStart('#')),
            BooleanValue bv => targetType == typeof(FhirBoolean) ? new FhirBoolean(bv.Value) : null,
            NumberValue nv => CreateNumericPrimitive(targetType, nv.Value),
            _ => FhirValueMapper.ToDataType(fshValue)
        };
    }

    /// <summary>Creates a FHIR PrimitiveType instance from a string, using the type's
    /// <c>(string)</c> constructor via reflection (handles <see cref="FhirString"/>,
    /// <see cref="Markdown"/>, <see cref="FhirUri"/>, <see cref="FhirUrl"/>, etc.).</summary>
    private static object? CreatePrimitive(Type targetType, string strValue)
    {
        var ctor = targetType.GetConstructor([typeof(string)]);
        return ctor?.Invoke([strValue]);
    }

    /// <summary>Creates a numeric FHIR PrimitiveType instance
    /// (<see cref="Integer"/>, <see cref="UnsignedInt"/>, <see cref="PositiveInt"/>,
    /// <see cref="Integer64"/>, <see cref="FhirDecimal"/>).</summary>
    private static object? CreateNumericPrimitive(Type targetType, decimal value)
    {
        if (targetType == typeof(Integer) || targetType == typeof(UnsignedInt) || targetType == typeof(PositiveInt))
        {
            var ctor = targetType.GetConstructor([typeof(int?)]);
            return ctor?.Invoke([(int?)((int)value)]) ?? Activator.CreateInstance(targetType, (int)value);
        }
        if (targetType == typeof(Integer64))
            return new Integer64((long)value);
        if (targetType == typeof(FhirDecimal))
            return new FhirDecimal(value);
        return null;
    }

    private static string? GetStringFromFshValue(FshValue fshValue) =>
        fshValue switch
        {
            StringValue sv => sv.Value,
            FshCode c => c.Value.TrimStart('#'),
            _ => null
        };
}
