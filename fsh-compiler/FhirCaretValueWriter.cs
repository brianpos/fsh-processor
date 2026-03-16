using System.Reflection;
using fsh_processor.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using FshCode = fsh_processor.Models.Code;

namespace fsh_compiler;

/// <summary>
/// Uses the Firely SDK's <see cref="ModelInspector"/> to dynamically set caret-value properties
/// on FHIR <see cref="Base"/> instances, eliminating the need for hard-coded per-property switch
/// statements.  Any property defined in the caller's FHIR model is supported automatically.
/// </summary>
/// <remarks>
/// <para>
/// The inspector is provided by the caller (typically from a version-specific
/// <c>ModelInfo.ModelInspector</c>).  A lazy fallback built from <c>Hl7.Fhir.Conformance</c>
/// is used when no inspector is supplied.
/// </para>
/// <para>
/// <see cref="TrySet"/> looks up the FHIR element name via
/// <see cref="ClassMapping.FindMappedElementByName"/>, converts the FSH value to the correct
/// Firely primitive type using <see cref="PropertyMapping.ImplementingType"/>, and calls
/// <see cref="PropertyMapping.SetValue"/> on the target instance.
/// FHIR enum literals (e.g. <c>"is-a"</c>, <c>"grouped-by"</c>) are resolved via
/// <see cref="EnumUtility.ParseLiteral"/> so that kebab-case FSH values map correctly to
/// their C# enum counterparts.
/// </para>
/// </remarks>
public static class FhirCaretValueWriter
{
    // Fallback inspector (lazy, only built when no version-specific inspector is supplied).
    private static readonly Lazy<ModelInspector> _conformanceFallback =
        new(() => ModelInspector.ForAssembly(typeof(StructureDefinition).Assembly));

    /// <summary>
    /// Attempts to set <paramref name="elementName"/> on <paramref name="target"/> using the
    /// value from <paramref name="fshValue"/>.
    /// </summary>
    /// <param name="target">The FHIR resource or element to update.</param>
    /// <param name="elementName">
    /// FHIR element name, e.g. <c>"publisher"</c>, <c>"mustSupport"</c>, <c>"status"</c>.
    /// </param>
    /// <param name="fshValue">The FSH value to set.</param>
    /// <param name="inspector">
    /// The <see cref="ModelInspector"/> for the target FHIR version.
    /// Pass the version-specific <c>ModelInfo.ModelInspector</c>; when <c>null</c> the
    /// Conformance-assembly fallback is used.
    /// </param>
    /// <returns>
    /// <c>true</c> when a matching property was found and set; <c>false</c> when the property
    /// does not exist in the model or the value type is incompatible, in which case the caller
    /// should fall back (e.g. to an extension).
    /// </returns>
    public static bool TrySet(Base target, string elementName, FshValue? fshValue, ModelInspector? inspector = null)
    {
        if (fshValue is null) return false;

        var activeInspector = inspector ?? _conformanceFallback.Value;
        var classMap = activeInspector.FindClassMapping(target.GetType());
        if (classMap is null) return false;

        var propMap = classMap.FindMappedElementByName(elementName);
        if (propMap is null) return false;

        var converted = ConvertValue(fshValue, propMap.ImplementingType, activeInspector);
        if (converted is null) return false;

        propMap.SetValue(target, converted);
        return true;
    }

    /// <summary>
    /// Attempts to set an indexed collection element for <paramref name="elementName"/> on
    /// <paramref name="target"/> using the value from <paramref name="fshValue"/>.
    /// When <paramref name="elementName"/> refers to a collection property, the list is grown as
    /// necessary and the element at <paramref name="index"/> is set.  For non-collection
    /// properties, the index is ignored and the value is set directly (same as
    /// <see cref="TrySet"/>).
    /// </summary>
    public static bool TrySetIndexed(
        Base target, string elementName, int index, FshValue? fshValue, ModelInspector? inspector = null)
    {
        if (fshValue is null) return false;

        var activeInspector = inspector ?? _conformanceFallback.Value;
        var classMap = activeInspector.FindClassMapping(target.GetType());
        if (classMap is null) return false;

        var propMap = classMap.FindMappedElementByName(elementName);
        if (propMap is null) return false;

        var converted = ConvertValue(fshValue, propMap.ImplementingType, activeInspector);
        if (converted is null) return false;

        if (!propMap.IsCollection)
        {
            propMap.SetValue(target, converted);
            return true;
        }

        // Ensure the list exists, then set element at the requested index.
        var list = propMap.GetValue(target) as System.Collections.IList;
        if (list is null)
        {
            var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
            list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            propMap.SetValue(target, list);
        }

        while (list.Count <= index)
            list.Add(Activator.CreateInstance(propMap.ImplementingType));

        list[index] = converted;
        return true;
    }

    // ─── Value conversion ────────────────────────────────────────────────────

    private static object? ConvertValue(FshValue fshValue, Type targetType, ModelInspector inspector)
    {
        // Code<TEnum> — use EnumUtility.ParseLiteral so that FHIR kebab-case literals
        // (e.g. "is-a", "grouped-by") are resolved correctly against [EnumLiteral] attributes.
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Code<>))
        {
            var enumType = targetType.GetGenericArguments()[0];
            var literal = GetStringFromFshValue(fshValue);
            if (literal is null) return null;

            var enumValue = EnumUtility.ParseLiteral(literal, enumType, ignoreCase: true);
            if (enumValue is null) return null;

            return Activator.CreateInstance(targetType, enumValue);
        }

        return fshValue switch
        {
            StringValue sv   => CreatePrimitive(targetType, sv.Value),
            FshCode c        => CreatePrimitive(targetType, c.Value.TrimStart('#')),
            BooleanValue bv  => targetType == typeof(FhirBoolean) ? new FhirBoolean(bv.Value) : null,
            NumberValue nv   => CreateNumericPrimitive(targetType, nv.Value),
            _                => FhirValueMapper.ToDataType(fshValue, inspector)
        };
    }

    /// <summary>
    /// Creates a FHIR PrimitiveType instance from a string value using the type's
    /// <c>(string)</c> constructor (handles <see cref="FhirString"/>, <see cref="Markdown"/>,
    /// <see cref="FhirUri"/>, <see cref="FhirUrl"/>, etc.).
    /// </summary>
    private static object? CreatePrimitive(Type targetType, string strValue)
    {
        var ctor = targetType.GetConstructor([typeof(string)]);
        return ctor?.Invoke([strValue]);
    }

    /// <summary>
    /// Creates a numeric FHIR PrimitiveType instance
    /// (<see cref="Integer"/>, <see cref="UnsignedInt"/>, <see cref="PositiveInt"/>,
    /// <see cref="Integer64"/>, <see cref="FhirDecimal"/>).
    /// </summary>
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
            FshCode c      => c.Value.TrimStart('#'),
            _              => null
        };
}
