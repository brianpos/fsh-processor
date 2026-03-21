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
    /// <param name="aliasResolver">
    /// Optional function that resolves FSH alias names in system-qualified codes (e.g.
    /// <c>$m49.htm</c> → <c>http://unstats.un.org/unsd/methods/m49/m49.htm</c>).
    /// </param>
    /// <returns>
    /// <c>true</c> when a matching property was found and set; <c>false</c> when the property
    /// does not exist in the model or the value type is incompatible, in which case the caller
    /// should fall back (e.g. to an extension).
    /// </returns>
    public static bool TrySet(Base target, string elementName, FshValue? fshValue, ModelInspector? inspector = null, Func<string, string>? aliasResolver = null)
    {
        if (fshValue is null) return false;

        var activeInspector = inspector ?? _conformanceFallback.Value;
        var classMap = activeInspector.FindClassMapping(target.GetType());
        if (classMap is null) return false;

        var propMap = classMap.FindMappedElementByName(elementName);
        if (propMap is null) return false;

        var converted = ConvertValue(fshValue, propMap.ImplementingType, activeInspector, aliasResolver);
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
        Base target, string elementName, int index, FshValue? fshValue, ModelInspector? inspector = null, Func<string, string>? aliasResolver = null)
    {
        if (fshValue is null) return false;

        var activeInspector = inspector ?? _conformanceFallback.Value;
        var classMap = activeInspector.FindClassMapping(target.GetType());
        if (classMap is null) return false;

        var propMap = classMap.FindMappedElementByName(elementName);
        if (propMap is null) return false;

        var converted = ConvertValue(fshValue, propMap.ImplementingType, activeInspector, aliasResolver);
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
    /// Attempts to set a potentially compound (dot-separated) caret path on a FHIR object,
    /// supporting soft-index notation (<c>[+]</c>, <c>[=]</c>, <c>[N]</c>) and URL-keyed
    /// extension navigation (<c>extension[$alias]</c>) for collection navigation.
    /// Simple (single-segment, no brackets) paths are forwarded to <see cref="TrySet"/>.
    /// </summary>
    /// <param name="target">Root FHIR object to start navigation from.</param>
    /// <param name="compoundPath">
    /// Dot-separated path, e.g. <c>"context.type"</c>, <c>"context[+].type"</c>,
    /// <c>"slicing.discriminator.path"</c>, <c>"binding.description"</c>,
    /// <c>"extension[$alias].valueCode"</c>.
    /// </param>
    /// <param name="fshValue">The FSH value to set at the leaf segment.</param>
    /// <param name="softIndexState">
    /// Mutable dictionary tracking the current soft-index counter per path prefix.
    /// Pass the same instance across sequential rule applications so <c>[+]</c>/<c>[=]</c>
    /// pairs work correctly.
    /// </param>
    /// <param name="inspector">Version-specific model inspector.</param>
    /// <param name="aliasResolver">FSH alias resolver.</param>
    /// <returns><c>true</c> when the value was set; <c>false</c> otherwise.</returns>
    public static bool TrySetCompound(
        Base target,
        string compoundPath,
        FshValue? fshValue,
        Dictionary<string, int> softIndexState,
        ModelInspector? inspector = null,
        Func<string, string>? aliasResolver = null)
    {
        if (fshValue is null) return false;

        // Fast path: no compound navigation needed.
        if (!compoundPath.Contains('.') && !compoundPath.Contains('['))
            return TrySet(target, compoundPath, fshValue, inspector, aliasResolver);

        var activeInspector = inspector ?? _conformanceFallback.Value;

        // Split at the first dot outside brackets to get head segment and the remaining tail.
        var dotIndex = FindFirstDotOutsideBrackets(compoundPath);
        if (dotIndex < 0)
        {
            // No dot — has bracket notation only (e.g. "context[+]"), no leaf property.
            return false;
        }

        var headSegment = compoundPath[..dotIndex];
        var tailPath    = compoundPath[(dotIndex + 1)..];

        // Navigate into the child indicated by headSegment.
        var next = NavigateToChild(target, headSegment, softIndexState, activeInspector, create: true, aliasResolver);
        if (next is not Base nextBase) return false;

        // Recursively set the tail path; if still compound, recurse.
        if (tailPath.Contains('.') || tailPath.Contains('['))
            return TrySetCompound(nextBase, tailPath, fshValue, softIndexState, activeInspector, aliasResolver);

        // Leaf segment: try plain TrySet first, then choice-type fallback.
        if (TrySet(nextBase, tailPath, fshValue, activeInspector, aliasResolver)) return true;
        return TrySetChoiceTypeLeaf(nextBase, tailPath, fshValue!, activeInspector, aliasResolver);
    }

    /// <summary>
    /// Returns the index of the first <c>'.'</c> that is not inside square brackets,
    /// or <c>-1</c> if no such dot exists.
    /// </summary>
    private static int FindFirstDotOutsideBrackets(string path)
    {
        int depth = 0;
        for (int i = 0; i < path.Length; i++)
        {
            switch (path[i])
            {
                case '[': depth++; break;
                case ']': if (depth > 0) depth--; break;
                case '.' when depth == 0: return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Navigates one path segment on <paramref name="parent"/> and returns the child object.
    /// Supports:
    /// <list type="bullet">
    ///   <item><description><c>name</c>  — scalar: get existing value or create one; collection: use last element or first.</description></item>
    ///   <item><description><c>name[+]</c> — append a new element to the collection and track its index in <paramref name="softIndexState"/>.</description></item>
    ///   <item><description><c>name[=]</c> — reuse the last index tracked in <paramref name="softIndexState"/> for the same name.</description></item>
    ///   <item><description><c>name[N]</c> — use element at explicit integer index N.</description></item>
    ///   <item><description><c>extension[$alias]</c> — find or create the extension whose URL matches the resolved alias.</description></item>
    /// </list>
    /// </summary>
    private static object? NavigateToChild(
        Base parent,
        string segment,
        Dictionary<string, int> softIndexState,
        ModelInspector activeInspector,
        bool create,
        Func<string, string>? aliasResolver = null)
    {
        // Detect bracket notation.
        var bracketStart = segment.IndexOf('[');
        string baseName;
        string? indexToken;

        if (bracketStart >= 0)
        {
            baseName   = segment[..bracketStart];
            var bracketEnd = segment.IndexOf(']', bracketStart);
            indexToken = bracketEnd > bracketStart
                ? segment[(bracketStart + 1)..bracketEnd]
                : null;
        }
        else
        {
            baseName   = segment;
            indexToken = null;
        }

        var classMap = activeInspector.FindClassMapping(parent.GetType());
        if (classMap is null) return null;

        var propMap = classMap.FindMappedElementByName(baseName);
        if (propMap is null) return null;

        if (propMap.IsCollection)
        {
            var list = propMap.GetValue(parent) as System.Collections.IList;

            // URL-keyed extension navigation: extension[$alias] or extension[urlString]
            // When the index is neither a soft-index (+/=) nor an integer, it's treated as
            // a URL/alias key to find an extension by its url property.
            if (indexToken != null
                && indexToken != "+"
                && indexToken != "="
                && !int.TryParse(indexToken, out _))
            {
                var resolvedUrl = aliasResolver != null ? aliasResolver(indexToken) : indexToken;

                // Try to find an existing Extension with this URL.
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item is Hl7.Fhir.Model.Extension ext && ext.Url == resolvedUrl)
                            return ext;
                    }
                }

                if (!create) return null;

                // Create a new extension with the resolved URL.
                if (list is null)
                {
                    var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
                    list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                    propMap.SetValue(parent, list);
                }
                var newExt = new Hl7.Fhir.Model.Extension { Url = resolvedUrl };
                list.Add(newExt);
                return newExt;
            }

            int targetIndex;
            if (indexToken == "+")
            {
                // Append a new element; record new index.
                if (list is null)
                {
                    var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
                    list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                    propMap.SetValue(parent, list);
                }
                var newItem = Activator.CreateInstance(propMap.ImplementingType)!;
                list.Add(newItem);
                targetIndex = list.Count - 1;
                softIndexState[baseName] = targetIndex;
                return newItem;
            }
            else if (indexToken == "=")
            {
                // Reuse last index for this name.
                softIndexState.TryGetValue(baseName, out targetIndex);
            }
            else if (indexToken != null && int.TryParse(indexToken, out var explicitIdx))
            {
                targetIndex = explicitIdx;
            }
            else
            {
                // No index: use index 0 (get-or-create).
                targetIndex = 0;
            }

            if (list is null)
            {
                if (!create) return null;
                var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
                list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                propMap.SetValue(parent, list);
            }

            while (list.Count <= targetIndex)
                list.Add(Activator.CreateInstance(propMap.ImplementingType)!);

            return list[targetIndex];
        }
        else
        {
            // Non-collection property: get or create.
            var current = propMap.GetValue(parent);
            if (current is null && create)
            {
                current = Activator.CreateInstance(propMap.ImplementingType);
                if (current is not null)
                    propMap.SetValue(parent, current);
            }
            return current;
        }
    }

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
        Base target, string elementName, FshValue fshValue, ModelInspector inspector, Func<string, string>? aliasResolver = null)
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
            var suffixType = inspector.FindClassMapping(typeSuffix);
            if (suffixType is null) continue;

            // The base must be a mapped property on the target class.
            var propMap = classMap.FindMappedElementByName(baseName);
            if (propMap is null) continue;

            // Produce a concrete DataType from the FSH value.
            var dataType = AdaptToTargetType(FhirValueMapper.ToDataType(fshValue, inspector, aliasResolver), suffixType.NativeType);
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

    private static object? ConvertValue(FshValue fshValue, Type targetType, ModelInspector inspector, Func<string, string>? aliasResolver = null)
    {
        // Plain System.String property — e.g. Extension.Url in the Firely SDK is declared as a
        // raw C# string rather than a FHIR PrimitiveType, so it has no string(string) constructor
        // and CreatePrimitive returns null.  Handle it here before the FHIR-DataType switch.
        if (targetType == typeof(string))
        {
            // NameValue: `$alias` used without a `#code` suffix is parsed as a name by the FSH
            // grammar (not as a Code token).  For string targets such as Extension.Url, resolve
            // the alias to its URL and return the resulting string directly — the raw C# string
            // property does not need URI percent-encoding (unlike FhirUri/FhirUrl primitives).
            if (fshValue is NameValue nameVal)
                return aliasResolver?.Invoke(nameVal.Value) ?? nameVal.Value;

            return GetStringFromFshValue(fshValue);
        }

        // Base64Binary — the Firely SDK stores binary data as byte[] and its only non-default
        // constructor takes byte[].  A FSH string value is treated as a base64-encoded string.
        if (targetType == typeof(Base64Binary) && fshValue is StringValue b64sv)
        {
            try { return new Base64Binary(Convert.FromBase64String(b64sv.Value)); }
            catch (FormatException) { return null; }
        }

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
            // For FshCode: extract the code-only part (strip system and leading #) for
            // primitive targets (FhirCode, FhirString, …).  When that fails (e.g. target is
            // CodeableConcept), fall through to ToDataType which produces a Coding that
            // AdaptToTargetType can then wrap in a CodeableConcept.
            FshCode c        => CreatePrimitive(targetType, FhirValueMapper.SplitCodeValue(c.Value).Code)
                                ?? AdaptToTargetType(FhirValueMapper.ToDataType(c, inspector, aliasResolver), targetType),
            BooleanValue bv  => targetType == typeof(FhirBoolean) ? new FhirBoolean(bv.Value) : null,
            NumberValue nv   => CreateNumericPrimitive(targetType, nv.Value),
            _                => AdaptToTargetType(FhirValueMapper.ToDataType(fshValue, inspector), targetType)
        };
    }

    // URI-backed FHIR primitive types whose values must be RFC 3986–normalised
    // before construction so that e.g. choice-type markers like [x] serialise as %5Bx%5D.
    private static readonly HashSet<Type> _uriTypes =
    [
        typeof(FhirUri), typeof(FhirUrl), typeof(Hl7.Fhir.Model.Canonical), typeof(Oid), typeof(Uuid)
    ];

    /// <summary>
    /// Creates a FHIR PrimitiveType instance from a string value using the type's
    /// <c>(string)</c> constructor (handles <see cref="FhirString"/>, <see cref="Markdown"/>,
    /// <see cref="FhirUri"/>, <see cref="FhirUrl"/>, etc.).
    /// URI-typed targets are normalised via <see cref="NormalizeUri"/> before construction.
    /// </summary>
    private static object? CreatePrimitive(Type targetType, string strValue)
    {
        if (_uriTypes.Contains(targetType))
            strValue = NormalizeUri(strValue);
        var ctor = targetType.GetConstructor([typeof(string)]);
        return ctor?.Invoke([strValue]);
    }

    /// <summary>
    /// Returns an RFC 3986–compliant URI string by percent-encoding characters that
    /// are invalid unescaped in URI path segments.
    /// <para>
    /// <c>[</c> and <c>]</c> are the primary targets: they appear in FHIR canonical URLs
    /// as choice-type markers (e.g. <c>versionAlgorithm[x]</c>) but are not valid
    /// unescaped in path segments per RFC 3986.  Already-encoded sequences such as
    /// <c>%5B</c> are left unchanged — only literal bracket characters are encoded.
    /// </para>
    /// <para>
    /// <see cref="Uri.AbsoluteUri"/> is intentionally avoided here: on .NET it does not
    /// encode <c>[</c>/<c>]</c> in paths, and it normalises bare-host URIs by appending
    /// a trailing slash (<c>http://loinc.org</c> → <c>http://loinc.org/</c>).
    /// </para>
    /// </summary>
    private static string NormalizeUri(string url) =>
        url.Replace("[", "%5B").Replace("]", "%5D");

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
            // Extract code-only part (strip system prefix and leading #).
            FshCode c      => FhirValueMapper.SplitCodeValue(c.Value).Code,
            _              => null
        };

    /// <summary>
    /// Returns <paramref name="converted"/> when it is already assignment-compatible with
    /// <paramref name="targetType"/>.  When the types differ but both are string-backed FHIR
    /// primitive types (e.g. <see cref="Canonical"/> → <see cref="FhirUri"/>), the raw string
    /// value is extracted and used to construct the correct target primitive.
    /// A <see cref="Coding"/> is wrapped in a <see cref="CodeableConcept"/> when the target
    /// type is <see cref="CodeableConcept"/>.
    /// Returns <c>null</c> when no adaptation is possible.
    /// </summary>
    private static object? AdaptToTargetType(DataType? converted, Type targetType)
    {
        if (converted is null) return null;
        if (targetType.IsAssignableFrom(converted.GetType())) return converted;

        // Coding → CodeableConcept wrapping (FSH spec: code assigned to a CodeableConcept
        // property creates a CodeableConcept with one Coding element).
        if (converted is Coding coding && targetType == typeof(CodeableConcept))
            return new CodeableConcept { Coding = [coding] };

        // Quantity sub-types (Duration, Age, Distance, Count, MoneyQuantity, SimpleQuantity) —
        // ToDataType always produces a plain Hl7.Fhir.Model.Quantity; copy its fields into a
        // new instance of the requested concrete sub-type so that e.g. `valueDuration = 200 'a'`
        // produces a Duration rather than a plain Quantity.
        if (converted is Hl7.Fhir.Model.Quantity sourceQty
            && converted.GetType().IsAssignableFrom(targetType))
        {
            if (Activator.CreateInstance(targetType) is Hl7.Fhir.Model.Quantity subQty)
            {
                subQty.Value = sourceQty.Value;
                subQty.Comparator = sourceQty.Comparator;
                subQty.Unit = sourceQty.Unit;
                subQty.System = sourceQty.System;
                subQty.Code = sourceQty.Code;
                return subQty;
            }
        }

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
