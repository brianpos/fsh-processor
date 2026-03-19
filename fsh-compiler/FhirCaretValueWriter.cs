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

        if (propMap.IsCollection)
        {
            // Collection property: append the new value to the existing list (or create one).
            // This matches FSH semantics where a non-indexed caret assignment like
            //   * ^contextInvariant = "..."
            // appends to the collection (equivalent to [+]).
            var list = propMap.GetValue(target) as System.Collections.IList;
            if (list is null)
            {
                var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
                list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                propMap.SetValue(target, list);
            }
            list.Add(converted);
            return true;
        }

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

    /// <summary>
    /// Handles FHIR choice-type element names such as <c>valueDecimal</c>, <c>valueCoding</c>,
    /// or <c>admitReasonCoding</c> (where <c>admitReason</c> is the base property and
    /// <c>Coding</c> is the FHIR DataType suffix).
    /// </summary>
    /// <remarks>
    /// In FHIR, choice-type elements use a <c>[x]</c> suffix in the schema and are serialised
    /// with a type-specific suffix in JSON/XML (e.g. <c>valueCoding</c>, <c>valueDecimal</c>).
    /// The Firely SDK's <see cref="ClassMapping"/> registers the property under the base name
    /// only (e.g. <c>"value"</c>), so a direct lookup with the suffixed name returns <c>null</c>.
    /// <para>
    /// The method scans the element name from right to left, finding each uppercase letter as
    /// a candidate split point where <c>name[i..]</c> is the potential type suffix and
    /// <c>name[..i]</c> is the potential base property name.  The first candidate where both
    /// the suffix is a recognised FHIR DataType (via <see cref="ModelInspector.FindClassMapping"/>)
    /// AND the base name maps to a property on the target class is used.
    /// </para>
    /// <para>
    /// This method is intentionally <c>internal</c> so it can be called from the CodeSystem
    /// compiler path, where choice-type values are expected (e.g. <c>concept.property.value[x]</c>).
    /// It is NOT wired into the general <see cref="TrySet"/>/<see cref="TrySetIndexed"/> path to
    /// avoid incorrectly setting choice-type values for elements that do not allow the given type.
    /// </para>
    /// </remarks>
    internal static bool TrySetChoiceTypeLeaf(
        Base target, string elementName, FshValue fshValue, ModelInspector inspector)
    {
        var classMap = inspector.FindClassMapping(target.GetType());
        if (classMap is null) return false;

        // Scan right-to-left over uppercase letters.  Each uppercase position is a candidate
        // boundary between the base property name and the FHIR DataType suffix.
        // e.g. "admitReasonCoding" → tries R(5) first (suffix "ReasonCoding" – not a DataType),
        //      then C(11) (suffix "Coding" – is a DataType, base "admitReason" is a property) ✓
        // e.g. "valueDateTime"     → tries T(9) (suffix "Time" – is a DataType, but base
        //      "valueDate" is not a property → skip), then D(5) ("DateTime" + "value") ✓
        for (int i = elementName.Length - 1; i >= 1; i--)
        {
            if (!char.IsUpper(elementName[i])) continue;

            var typeSuffix = elementName[i..];
            var baseName   = elementName[..i];

            // The suffix must be a recognised FHIR DataType name.
            if (inspector.FindClassMapping(typeSuffix) is null) continue;

            // The base must be a mapped property on the target class.
            var propMap = classMap.FindMappedElementByName(baseName);
            if (propMap is null) continue;

            // Produce a concrete DataType from the FSH value.
            var dataType = FhirValueMapper.ToDataType(fshValue, inspector);
            if (dataType is null) return false;

            // Verify the concrete type is assignment-compatible with the property's implementing type.
            if (!propMap.ImplementingType.IsAssignableFrom(dataType.GetType())) return false;

            if (propMap.IsCollection)
            {
                var list = propMap.GetValue(target) as System.Collections.IList;
                if (list is null)
                {
                    var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
                    list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                    propMap.SetValue(target, list);
                }
                list.Add(dataType);
                return true;
            }

            propMap.SetValue(target, dataType);
            return true;
        }

        return false;
    }

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
            _                => AdaptToTargetType(FhirValueMapper.ToDataType(fshValue, inspector), targetType)
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

    /// <summary>
    /// Returns <paramref name="converted"/> when it is already assignment-compatible with
    /// <paramref name="targetType"/>.  When the types differ but both are string-backed FHIR
    /// primitive types (e.g. <see cref="Canonical"/> → <see cref="FhirUri"/>), the raw string
    /// value is extracted and used to construct the correct target primitive.  Returns
    /// <c>null</c> when no adaptation is possible.
    /// </summary>
    private static object? AdaptToTargetType(DataType? converted, Type targetType)
    {
        if (converted is null) return null;
        if (targetType.IsAssignableFrom(converted.GetType())) return converted;

        // Both sides are string-backed FHIR primitives — extract the raw value and
        // create the correct target type (e.g. Canonical → FhirUri, FhirUrl → FhirString).
        if (converted is PrimitiveType primitive && primitive.ObjectValue is string rawValue)
        {
            var ctor = targetType.GetConstructor([typeof(string)]);
            if (ctor != null) return ctor.Invoke([rawValue]);
        }

        return null;
    }
}
