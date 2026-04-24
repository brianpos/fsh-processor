using Hl7.FhirShorthand.Serialization.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using FhirCode = Hl7.Fhir.Model.Code;
using FhirCodeSystem = Hl7.Fhir.Model.CodeSystem;
using FhirExtension = Hl7.Fhir.Model.Extension;
using FhirResource = Hl7.Fhir.Model.Resource;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;
using FshCode = Hl7.FhirShorthand.Serialization.Models.Code;

namespace Hl7.FhirShorthand.Compiler;

/// <summary>
/// Compiles a parsed FSH document (<see cref="FshDoc"/>) into a list of FHIR
/// <see cref="Resource"/> instances using the Firely SDK conformance model.
/// </summary>
public static class FshCompiler
{
    /// <summary>The FSH alias-reference prefix character (e.g. <c>$myAlias</c>).</summary>
    private const char AliasPrefix = '$';
    /// <summary>
    /// Compiles all entities in <paramref name="doc"/> to FHIR resources.
    /// Entities that do not produce a FHIR resource (Alias, RuleSet) are silently skipped.
    /// </summary>
    /// <param name="doc">Parsed FSH document.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <returns>
    /// A <see cref="CompileResult{T}"/> that is either a <c>SuccessResult</c> containing
    /// the list of compiled resources, or a <c>FailureResult</c> listing per-entity errors.
    /// </returns>
    public static CompileResult<List<FhirResource>> Compile(FshDoc doc, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var context = CompilerContext.Build(doc);

        if (opts.AliasOverrides != null)
            foreach (var kvp in opts.AliasOverrides)
                context.Aliases[kvp.Key] = kvp.Value;

        return CompileWithContext([doc], context, opts);
    }

    /// <summary>
    /// Compiles all entities across multiple <paramref name="docs"/> to FHIR resources using a
    /// merged context.  Aliases, rule sets, and invariants defined in any document are visible
    /// to all other documents, enabling multi-file IG compilation.
    /// </summary>
    /// <param name="docs">Parsed FSH documents to compile together.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <returns>
    /// A <see cref="CompileResult{T}"/> that is either a <c>SuccessResult</c> containing
    /// the combined list of compiled resources, or a <c>FailureResult</c> listing per-entity errors.
    /// </returns>
    public static CompileResult<List<FhirResource>> Compile(
        IEnumerable<FshDoc> docs, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var docList = docs.ToList();

        // Build a merged context from all documents so that aliases, rule sets, and
        // invariants defined in any file are visible during compilation of all files.
        var context = new CompilerContext();
        foreach (var doc in docList)
            context.MergeFrom(doc);

        if (opts.AliasOverrides != null)
            foreach (var kvp in opts.AliasOverrides)
                context.Aliases[kvp.Key] = kvp.Value;

        // Pre-scan: register CodeSystem/ValueSet name/id → canonical URL for system name and
        // Canonical() resolution (e.g. TemporaryCodes → .../CodeSystem/temp,
        // QuestionnaireBehaviorConditions → .../ValueSet/formBehaviorConditions).
        foreach (var doc in docList)
        {
            foreach (var entity in doc.Entities)
            {
                if (entity is Hl7.FhirShorthand.Serialization.Models.CodeSystem cs)
                {
                    var csUrl = ResolveUrl(cs.Id ?? cs.Name, opts, "CodeSystem");
                    if (csUrl is null) continue;
                    if (!string.IsNullOrEmpty(cs.Name))
                        context.CodeSystemUrls.TryAdd(cs.Name, csUrl);
                    if (!string.IsNullOrEmpty(cs.Id))
                        context.CodeSystemUrls.TryAdd(cs.Id, csUrl);
                }
                else if (entity is Hl7.FhirShorthand.Serialization.Models.ValueSet vs)
                {
                    var vsUrl = ResolveUrl(vs.Id ?? vs.Name, opts, "ValueSet");
                    if (vsUrl is null) continue;
                    if (!string.IsNullOrEmpty(vs.Name))
                        context.ValueSetUrls.TryAdd(vs.Name, vsUrl);
                    if (!string.IsNullOrEmpty(vs.Id))
                        context.ValueSetUrls.TryAdd(vs.Id, vsUrl);
                }
            }
        }

        // Pre-scan the system zip for ResourceId indexedCanonicals
        IndexResolver(options.Resolver, context);

        return CompileWithContext(docList, context, opts);
    }

    private static void IndexResolver(IResourceResolver resolver, CompilerContext context)
    {
        if (resolver is CachedResolver cr)
        {
            IndexResolver(cr.Source, context);
        }
        if (resolver is MultiResolver mr)
        {
            foreach (var r in mr.Sources)
            {
                IndexResolver(r, context);
            }
        }

        if (resolver is CommonZipSource zs)
        {
            foreach (var summary in zs.ListSummaries())
            {
                var resourceType = summary.GetTypeName();
                var resourceName = summary.GetConformanceName();
                var canonicalUrl = summary.GetConformanceCanonicalUrl();
                var key = $"{resourceType}#{resourceName}";
                if (!string.IsNullOrEmpty(canonicalUrl) && !string.IsNullOrEmpty(resourceName))
                {
                    if (!context.CanonicalsFromSpecificationZip.ContainsKey(key))
                        context.CanonicalsFromSpecificationZip.Add(key, canonicalUrl);
                    //else
                    //    Console.WriteLine($"Duplicate Key {key} for {resourceType} - {canonicalUrl} - existing canonical {context.CanonicalsFromSpecificationZip[key]}");
                }
            }
        }
    }

    /// <summary>
    /// Assigns a compilation-order tier to each entity type.
    /// Lower tiers are compiled first.  Within the same tier, entities are further
    /// sorted by intra-tier dependencies (topological sort).
    /// </summary>
    private static int EntityTier(FshEntity entity) => entity switch
    {
        // Tier 0 — context-only entities (no FHIR resource produced, but must be first)
        Alias                                   => 0,
        RuleSet                                 => 0,
        Invariant                               => 0,
        // Tier 1 — CodeSystems (must register canonical URLs before ValueSets)
        Hl7.FhirShorthand.Serialization.Models.CodeSystem         => 1,
        // Tier 2 — ValueSets (may reference CodeSystem URLs)
        Hl7.FhirShorthand.Serialization.Models.ValueSet           => 2,
        // Tier 3 — StructureDefinition producers (Profile, Extension, Logical, Resource)
        Profile                                 => 3,
        Hl7.FhirShorthand.Serialization.Models.Extension          => 3,
        Logical                                 => 3,
        Hl7.FhirShorthand.Serialization.Models.Resource           => 3,
        // Tier 4 — Instances (need compiled SDs for type resolution)
        Hl7.FhirShorthand.Serialization.Models.Instance           => 4,
        // Tier 5 — Mappings (annotate already-compiled SDs)
        Hl7.FhirShorthand.Serialization.Models.Mapping            => 5,
        _                                       => 6
    };

    /// <summary>
    /// Returns the name of the parent entity that <paramref name="entity"/> depends on,
    /// or <c>null</c> when it has no intra-project dependency.
    /// </summary>
    private static string? GetEntityDependency(FshEntity entity) => entity switch
    {
        Profile p                               => p.Parent?.Value,
        Hl7.FhirShorthand.Serialization.Models.Extension ext      => ext.Parent,
        Logical l                               => l.Parent,
        Hl7.FhirShorthand.Serialization.Models.Resource r         => r.Parent,
        Hl7.FhirShorthand.Serialization.Models.Instance inst      => inst.InstanceOf,
        Hl7.FhirShorthand.Serialization.Models.Mapping m          => m.Source,
        _                                       => null
    };

    /// <summary>
    /// Collects all entities from every <paramref name="docs"/> into a single flat list and
    /// sorts them by compilation tier (Alias/RuleSet/Invariant → CodeSystem → ValueSet →
    /// Profile/Extension/Logical/Resource → Instance → Mapping).  Within each tier entities
    /// are topologically sorted by their intra-project dependencies so that a parent is
    /// always compiled before its children.
    /// </summary>
    private static List<FshEntity> SortEntities(IEnumerable<FshDoc> docs, CompilerContext context)
    {
        // Collect all entities from all documents into a flat list.
        var allEntities = docs.SelectMany(d => d.Entities).ToList();

        // Build a name → entity index for dependency resolution.
        var nameToEntity = new Dictionary<string, FshEntity>(StringComparer.Ordinal);
        foreach (var e in allEntities)
            nameToEntity.TryAdd(e.Name, e);

        // Group entities by tier, then topologically sort within each tier.
        var sorted = new List<FshEntity>(allEntities.Count);

        foreach (var tierGroup in allEntities.GroupBy(EntityTier).OrderBy(g => g.Key))
        {
            var tier = tierGroup.ToList();
            if (tier.Count <= 1)
            {
                sorted.AddRange(tier);
                continue;
            }

            // Build adjacency list for Kahn's algorithm within this tier.
            var tierNames = new HashSet<string>(tier.Select(e => e.Name), StringComparer.Ordinal);
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var e in tier)
            {
                inDegree.TryAdd(e.Name, 0);
                dependents.TryAdd(e.Name, []);
            }

            foreach (var e in tier)
            {
                var dep = GetEntityDependency(e);
                if (dep != null)
                {
                    // Resolve aliases so that $-prefixed refs match entity names.
                    var resolved = context.ResolveAlias(dep);
                    // Only consider dependencies within this same tier.
                    if (tierNames.Contains(resolved))
                    {
                        inDegree[e.Name] = inDegree.GetValueOrDefault(e.Name) + 1;
                        dependents[resolved].Add(e.Name);
                    }
                }
            }

            // Kahn's algorithm.
            var queue = new Queue<string>(
                tier.Where(e => inDegree.GetValueOrDefault(e.Name) == 0).Select(e => e.Name));
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                if (!visited.Add(name)) continue;

                sorted.Add(nameToEntity[name]);

                foreach (var child in dependents.GetValueOrDefault(name, []))
                {
                    inDegree[child]--;
                    if (inDegree[child] == 0)
                        queue.Enqueue(child);
                }
            }

            // Append any entities not reached (cycles or unresolved deps) in original order.
            foreach (var e in tier)
            {
                if (!visited.Contains(e.Name))
                    sorted.Add(e);
            }
        }

        return sorted;
    }

    private static CompileResult<List<FhirResource>> CompileWithContext(
        IEnumerable<FshDoc> docs, CompilerContext context, CompilerOptions opts)
    {
        var errors = new List<CompilerError>();
        var resources = new List<FhirResource>();

        // Track entity-name → StructureDefinition for Mapping processing.
        var sdByEntityName = new Dictionary<string, StructureDefinition>(StringComparer.Ordinal);

        // Sort all entities across all documents into a single dependency-ordered list.
        // The sort ensures: Alias/RuleSet/Invariant first, then CodeSystems, ValueSets,
        // SD-producing entities (dependency-ordered), Instances, and Mappings last.
        var sortedEntities = SortEntities(docs, context);

        foreach (var entity in sortedEntities)
        {
            try
            {
                // Mappings annotate already-compiled StructureDefinitions rather than
                // producing a new resource.
                if (entity is Hl7.FhirShorthand.Serialization.Models.Mapping mapping)
                {
                    if (mapping.Source is null ||
                        !sdByEntityName.TryGetValue(mapping.Source, out var targetSd))
                    {
                        context.Warnings.Add(new CompilerWarning
                        {
                            EntityName = entity.Name,
                            Message = mapping.Source is null
                                ? "Mapping has no Source; skipped."
                                : $"Mapping Source '{mapping.Source}' does not match any compiled StructureDefinition; skipped."
                        });
                        continue;
                    }

                    ApplyMappingToSD(mapping, targetSd, context, opts);
                    continue;
                }

                FhirResource? resource = entity switch
                {
                    Profile profile => BuildProfile(profile, context, opts),
                    Hl7.FhirShorthand.Serialization.Models.Extension ext => BuildExtension(ext, context, opts),
                    Logical logical => BuildLogical(logical, context, opts),
                    Hl7.FhirShorthand.Serialization.Models.Resource fshResource => BuildResource(fshResource, context, opts),
                    Hl7.FhirShorthand.Serialization.Models.ValueSet vs => BuildValueSet(vs, context, opts),
                    Hl7.FhirShorthand.Serialization.Models.CodeSystem cs => BuildCodeSystem(cs, context, opts),
                    Hl7.FhirShorthand.Serialization.Models.Instance inst => BuildInstance(inst, context, opts),
                    // Alias, RuleSet, and Invariant produce no FHIR resource
                    _ => null
                };

                if (resource != null)
                {
                    resources.Add(resource);
                    if (resource is StructureDefinition sd)
                    {
                        sdByEntityName.TryAdd(entity.Name, sd);
                        context.RegisterStructureDefinition(entity.Name, sd);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new CompilerError
                {
                    EntityName = entity.Name,
                    Message = ex.Message,
                    Position = entity.Position
                });
            }
        }

        // Post-compilation fix-up: resolve in-IG ValueSet binding URLs.
        // Binding rules that reference a ValueSet by entity name (e.g. "QuestionnaireLaunchContext")
        // cannot be resolved during SD compilation because the VS might not be compiled yet.
        // Now that all resources are built, walk every StructureDefinition's element bindings
        // and replace bare ValueSet names with their compiled canonical URLs.
        FixUpValueSetBindings(resources.OfType<StructureDefinition>().ToList(), resources, context, opts);

        return errors.Count > 0
            ? CompileResult<List<FhirResource>>.FromFailure(errors, context.Warnings)
            : CompileResult<List<FhirResource>>.FromSuccess(resources, context.Warnings);
    }

    // ─── StructureDefinition builders ────────────────────────────────────────

    /// <summary>
    /// Converts a FSH <see cref="Profile"/> entity to a FHIR <see cref="StructureDefinition"/>.
    /// </summary>
    public static StructureDefinition BuildProfile(
        Profile profile, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();

        // C-PR3: Determine the profile's Kind from the parent type via the resolver or ModelInspector.
        // For profiles of resources → "resource"; for datatypes → "complex-type".
        // Fall back to "resource" when the type can't be resolved (most common case).
        var parentTypeName = context.ResolveAlias(profile.Parent?.Value) ?? "DomainResource";

        // Build a merged resolver that includes both compiled SDs and the external resolver.
        var mergedResolver = BuildMergedResolver(context, opts.Resolver);

        // Resolve the parent type name to a canonical URL when it is a bare core FHIR type.
        // Prefer using the resolver to find the SD; fall back to the inspector for backward
        // compatibility when no resolver is available.
        var parentBaseSd = FindStructureDefinitionForType(parentTypeName, mergedResolver);
        if (parentBaseSd?.Url != null)
            parentTypeName = parentBaseSd.Url;
        else if (opts.Inspector?.IsKnownResource(parentTypeName) == true || opts.Inspector?.IsDataType(parentTypeName) == true)
            parentTypeName = opts.Inspector.CanonicalUriForFhirCoreType(parentTypeName) ?? parentTypeName;

        var resolvedParent = parentTypeName;

        // C-PR4: StructureDefinition.Type must be the bare FHIR type name.
        // When the parent is a URL (e.g. from an alias), strip to the last segment and
        // check whether that segment is a known base FHIR type via the resolver or ModelInspector.
        // Only use the stripped name when it is a recognised type; otherwise fall back to
        // the pre-alias-resolution name so profiles-of-profiles don't produce a bogus type.
        var typeValue = ExtractBareTypeName(resolvedParent, parentTypeName, opts.Inspector, mergedResolver);
        if (!IsKnownFhirType(typeValue, opts.Inspector, mergedResolver))
        {
            // Try resolver-based profile chain walk first
            var resolvedFromSd = context.ResolveBaseTypeFromResolver(resolvedParent, mergedResolver);
            if (!string.IsNullOrEmpty(resolvedFromSd))
            {
                typeValue = resolvedFromSd;
            }

            var byUrl = context.CompiledStructureDefinitions
                .Values
                .Where(s => !string.IsNullOrEmpty(s.Url))
                .GroupBy(s => s.Url!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var resolvedType = ResolveUnderlyingFhirType(typeValue, context, opts, byUrl, depth: 0);
            if (!string.IsNullOrEmpty(resolvedType))
                typeValue = resolvedType;
        }

        // C-PR3: Use the merged resolver (compiled SDs + external) so that profiles-of-profiles
        // resolve the correct Kind (e.g. a profile of an in-IG profile that ultimately profiles
        // a core resource type gets Kind=Resource, not Kind=ComplexType).
        var kind = InferKindFromType(typeValue, opts.Inspector, mergedResolver);

        var sd = new StructureDefinition
        {
            Id = profile.Id?.Value,
            Url = ResolveUrl(profile.Id?.Value, opts, "StructureDefinition"),
            Name = profile.Name,
            Title = profile.Title?.Value,
            Description = NormalizeLineEndings(profile.Description?.Value),
            Status = PublicationStatus.Active,
            Abstract = false,
            Kind = kind,
            Type = typeValue,
            BaseDefinition = ResolveBaseDefinitionCanonical(resolvedParent, parentTypeName, context, opts),
            Derivation = StructureDefinition.TypeDerivationRule.Constraint,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        if (opts.FhirVersion != null && sd.FhirVersion == null)
        {
            // Try well-known shorthands first, then fall back to EnumUtility.ParseLiteral
            // which handles all version strings that carry [EnumLiteral] attributes.
            sd.FhirVersion = opts.FhirVersion switch
            {
                "4.0.1" or "4.0" => FHIRVersion.N4_0_1,
                "4.3.0" or "4.3" => FHIRVersion.N4_3_0,
                "5.0.0" or "5.0" => FHIRVersion.N5_0_0,
                _ => EnumUtility.ParseLiteral<FHIRVersion>(opts.FhirVersion, ignoreCase: true)
            };
        }

        // Root element: explicit ElementId is required for correct round-trip and validation.
        sd.Differential.Element.Add(new ElementDefinition(sd.Type) { Path = sd.Type, ElementId = sd.Type });

        AttachOrderingContext(sd, context, opts);

        // Resolve the core base FHIR type SD (not just the immediate parent) for choice-type
        // variant mapping and base-slice detection.  When the parent is itself a profile of a
        // core type, parentBaseSd is a constraint and does not carry the full type list.
        // Must be resolved BEFORE ApplySdRules because choice-type slice detection now happens
        // inline in GetOrCreateElement during rule application.
        var coreTypeSd = string.IsNullOrEmpty(sd.Type) ? null
            : FindStructureDefinitionForType(sd.Type, mergedResolver);
        AttachChoiceSliceContext(sd, parentBaseSd, coreTypeSd, mergedResolver);

        ApplySdRules(profile.Rules, sd, context, opts);

        RemoveRedundantCardinalityAgainstBase(sd, context, opts);
        RemoveRedundantTypeConstraints(sd, context, opts);
        RemoveRedundantSlicingAgainstBase(sd, context, opts);
        RemoveNoOpScaffoldElements(sd);
        return sd;
    }

    /// <summary>
    /// Infers the <see cref="StructureDefinition.StructureDefinitionKind"/> for a profile
    /// given its parent type name (possibly a URL).  Prefers the <paramref name="resolver"/>
    /// to look up the StructureDefinition's <c>Kind</c> property directly; falls back to
    /// <paramref name="inspector"/> when the resolver is unavailable.  Defaults to
    /// <c>Resource</c> when the type cannot be resolved.
    /// </summary>
    private static StructureDefinition.StructureDefinitionKind InferKindFromType(
        string? parentTypeOrUrl, ModelInspector? inspector, IResourceResolver? resolver = null)
    {
        if (string.IsNullOrEmpty(parentTypeOrUrl))
            return StructureDefinition.StructureDefinitionKind.Resource;

        // Strip URL prefix to get the bare type name.
        var typeName = parentTypeOrUrl;
        if (IsAbsoluteUrl(typeName))
        {
            var lastSlash = typeName.LastIndexOf('/');
            if (lastSlash >= 0) typeName = typeName[(lastSlash + 1)..];
        }

        // Prefer resolver-based lookup: read Kind directly from the StructureDefinition.
        if (resolver != null)
        {
            var sd = FindStructureDefinitionForType(typeName, resolver)
                  ?? (IsAbsoluteUrl(parentTypeOrUrl) ? resolver.FindStructureDefinition(parentTypeOrUrl) : null);
            if (sd?.Kind != null)
            {
                return sd.Kind == StructureDefinition.StructureDefinitionKind.Resource
                    ? StructureDefinition.StructureDefinitionKind.Resource
                    : StructureDefinition.StructureDefinitionKind.ComplexType;
            }
        }

        if (inspector != null)
        {
            var classMap = inspector.FindClassMapping(typeName);
            if (classMap != null)
            {
                return classMap.IsResource
                    ? StructureDefinition.StructureDefinitionKind.Resource
                    : StructureDefinition.StructureDefinitionKind.ComplexType;
            }
        }

        // Fallback: most profiles target resources; if the name starts with a lowercase letter
        // (e.g. "string", "boolean", "code") assume complex-type (FHIR uses ComplexType for
        // both complex data types and primitive types in the SDK model).
        // NOTE: This is a last-resort heuristic that works for the FHIR naming convention
        // where base resource types are PascalCase (Patient, Questionnaire) while primitive
        // and complex data types are lowercase (string, boolean, code, Coding).  It will
        // fail for non-standard type names that don't follow this convention.
        if (typeName.Length > 0 && char.IsLower(typeName[0]))
            return StructureDefinition.StructureDefinitionKind.ComplexType;

        return StructureDefinition.StructureDefinitionKind.Resource;
    }

    /// <summary>
    /// Extracts the bare FHIR type name from a parent type name or URL.
    /// When <paramref name="resolvedParent"/> is a URL, strips the last segment and
    /// checks whether it is a known base FHIR type via <paramref name="resolver"/> (preferred)
    /// or <paramref name="inspector"/> (fallback).
    /// Falls back to <paramref name="originalParent"/> (pre-alias-resolution name) so that
    /// profiles-of-profiles (where the URL segment is a profile id, not a type name) don't
    /// produce a bogus type value.
    /// </summary>
    private static string ExtractBareTypeName(
        string resolvedParent, string originalParent, ModelInspector? inspector, IResourceResolver? resolver)
    {
        if (!IsAbsoluteUrl(resolvedParent))
            return resolvedParent;   // Already bare (e.g. "Patient", "DomainResource").

        var lastSlash = resolvedParent.LastIndexOf('/');
        var segment = lastSlash >= 0 ? resolvedParent[(lastSlash + 1)..] : resolvedParent;

        // Prefer resolver: look up the segment as a core FHIR type (Specialization only).
        // Profiles (Constraint SDs) are not bare type names — they need further resolution.
        if (resolver != null)
        {
            var sd = FindStructureDefinitionForType(segment, resolver);
            if (sd?.Derivation == StructureDefinition.TypeDerivationRule.Constraint && !string.IsNullOrEmpty(sd.Type))
                return sd.Type;
            if (sd?.Derivation == StructureDefinition.TypeDerivationRule.Specialization)
                return segment;
        }

        // Fall back to inspector.
        if (inspector != null && inspector.FindClassMapping(segment) != null)
            return segment;

        // Alias name like "$someAlias" → strip the leading alias prefix to get a displayable fallback.
        return originalParent.TrimStart(AliasPrefix);
    }

    /// <summary>
    /// Converts a FSH <see cref="Hl7.FhirShorthand.Serialization.Models.Extension"/> entity to a
    /// FHIR <see cref="StructureDefinition"/> of kind <c>complex-type</c>.
    /// </summary>
    public static StructureDefinition BuildExtension(
        Hl7.FhirShorthand.Serialization.Models.Extension ext, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var sd = new StructureDefinition
        {
            Id = ext.Id,
            Url = ResolveUrl(ext.Id, opts, "StructureDefinition"),
            Name = ext.Name,
            Title = ext.Title,
            Description = NormalizeLineEndings(ext.Description),
            Status = PublicationStatus.Active,
            Abstract = false,
            Type = "Extension",
            BaseDefinition = ResolveBaseDefinitionCanonical(
                context.ResolveAlias(ext.Parent ?? "http://hl7.org/fhir/StructureDefinition/Extension"),
                "Extension", context, opts),
            Derivation = StructureDefinition.TypeDerivationRule.Constraint,
            Kind = StructureDefinition.StructureDefinitionKind.ComplexType,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        sd.Differential.Element.Add(new ElementDefinition("Extension") { Path = "Extension", ElementId = "Extension" });

        AttachOrderingContext(sd, context, opts);
        var extResolver = BuildMergedResolver(context, opts.Resolver);
        AttachChoiceSliceContext(sd, parentBaseSd: null,
            coreTypeSd: FindStructureDefinitionForType(sd.Type, extResolver),
            resolver: extResolver);

        // Stamp the target FHIR version from the compiler options when supplied (sushi always
        // emits fhirVersion on generated StructureDefinitions).
        if (opts.FhirVersion != null && sd.FhirVersion == null)
        {
            sd.FhirVersion = opts.FhirVersion switch
            {
                "4.0.1" or "4.0" => FHIRVersion.N4_0_1,
                "4.3.0" or "4.3" => FHIRVersion.N4_3_0,
                "5.0.0" or "5.0" => FHIRVersion.N5_0_0,
                _ => EnumUtility.ParseLiteral<FHIRVersion>(opts.FhirVersion, ignoreCase: true)
            };
        }

        // Context — determine type from the grammar alternative that was matched (P-EX3).
        if (ext.Contexts.Count > 0)
        {
            sd.Context = ext.Contexts
                .Select(c => new StructureDefinition.ContextComponent
                {
                    Type = c.Type switch
                    {
                        ContextItemType.Fhirpath  => StructureDefinition.ExtensionContextType.Fhirpath,
                        ContextItemType.Extension => StructureDefinition.ExtensionContextType.Extension,
                        _                         => StructureDefinition.ExtensionContextType.Element,
                    },
                    Expression = c.Value
                })
                .ToList();
        }

        ApplySdRules(ext.Rules, sd, context, opts);

        // C-EX1: Normalize the differential to match FHIR / sushi conventions for extensions:
        //   • Strip a redundant min=0 on the root element (defaults inherited from Element).
        //   • Auto-inject an Extension.extension element with max="0" for simple extensions
        //     (no explicit Extension.extension already present → forbids nested extensions).
        //   • Ensure Extension.url carries fixedUri = canonical URL and no redundant type
        //     constraint (the base already pins it to uri).
        NormalizeExtensionDifferential(sd);

        return sd;
    }

    /// <summary>
    /// Applies post-rule normalization to an Extension's differential element list so that
    /// the output matches the canonical sushi shape: the root element suppresses the
    /// default min=0, a simple extension gets an auto-generated <c>Extension.extension</c>
    /// marker with <c>max="0"</c>, and <c>Extension.url</c> is pinned to the extension's
    /// canonical URL via <c>fixedUri</c> without a redundant <c>type</c> constraint.
    /// </summary>
    private static void NormalizeExtensionDifferential(StructureDefinition sd)
    {
        var elements = sd.Differential?.Element;
        if (elements == null) return;

        // Root "Extension" element: drop redundant cardinality that matches the inherited
        // defaults from the base Extension StructureDefinition (min=0, max="*"). Sushi
        // strips these because they carry no information.
        var root = elements.FirstOrDefault(e => e.Path == "Extension" && e.SliceName == null);
        if (root != null && root.Min == 0)
            root.MinElement = null;
        if (root != null && root.Max == "*")
            root.MaxElement = null;

        // Populate root Short / Definition from the Extension's Title / Description when
        // not explicitly set by caret rules — mirrors sushi's default behaviour of
        // copying these values onto the root ElementDefinition.
        if (root != null)
        {
            if (string.IsNullOrEmpty(root.Short) && !string.IsNullOrEmpty(sd.Title))
                root.Short = sd.Title;
            if (string.IsNullOrEmpty(root.Definition) && !string.IsNullOrEmpty(sd.Description))
                root.Definition = sd.Description;
        }

        // Determine whether the extension declares any sub-extensions. A ContainsRule over
        // "extension" creates a slice element with SliceName set, plus (optionally) a parent
        // Extension.extension node. If any child of Extension.extension is present, treat
        // this as a complex extension; otherwise auto-inject a zero-cardinality marker.
        var hasExtensionChild = elements.Any(e =>
            (e.Path == "Extension.extension" && e.SliceName == null) ||
            (e.Path != null && e.Path.StartsWith("Extension.extension", StringComparison.Ordinal)));

        if (!hasExtensionChild)
        {
            var marker = new ElementDefinition("Extension.extension")
            {
                Path = "Extension.extension",
                ElementId = "Extension.extension",
                Max = "0"
            };
            InsertElementInOrder(sd, marker);
        }

        // Extension.url: always pinned to the canonical URL via fixedUri; strip any type
        // constraint the user may have set via "* url only uri" (the base already fixes the
        // type to uri so the constraint is redundant and sushi omits it).
        if (!string.IsNullOrEmpty(sd.Url))
        {
            var urlEl = elements.FirstOrDefault(e => e.Path == "Extension.url" && e.SliceName == null);
            if (urlEl == null)
            {
                urlEl = new ElementDefinition("Extension.url")
                {
                    Path = "Extension.url",
                    ElementId = "Extension.url"
                };
                InsertElementInOrder(sd, urlEl);
            }

            urlEl.Fixed = new FhirUri(sd.Url);
            urlEl.Type = new List<ElementDefinition.TypeRefComponent>();
        }

        // Extension.value[x]: strip max="1" when it equals the inherited default from the
        // base Extension.value[x] (cardinality 0..1). Sushi omits this redundant cardinality.
        var valueEl = elements.FirstOrDefault(e => e.Path == "Extension.value[x]" && e.SliceName == null);
        if (valueEl != null && valueEl.Max == "1")
            valueEl.MaxElement = null;

        // For each sub-extension slice (Extension.extension:X), sushi auto-generates:
        //   • Extension.extension:X.extension  (max = "0") — forbids grand-children
        //   • Extension.extension:X.url        (fixedUri = X, no type constraint)
        // and adds Extension.value[x] (max = "0") at the root to forbid a direct value.
        NormalizeSubExtensionSlices(sd);
    }

    /// <summary>
    /// Sushi-compatible post-processing for complex extensions: for every sub-extension slice
    /// (<c>Extension.extension:X</c>) ensures the standard child elements exist
    /// (<c>:X.extension</c> with <c>max="0"</c>, <c>:X.url</c> with <c>fixedUri=X</c>) and
    /// strips the redundant <c>type=[uri]</c> constraint from sub-extension url elements.
    /// When any slice is present, also injects a root <c>Extension.value[x]</c> with
    /// <c>max="0"</c> so that the complex extension cannot also carry a direct value.
    /// </summary>
    private static void NormalizeSubExtensionSlices(StructureDefinition sd)
    {
        var elements = sd.Differential.Element;

        // Collect slice elements directly under Extension.extension.
        var sliceElements = elements
            .Where(e => e.Path == "Extension.extension" && !string.IsNullOrEmpty(e.SliceName))
            .ToList();

        if (sliceElements.Count == 0) return;

        foreach (var slice in sliceElements)
        {
            var sliceName = slice.SliceName!;
            var sliceIdPrefix = $"Extension.extension:{sliceName}";

            // 1. Ensure :X.extension max=0 marker exists.
            var childExt = elements.FirstOrDefault(e => e.ElementId == $"{sliceIdPrefix}.extension");
            if (childExt == null)
            {
                childExt = new ElementDefinition("Extension.extension.extension")
                {
                    Path = "Extension.extension.extension",
                    ElementId = $"{sliceIdPrefix}.extension",
                    Max = "0"
                };
                InsertElementInOrder(sd, childExt);
            }
            else if (childExt.Max == null)
            {
                childExt.Max = "0";
            }

            // 2. Ensure :X.url exists with fixedUri = sliceName and no type constraint.
            var childUrl = elements.FirstOrDefault(e => e.ElementId == $"{sliceIdPrefix}.url");
            if (childUrl == null)
            {
                childUrl = new ElementDefinition("Extension.extension.url")
                {
                    Path = "Extension.extension.url",
                    ElementId = $"{sliceIdPrefix}.url",
                    Fixed = new FhirUri(sliceName)
                };
                InsertElementInOrder(sd, childUrl);
            }
            else
            {
                // Pin url via fixedUri (sushi convention) and drop redundant type=[uri].
                childUrl.Fixed ??= new FhirUri(sliceName);
                childUrl.Type = new List<ElementDefinition.TypeRefComponent>();
            }

            // 3. Strip redundant cardinality on :X.value[x] — base Extension.value[x]
            //    already has min=0, max="1", so sushi omits those default values.
            var childValue = elements.FirstOrDefault(e => e.ElementId == $"{sliceIdPrefix}.value[x]");
            if (childValue != null)
            {
                if (childValue.Max == "1") childValue.MaxElement = null;
                if (childValue.Min == 0) childValue.MinElement = null;
            }
        }

        // 3. Inject root Extension.value[x] with max="0" to forbid direct value on this
        //    complex extension (sushi emits this whenever sub-extension slices exist).
        var rootValue = elements.FirstOrDefault(e => e.Path == "Extension.value[x]" && e.SliceName == null);
        if (rootValue == null)
        {
            rootValue = new ElementDefinition("Extension.value[x]")
            {
                Path = "Extension.value[x]",
                ElementId = "Extension.value[x]",
                Max = "0"
            };
            InsertElementInOrder(sd, rootValue);
        }
        else if (rootValue.Max == null)
        {
            rootValue.Max = "0";
        }

        // 4. Aggregate required slice minimums onto the parent Extension.extension.
        //    Sushi computes min on the parent as the sum of mandatory sub-extension
        //    slices (those with min >= 1) and drops the default slicing block.
        AggregateRequiredSliceMinimums(elements);
    }

    /// <summary>
    /// Sums the <c>min</c> values of required sub-extension slices onto the parent
    /// <c>Extension.extension</c> element and drops the default value-on-url slicing
    /// block when present.  Matches sushi's convention of treating the extension-by-url
    /// slicing as implicit and surfacing the aggregate required cardinality on the
    /// parent.
    /// </summary>
    private static void AggregateRequiredSliceMinimums(List<ElementDefinition> elements)
    {
        var parent = elements.FirstOrDefault(e => e.Path == "Extension.extension" && e.SliceName == null);
        if (parent == null) return;

        var requiredSum = elements
            .Where(e => e.Path == "Extension.extension" && !string.IsNullOrEmpty(e.SliceName))
            .Sum(e => e.Min ?? 0);

        if (requiredSum > 0 && (parent.Min ?? 0) < requiredSum)
            parent.Min = requiredSum;

        // Drop the default extension-by-url slicing block (implicit per FHIR).
        if (parent.Slicing?.Discriminator?.Count == 1)
        {
            var d = parent.Slicing.Discriminator[0];
            if (d.Type == ElementDefinition.DiscriminatorType.Value && d.Path == "url")
                parent.Slicing = null;
        }
    }

    /// <summary>
    /// Converts a FSH <see cref="Logical"/> entity to a FHIR <see cref="StructureDefinition"/>
    /// of kind <c>logical</c>.
    /// </summary>
    public static StructureDefinition BuildLogical(
        Logical logical, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();

        // Logical models use the full canonical URL as Type (spec §Logical), but element
        // paths use the short id so that element ids look like "SdcQuestionLibrary.dob"
        // rather than "http://hl7.org/fhir/uv/sdc/StructureDefinition/SdcQuestionLibrary.dob".
        var logicalUrl = ResolveUrl(logical.Id, opts, "StructureDefinition");
        var logicalType = !string.IsNullOrEmpty(logicalUrl) ? logicalUrl : logical.Name;
        var logicalPathPrefix = logical.Id ?? logical.Name;

        var sd = new StructureDefinition
        {
            Id = logical.Id,
            Url = logicalUrl,
            Name = logical.Name,
            Title = logical.Title,
            Description = NormalizeLineEndings(logical.Description),
            Status = PublicationStatus.Active,
            Abstract = false,
            Type = logicalType,
            BaseDefinition = ResolveBaseDefinitionCanonical(
                context.ResolveAlias(logical.Parent ?? "http://hl7.org/fhir/StructureDefinition/Base"),
                "Base", context, opts),
            Derivation = StructureDefinition.TypeDerivationRule.Specialization,
            Kind = StructureDefinition.StructureDefinitionKind.Logical,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        if (opts.FhirVersion != null && sd.FhirVersion == null)
        {
            sd.FhirVersion = opts.FhirVersion switch
            {
                "4.0.1" or "4.0" => FHIRVersion.N4_0_1,
                "4.3.0" or "4.3" => FHIRVersion.N4_3_0,
                "5.0.0" or "5.0" => FHIRVersion.N5_0_0,
                _ => EnumUtility.ParseLiteral<FHIRVersion>(opts.FhirVersion, ignoreCase: true)
            };
        }

        var rootElement = new ElementDefinition(logicalPathPrefix) { Path = logicalPathPrefix, ElementId = logicalPathPrefix, Short = logical.Title, Definition = logical.Description };
        sd.Differential.Element.Add(rootElement);

        AttachOrderingContext(sd, context, opts);
        var logicalResolver = BuildMergedResolver(context, opts.Resolver);
        AttachChoiceSliceContext(sd, parentBaseSd: null,
            coreTypeSd: FindStructureDefinitionForType(sd.Type, logicalResolver),
            resolver: logicalResolver);

        // C-LG1: Emit type-characteristics extension for each Characteristics code.
        if (logical.Characteristics.Count > 0)
        {
            foreach (var ch in logical.Characteristics)
            {
                sd.Extension.Add(new FhirExtension
                {
                    Url = "http://hl7.org/fhir/tools/StructureDefinition/type-characteristics",
                    Value = new FhirCode(ch.TrimStart('#'))
                });
            }
        }

        ApplySdRules(logical.Rules, sd, context, opts);
        RemoveRedundantCardinalityAgainstBase(sd, context, opts);
        RemoveNoOpScaffoldElements(sd);
        return sd;
    }

    /// <summary>
    /// Converts a FSH <see cref="Hl7.FhirShorthand.Serialization.Models.Resource"/> entity to a FHIR
    /// <see cref="StructureDefinition"/> of kind <c>resource</c>.
    /// </summary>
    public static StructureDefinition BuildResource(
        Hl7.FhirShorthand.Serialization.Models.Resource fshResource, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var sd = new StructureDefinition
        {
            Id = fshResource.Id,
            Url = ResolveUrl(fshResource.Id, opts, "StructureDefinition"),
            Name = fshResource.Name,
            Title = fshResource.Title,
            Description = NormalizeLineEndings(fshResource.Description),
            Status = PublicationStatus.Active,
            Experimental = false,
            Abstract = false,
            Type = fshResource.Name,
            BaseDefinition = ResolveBaseDefinitionCanonical(
                context.ResolveAlias(fshResource.Parent ?? "http://hl7.org/fhir/StructureDefinition/DomainResource"),
                "DomainResource", context, opts),
            Derivation = StructureDefinition.TypeDerivationRule.Specialization,
            Kind = StructureDefinition.StructureDefinitionKind.Resource,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        sd.Differential.Element.Add(new ElementDefinition(sd.Type) { Path = sd.Type, ElementId = sd.Type });

        AttachOrderingContext(sd, context, opts);
        var resourceResolver = BuildMergedResolver(context, opts.Resolver);
        AttachChoiceSliceContext(sd, parentBaseSd: null,
            coreTypeSd: FindStructureDefinitionForType(sd.Type, resourceResolver),
            resolver: resourceResolver);

        ApplySdRules(fshResource.Rules, sd, context, opts);
        RemoveRedundantCardinalityAgainstBase(sd, context, opts);
        RemoveNoOpScaffoldElements(sd);
        return sd;
    }

    // ─── ValueSet builder ────────────────────────────────────────────────────

    /// <summary>
    /// Converts a FSH <see cref="Hl7.FhirShorthand.Serialization.Models.ValueSet"/> entity to a FHIR
    /// <see cref="FhirValueSet"/> resource.
    /// </summary>
    public static FhirValueSet BuildValueSet(
        Hl7.FhirShorthand.Serialization.Models.ValueSet vs, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var fvs = new FhirValueSet
        {
            Id = vs.Id,
            Url = ResolveUrl(vs.Id, opts, "ValueSet"),
            Name = vs.Name,
            Title = vs.Title,
            Description = NormalizeLineEndings(vs.Description),
            Status = PublicationStatus.Active,
            Experimental = false,
            Compose = new FhirValueSet.ComposeComponent
            {
                Include = new List<FhirValueSet.ConceptSetComponent>(),
                Exclude = new List<FhirValueSet.ConceptSetComponent>()
            }
        };

        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);
        foreach (var rule in vs.Rules)
        {
            switch (rule)
            {
                case VsComponentRule compRule:
                    ApplyVsComponentRule(compRule, fvs, context, opts);
                    break;

                case VsCaretValueRule caretRule:
                    ApplyVsCaretValueRule(caretRule, fvs, opts.Inspector, context.ResolveAlias, canonicalResolver);
                    break;

                case VsInsertRule insertRule:
                    ApplyVsInsertRule(insertRule, fvs, context, opts);
                    break;

                case CodeCaretValueRule codeCaretRule:
                    ApplyCodeCaretValueRule(codeCaretRule, fvs, opts.Inspector, context.ResolveAlias, canonicalResolver);
                    break;

                case CodeInsertRule codeInsertRule:
                    ApplyCodeInsertRule(codeInsertRule, fvs, context, opts);
                    break;

                default:
                    context.Warnings.Add(new CompilerWarning
                    {
                        EntityName = vs.Name,
                        Message = $"Rule type '{rule.GetType().Name}' is not supported for ValueSets; skipped.",
                        Position = rule.Position
                    });
                    break;
            }
        }

        // Remove empty include/exclude lists
        if (fvs.Compose.Include.Count == 0 && fvs.Compose.Exclude.Count == 0)
            fvs.Compose = null;

        return fvs;
    }

    // ─── CodeSystem builder ──────────────────────────────────────────────────

    /// <summary>
    /// Converts a FSH <see cref="Hl7.FhirShorthand.Serialization.Models.CodeSystem"/> entity to a FHIR
    /// <see cref="FhirCodeSystem"/> resource.
    /// </summary>
    public static FhirCodeSystem BuildCodeSystem(
        Hl7.FhirShorthand.Serialization.Models.CodeSystem cs, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var resolvedInspector = opts.Inspector ?? ModelInspector.ForAssembly(typeof(StructureDefinition).Assembly);
        var fcs = new FhirCodeSystem
        {
            Id = cs.Id,
            Url = ResolveUrl(cs.Id, opts, "CodeSystem"),
            Name = cs.Name,
            Title = cs.Title,
            Description = NormalizeLineEndings(cs.Description),
            Status = PublicationStatus.Active,
            Experimental = false,
            Content = CodeSystemContentMode.Complete,
            Concept = new List<FhirCodeSystem.ConceptDefinitionComponent>()
        };

        // Soft-index state for top-level (CodeSystem) caret rules and per-concept caret rules.
        var csIndexState = new Dictionary<string, int>(StringComparer.Ordinal);
        var conceptIndexStates = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);

        foreach (var rule in PropagateConceptCodesToCaretRules(cs.Rules))
        {
            switch (rule)
            {
                case Concept concept:
                    ApplyConceptRule(concept, fcs);
                    break;

                case CsCaretValueRule caretRule:
                    ApplyCsCaretValueRule(caretRule, fcs, csIndexState, conceptIndexStates, resolvedInspector, context.ResolveAlias, canonicalResolver);
                    break;

                case CsInsertRule insertRule:
                    ApplyCsInsertRule(insertRule, fcs, context, opts, csIndexState, conceptIndexStates);
                    break;
            }
        }

        // C-CS1: Compute total concept count (including nested concepts).
        fcs.Count = CountAllConcepts(fcs.Concept);

        return fcs;
    }

    private static int CountAllConcepts(IEnumerable<FhirCodeSystem.ConceptDefinitionComponent>? concepts)
    {
        if (concepts is null) return 0;
        int total = 0;
        foreach (var c in concepts)
        {
            total++;
            total += CountAllConcepts(c.Concept);
        }
        return total;
    }

    /// <summary>
    /// Propagates concept codes from parent <see cref="Concept"/> rules to child
    /// <see cref="CsCaretValueRule"/> rules that are indented under them (C-CS4).
    /// </summary>
    /// <remarks>
    /// In FSH a concept-scoped caret-value rule can be written two ways:
    /// <code>
    /// * #root ^property[+].code = #notSelectable      ← explicit code on the same line
    /// * #root "Root"                                   ← concept
    ///   * ^property[+].code = #notSelectable           ← indented caret rule (no code tokens)
    /// </code>
    /// The indented form stores an empty <see cref="CsCaretValueRule.Codes"/> list.
    /// This method detects that pattern by comparing indentation levels and fills in
    /// the codes from the ancestor <see cref="Concept"/> rule.
    /// </remarks>
    private static IEnumerable<CsRule> PropagateConceptCodesToCaretRules(IEnumerable<CsRule> rules)
    {
        // Stack entries: (indentLength, conceptCodes)
        var stack = new Stack<(int IndentLen, List<string> Codes)>();

        foreach (var rule in rules)
        {
            var indentLen = rule.Indent.Length;

            // Pop all stack entries at the same or deeper indent — they can no longer be
            // ancestors of the current rule.
            while (stack.Count > 0 && stack.Peek().IndentLen >= indentLen)
                stack.Pop();

            if (rule is Concept concept)
            {
                // Push concept's codes so that more-indented caret rules can inherit them.
                if (concept.Codes.Count > 0)
                    stack.Push((indentLen, concept.Codes));
                yield return rule;
            }
            else if (rule is CsCaretValueRule caretRule && caretRule.Codes.Count == 0
                     && stack.Count > 0)
            {
                // C-CS4: Indented caret rule with no explicit codes — inherit from parent concept.
                var parentCodes = stack.Peek().Codes;
                yield return new CsCaretValueRule
                {
                    Position = caretRule.Position,
                    LeadingHiddenTokens = caretRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = caretRule.TrailingHiddenTokens,
                    Indent = caretRule.Indent,
                    Codes = parentCodes,
                    CaretPath = caretRule.CaretPath,
                    Value = caretRule.Value
                };
            }
            else
            {
                yield return rule;
            }
        }
    }



    /// <summary>
    /// Applies indented-rule path composition to <paramref name="rules"/>, returning a new
    /// sequence where each rule's path has been prefixed by any ancestor rule's path,
    /// respecting indentation levels (C-FP1 / P-FP1).
    /// </summary>
    /// <remarks>
    /// FSH allows nested rules to use relative paths:
    /// <code>
    /// * extension[option]          ← context path = "extension[option]"
    ///   * value[x] 1..1            ← resolved path = "extension[option].value[x]"
    ///   * ^short = "The value"     ← resolved path = "extension[option]" (empty path inherits context)
    ///     * ^definition = "..."    ← resolved path = "extension[option].value[x]" (inherits from value[x])
    /// </code>
    /// The <c>Indent</c> property on each rule (the whitespace before <c>*</c>) determines
    /// the nesting level.  This method composes paths bottom-up from that hierarchy.
    /// </remarks>
    private static IEnumerable<FshRule> ComposeIndentedPaths(IEnumerable<FshRule> rules)
    {
        // Stack entries: (indentLength, effectivePath)
        var stack = new Stack<(int IndentLen, string Path)>();

        foreach (var rule in rules)
        {
            var indentLen = rule.Indent.Length;

            // Pop all ancestors that are at the same or deeper indent — they cannot be
            // ancestors of the current rule.
            while (stack.Count > 0 && stack.Peek().IndentLen >= indentLen)
                stack.Pop();

            // The parent context path is from the stack top.
            var parentPath = stack.Count > 0 ? stack.Peek().Path : string.Empty;

            // Compute the effective path: compose parent path with rule path.
            string effectivePath;
            if (!string.IsNullOrEmpty(parentPath))
            {
                effectivePath = string.IsNullOrEmpty(rule.Path)
                    ? parentPath               // caret/obeys rule with no element path → inherits parent
                    : CombineFshPaths(parentPath, rule.Path);
            }
            else
            {
                effectivePath = rule.Path;
            }

            effectivePath = NormalizeComposedPath(effectivePath);

            // Push this rule as potential context for deeper children.
            if (!string.IsNullOrEmpty(effectivePath))
                stack.Push((indentLen, effectivePath));

            // Yield rule with effective path (unchanged when there is no parent context).
            if (effectivePath == rule.Path)
            {
                yield return rule;
            }
            else
            {
                var clone = CloneRuleWithPath(rule, effectivePath);
                yield return clone ?? rule;
            }
        }
    }

    private static string CombineFshPaths(string parentPath, string childPath)
    {
        if (string.IsNullOrEmpty(parentPath)) return childPath;
        if (string.IsNullOrEmpty(childPath)) return NormalizeComposedPath(parentPath);

        var parent = parentPath.TrimEnd('.');
        var child = childPath.TrimStart('.');
        return $"{parent}.{child}";
    }

    private static string NormalizeComposedPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path ?? string.Empty;
        if (path == ".") return path;
        return path.TrimEnd('.');
    }

    private static void ApplySdRules(
        IEnumerable<FshRule> rules,
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        // C-FP1: Apply indented-rule path composition before processing.
        // This expands relative child paths (e.g. `* value[x]` under `* extension[x]`)
        // into their fully-qualified form (`extension[x].value[x]`).
        // Soft-index state is shared across all caret rules so that [+]/[=] pairs on
        // compound SD-level caret paths (e.g. ^context[+].type + ^context[=].expression) work.
        var caretSoftIndexState = new Dictionary<string, int>(StringComparer.Ordinal);
        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);
        foreach (var rule in ComposeIndentedPaths(rules))
        {
            switch (rule)
            {
                case CardRule cardRule:
                    ApplyCardRule(cardRule, sd);
                    break;

                case LrCardRule lrCard:
                    ApplyCardCore(lrCard.Path, lrCard.Cardinality, lrCard.Flags, sd);
                    break;

                case FlagRule flagRule:
                    ApplyFlagRule(flagRule, sd);
                    break;

                case LrFlagRule lrFlag:
                    ApplyFlagCore(lrFlag.Path, lrFlag.AdditionalPaths, lrFlag.Flags, sd);
                    break;

                case ValueSetRule valueSetRule:
                    ApplyValueSetRule(valueSetRule, sd, context);
                    break;

                case FixedValueRule fixedValueRule:
                    ApplyFixedValueRule(fixedValueRule, sd, opts.Inspector, context.ResolveAlias, canonicalResolver);
                    break;

                case ContainsRule containsRule:
                    ApplyContainsRule(containsRule, sd, context, opts);
                    break;

                case OnlyRule onlyRule:
                    ApplyOnlyRule(onlyRule, sd, context, opts);
                    break;

                case ObeysRule obeysRule:
                    ApplyObeysRule(obeysRule, sd, context);
                    break;

                case CaretValueRule caretValueRule:
                    ApplyCaretValueRule(caretValueRule, sd, opts.Inspector, context.ResolveAlias, canonicalResolver, caretSoftIndexState, context, opts);
                    break;

                case InsertRule insertRule:
                    var resolved = RuleSetResolver.Resolve(insertRule, context);
                    if (resolved.Count > 0)
                    {
                        // C-RL1: When the insert rule has a path context, prepend it to all resolved rules.
                        var pathPrefix = insertRule.Path;
                        if (!string.IsNullOrEmpty(pathPrefix))
                        {
                            var prefixed = resolved.Select(r =>
                            {
                                if (string.IsNullOrEmpty(r.Path))
                                    return r;
                                var clone = CloneRuleWithPath(r, CombineFshPaths(pathPrefix, r.Path));
                                return clone ?? r;
                            }).ToList();
                            ApplySdRules(prefixed, sd, context, opts);
                        }
                        else
                        {
                            ApplySdRules(resolved, sd, context, opts);
                        }
                    }
                    else
                        context.Warnings.Add(new CompilerWarning
                        {
                            EntityName = sd.Name,
                            Message = $"InsertRule references unknown RuleSet '{insertRule.RuleSetReference}'; skipped.",
                            Position = insertRule.Position
                        });
                    break;

                case PathRule pathRule:
                    if (!string.IsNullOrEmpty(pathRule.Path))
                        GetOrCreateElement(pathRule.Path, sd);
                    break;

                case AddElementRule addEl:
                    ApplyAddElementRule(addEl, sd);
                    break;

                case AddCRElementRule addCr:
                    ApplyAddCRElementRule(addCr, sd);
                    break;

                default:
                    context.Warnings.Add(new CompilerWarning
                    {
                        EntityName = sd.Name,
                        Message = $"Rule type '{rule.GetType().Name}' is not supported for StructureDefinitions; skipped.",
                        Position = rule.Position
                    });
                    break;
            }
        }
    }

    // ─── Individual SD rule handlers ─────────────────────────────────────────

    private static void ApplyCardRule(CardRule cardRule, StructureDefinition sd) =>
        ApplyCardCore(cardRule.Path, cardRule.Cardinality, cardRule.Flags, sd);

    private static void ApplyCardCore(string? path, string cardinality, List<string> flags, StructureDefinition sd)
    {
        if (string.IsNullOrEmpty(path)) return;
        var ed = GetOrCreateElement(path, sd);
        var parts = cardinality.Split("..");
        int? newMin = null;
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var min))
            {
                ed.Min = min;
                newMin = min;
            }
            // FSH permits open-ended cardinality (e.g. "1..") which leaves the upper bound
            // implicit.  Sushi omits the max element in that case; an empty string would
            // otherwise serialise as ""max": """ which is invalid FHIR.
            if (!string.IsNullOrEmpty(parts[1]))
                ed.Max = parts[1];
        }
        ApplyFlags(ed, flags);

        // When a named extension slice gets a positive minimum cardinality, sushi also
        // adds (or tightens) the parent (non-sliced) extension element minimum so that FHIR
        // profiling validity rules are met (parent min ≥ sum of required slice mins).
        if (newMin > 0 && !string.IsNullOrEmpty(ed.SliceName) && IsExtensionPath(path))
        {
            // Derive the parent path by stripping the bracket-index or colon notation.
            var bracketIdx = path.IndexOf('[');
            var colonIdx   = path.IndexOf(':');
            var parentPath = bracketIdx >= 0 ? path[..bracketIdx]
                           : colonIdx   >= 0 ? path[..colonIdx]
                           : null;
            if (parentPath != null)
            {
                var parentEd = FindElement(parentPath, sd) ?? GetOrCreateElement(parentPath, sd);
                if (!parentEd.Min.HasValue || parentEd.Min.Value < newMin.Value)
                    parentEd.Min = newMin.Value;
            }
        }
    }

    private static void ApplyFlagRule(FlagRule flagRule, StructureDefinition sd) =>
        ApplyFlagCore(flagRule.Path, flagRule.AdditionalPaths, flagRule.Flags, sd);

    private static void ApplyFlagCore(
        string? path, List<string> additionalPaths, List<string> flags, StructureDefinition sd)
    {
        if (!string.IsNullOrEmpty(path))
            ApplyFlags(GetOrCreateElement(path, sd), flags);

        foreach (var ap in additionalPaths)
            ApplyFlags(GetOrCreateElement(ap, sd), flags);
    }

    private static void ApplyValueSetRule(
        ValueSetRule valueSetRule, StructureDefinition sd, CompilerContext context)
    {
        if (string.IsNullOrEmpty(valueSetRule.Path) || string.IsNullOrEmpty(valueSetRule.ValueSetName))
            return;

        var ed = GetOrCreateElement(valueSetRule.Path, sd);
        var vsName = context.ResolveAlias(valueSetRule.ValueSetName);
        // Resolve bare ValueSet names to canonical URLs.
        if (!IsAbsoluteUrl(vsName))
        {
            // Prefer ValueSets defined in the current compile batch (local IG) so that
            // local names shadow any identically-named ValueSet in the specification zip.
            if (context.ValueSetUrls.TryGetValue(vsName, out var localVsUrl))
            {
                vsName = localVsUrl;
            }
            else
            {
                var specKey = $"ValueSet#{vsName}";
                if (context.CanonicalsFromSpecificationZip.TryGetValue(specKey, out var vsCanonical))
                    vsName = vsCanonical;
            }
        }

        ed.Binding = new ElementDefinition.ElementDefinitionBindingComponent
        {
            Strength = valueSetRule.Strength?.Trim('(', ')') switch
            {
                "example" => BindingStrength.Example,
                "preferred" => BindingStrength.Preferred,
                "extensible" => BindingStrength.Extensible,
                "required" => BindingStrength.Required,
                _ => BindingStrength.Preferred
            },
            ValueSet = vsName
        };
    }

    private static void ApplyFixedValueRule(FixedValueRule fixedValueRule, StructureDefinition sd, ModelInspector? inspector, Func<string, string>? aliasResolver, IResourceResolver? canonicalResolver)
    {
        if (string.IsNullOrEmpty(fixedValueRule.Path) || fixedValueRule.Value is null) return;
        var ed = GetOrCreateElement(fixedValueRule.Path, sd);
        var dt = FhirValueMapper.ToDataType(fixedValueRule.Value, inspector, aliasResolver, canonicalResolver);
        if (dt != null)
        {
            // When the mapped value is a Coding but the target element type is CodeableConcept
            // (e.g. `* code = $system#code` on a CodeableConcept field), wrap the Coding in a
            // CodeableConcept to produce the correct pattern[x] variant.
            if (dt is Hl7.Fhir.Model.Coding coding && canonicalResolver != null && sd.Type != null)
            {
                var typeCode = ResolveElementTypeCode(fixedValueRule.Path, sd.Type, canonicalResolver);
                if (string.Equals(typeCode, "CodeableConcept", StringComparison.Ordinal))
                {
                    dt = new Hl7.Fhir.Model.CodeableConcept
                    {
                        Coding = [new Hl7.Fhir.Model.Coding
                        {
                            System  = coding.System,
                            Code    = coding.Code,
                            Display = coding.Display,
                        }]
                    };
                }
            }

            // "exactly" modifier → fixed[x]; omitted → pattern[x]
            if (fixedValueRule.Exactly)
                ed.Fixed = dt;
            else
                ed.Pattern = dt;

            // When assigning onto a choice-type variant slice whose declared type is a
            // super-type of the value's type (e.g. Coding on a valueCodeableConcept slice),
            // wrap the value so the serialized pattern[x] / fixed[x] variant is correct.
            if (!string.IsNullOrEmpty(ed.SliceName)
                && ed.Type is { Count: 1 } types
                && !string.IsNullOrEmpty(types[0].Code))
            {
                WrapChoiceSlicePattern(ed, types[0].Code);
            }
        }
    }

    private static void ApplyContainsRule(ContainsRule containsRule, StructureDefinition sd, CompilerContext context, CompilerOptions? opts = null)
    {
        if (string.IsNullOrEmpty(containsRule.Path) || containsRule.Items.Count == 0) return;

        var isExtensionPath = IsExtensionPath(containsRule.Path);
        ElementDefinition? ed;

        if (isExtensionPath)
        {
            // For extension slicing, the bare parent element is needed to carry the slicing
            // discriminator when the direct parent SD doesn't already define extension slicing.
            // When the parent has named extension slices (slicing is inherited), we skip creating
            // the bare element — RemoveRedundantSlicingAgainstBase handles inherited slicing.
            ed = FindElement(containsRule.Path, sd);
            if (ed == null && !DirectParentHasNamedExtensionSlices(containsRule.Path, sd, context, opts))
                ed = GetOrCreateElement(containsRule.Path, sd);

            // Contains rules for extensions define slice-level cardinalities on the extension
            // array. To keep cardinalities compliant with FHIR profiling rules, the unsliced
            // extension element minimum must be at least the sum of all required slice mins.
            var requiredSliceMinSum = 0;
            foreach (var item in containsRule.Items)
            {
                var parts = item.Cardinality.Split("..");
                if (parts.Length == 2 && int.TryParse(parts[0], out var min) && min > 0)
                    requiredSliceMinSum += min;
            }

            if (requiredSliceMinSum > 0)
            {
                ed ??= GetOrCreateElement(containsRule.Path, sd);
                if (!ed.Min.HasValue || ed.Min.Value < requiredSliceMinSum)
                    ed.Min = requiredSliceMinSum;
            }
        }
        else
        {
            ed = GetOrCreateElement(containsRule.Path, sd);
        }

        if (ed is not null && ed.Slicing == null)
        {
            // Per the FSH spec, the default discriminator for extension slicing is
            // {type: "value", path: "url"}.  For other elements, the discriminator is
            // typically set separately via caret rules (^slicing.discriminator.*).
            var discriminators = isExtensionPath
                ? new List<ElementDefinition.DiscriminatorComponent>
                  {
                      new()
                      {
                          Type  = ElementDefinition.DiscriminatorType.Value,
                          Path  = "url"
                      }
                  }
                : new List<ElementDefinition.DiscriminatorComponent>();

            ed.Slicing = new ElementDefinition.SlicingComponent
            {
                Rules        = ElementDefinition.SlicingRules.Open,
                Ordered      = false,
                Discriminator = discriminators
            };
        }

        foreach (var item in containsRule.Items)
        {
            // When the "named" keyword is present:
            //   name(0) = type alias (e.g. the extension profile), name(1) = slice name.
            // When absent:
            //   name(0) = slice name (and no separate type is implied).
            var sliceName = item.NamedAlias ?? item.Name;
            var sliceEd = GetOrCreateElement($"{containsRule.Path}:{sliceName}", sd);
            var parts = item.Cardinality.Split("..");
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out var min))
                    sliceEd.Min = min;
                sliceEd.Max = parts[1];
            }
            ApplyFlags(sliceEd, item.Flags);

            // Gap 10: when NamedAlias is set, populate ed.Type with the type from item.Name.
            if (item.NamedAlias != null)
            {
                var resolvedType = context.ResolveAlias(item.Name);
                if (isExtensionPath)
                {
                    var resolvedProfile = ResolveBaseDefinitionCanonical(resolvedType, item.Name, context, opts);
                    // URL-encode brackets in the profile URL (e.g. value[x] → value%5Bx%5D).
                    // Brackets are not valid unencoded in URIs and sushi always percent-encodes them.
                    if (resolvedProfile.Contains('[') || resolvedProfile.Contains(']'))
                        resolvedProfile = resolvedProfile
                            .Replace("[", "%5B", StringComparison.Ordinal)
                            .Replace("]", "%5D", StringComparison.Ordinal);
                    sliceEd.Type =
                    [
                        new ElementDefinition.TypeRefComponent
                        {
                            Code = "Extension",
                            Profile = [resolvedProfile]
                        }
                    ];
                }
                else
                {
                    sliceEd.Type =
                    [
                        new ElementDefinition.TypeRefComponent { Code = resolvedType }
                    ];
                }
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="fshPath"/> refers to an extension element,
    /// i.e. when the final segment (after the last <c>'.'</c>) is <c>"extension"</c> or
    /// <c>"modifierExtension"</c> (both use value-URL discriminator slicing and require an
    /// Extension type profile).
    /// </summary>
    private static bool IsExtensionPath(string fshPath)
    {
        if (string.IsNullOrEmpty(fshPath)) return false;
        var lastDot = fshPath.LastIndexOf('.');
        var lastSeg = lastDot >= 0 ? fshPath[(lastDot + 1)..] : fshPath;
        // Strip slice notation: colon form "extension:name" or bracket form "extension[name]".
        var colonPos = lastSeg.IndexOf(':');
        if (colonPos >= 0) lastSeg = lastSeg[..colonPos];
        var bracketPos = lastSeg.IndexOf('[');
        if (bracketPos >= 0) lastSeg = lastSeg[..bracketPos];
        return string.Equals(lastSeg, "extension", StringComparison.OrdinalIgnoreCase)
            || string.Equals(lastSeg, "modifierExtension", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects redundant <c>only</c> rules on extension-related elements that sushi omits:
    /// <c>only Extension</c> applied to a sub-extension slice (path Extension.extension
    /// with a SliceName) or <c>only uri</c> applied to an Extension.url element.
    /// </summary>
    private static bool IsRedundantOnlyForExtension(
        ElementDefinition ed, IReadOnlyList<string> targetTypes)
    {
        if (ed.Path == "Extension.extension" && !string.IsNullOrEmpty(ed.SliceName)
            && targetTypes.Count == 1
            && string.Equals(targetTypes[0].Trim(), "Extension", StringComparison.Ordinal))
        {
            return true;
        }
        if ((ed.Path == "Extension.url"
             || (ed.Path != null && ed.Path.EndsWith(".url", StringComparison.Ordinal)
                 && ed.Path.StartsWith("Extension.", StringComparison.Ordinal)))
            && targetTypes.Count == 1
            && string.Equals(targetTypes[0].Trim(), "uri", StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private static void ApplyOnlyRule(OnlyRule onlyRule, StructureDefinition sd, CompilerContext context, CompilerOptions? opts = null)
    {
        if (string.IsNullOrEmpty(onlyRule.Path) || onlyRule.TargetTypes.Count == 0) return;
        var ed = GetOrCreateElement(onlyRule.Path, sd);

        // Redundant `only Extension` on a sub-extension slice (or `only uri` on an
        // extension's url element) adds no information — the base already pins the type.
        // Sushi omits these, so skip the assignment here.
        if (IsRedundantOnlyForExtension(ed, onlyRule.TargetTypes))
            return;

        // Sushi orders the emitted `type` list according to the base element's declared
        // FHIR type array (PropertyMapping.FhirType[]), not the FSH source order nor
        // alphabetically.  When we can resolve the base property's choice-type ordering,
        // use it as the primary sort key so that e.g. `only Quantity or string` on
        // Observation.value[x] serialises in the order declared by the base element.
        //
        // Special case: when the path targets a named extension slice (e.g. extension[adheresTo].value[x]),
        // sushi looks up the specific named extension's SD for type ordering.  When that extension SD
        // cannot be resolved (e.g. it is from an external package not in the local resolver), sushi
        // preserves the FSH source order instead of alphabetical.
        var typeRefs = onlyRule.TargetTypes.Select(tt => ParseTypeRef(tt, context, opts)).ToList();
        var (baseOrder, useSourceOrder) = GetBaseChoiceTypeOrder(onlyRule.Path, sd, opts?.Inspector, opts?.Resolver);
        if (baseOrder != null && baseOrder.Count > 1)
        {
            ed.Type = typeRefs
                .Select((t, idx) => (t, idx,
                    baseIdx: baseOrder.TryGetValue(t.Code ?? string.Empty, out var bi) ? bi : int.MaxValue))
                .OrderBy(x => x.baseIdx)
                .ThenBy(x => x.idx)
                .Select(x => x.t)
                .ToList();
        }
        else if (useSourceOrder)
        {
            // An external named extension was detected whose SD cannot be resolved.
            // Preserve FSH source order, matching sushi's behaviour.
            ed.Type = typeRefs;
        }
        else
        {
            // General fallback: primitive datatypes (lowercase initial) before complex datatypes /
            // resources (uppercase initial), then alphabetical by type code within each group.
            // Matches sushi's behaviour for "any DataType" elements like Extension.value[x].
            ed.Type = typeRefs
                .Select((t, idx) => (t, idx))
                .OrderBy(x => !string.IsNullOrEmpty(x.t.Code) && char.IsUpper(x.t.Code[0]) ? 1 : 0)
                .ThenBy(x => x.t.Code, StringComparer.Ordinal)
                .ThenBy(x => x.idx)
                .Select(x => x.t)
                .ToList();
        }
    }

    /// <summary>
    /// Resolves the base element's declared choice-type ordering for <paramref name="path"/>
    /// on <paramref name="sd"/>.  Returns a tuple:
    /// <list type="bullet">
    ///   <item><description><c>Order != null</c> — use this ordering.</description></item>
    ///   <item><description><c>Order == null, UseSourceOrder = true</c> — an external named
    ///     extension was detected but could not be resolved; caller should use FSH source order.</description></item>
    ///   <item><description><c>Order == null, UseSourceOrder = false</c> — general fallback;
    ///     caller should use alphabetical-within-tier ordering.</description></item>
    /// </list>
    /// Prefers the <paramref name="resolver"/> (StructureDefinition-based navigation) and
    /// falls back to the <paramref name="inspector"/> (ModelInspector-based walk) when the
    /// resolver is unavailable or does not find the element.
    /// When the path includes an external extension slice (e.g. <c>extension[adheresTo].value[x]</c>)
    /// whose SD cannot be resolved, returns <c>(null, true)</c> so the caller preserves FSH source order.
    /// </summary>
    private static (Dictionary<string, int>? Order, bool UseSourceOrder) GetBaseChoiceTypeOrder(
        string path, StructureDefinition sd, ModelInspector? inspector, IResourceResolver? resolver = null)
    {
        // When the path targets a specific named extension slice (e.g. extension[adheresTo].value[x]),
        // try to resolve the named extension's SD directly and return its value[x] type ordering.
        // If a named external extension exists as a slice with a type.profile but cannot be
        // resolved, return (null, true) so the caller uses FSH source order (not the generic
        // Extension.value[x] 50-type ordering which would produce wrong results).
        if (resolver != null)
        {
            var namedExtResult = GetChoiceTypeOrderForNamedExtensionSlice(path, sd, resolver);
            if (namedExtResult.Resolved) return (namedExtResult.Order, namedExtResult.Order == null);  // unresolvable external → UseSourceOrder=true
        }

        // Preferred: resolver-based lookup using StructureDefinition element lists.
        if (resolver != null && !string.IsNullOrEmpty(sd.Type))
        {
            var result = GetChoiceTypeOrderFromResolver(path, sd.Type, resolver);
            if (result != null) return (result, false);
        }

        // Fallback: ModelInspector-based property walk.
        if (inspector is null || string.IsNullOrEmpty(sd.Type)) return (null, false);
        var current = inspector.FindClassMapping(sd.Type);
        if (current is null) return (null, false);

        var segments = path.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var colon = seg.IndexOf(':');
            if (colon >= 0) seg = seg[..colon];
            var bracket = seg.IndexOf('[');
            if (bracket >= 0) seg = seg[..bracket];
            if (string.IsNullOrEmpty(seg)) return (null, false);

            var propMap = current.FindMappedElementByName(seg);
            if (propMap is null) return (null, false);

            if (i == segments.Length - 1)
            {
                var fhirTypes = propMap.FhirType;
                if (fhirTypes is null || fhirTypes.Length == 0) return (null, false);

                // Skip the "any DataType" case (Extension.value[x] in R4 resolves its
                // FhirType to just the abstract DataType base class). An abstract-only
                // entry carries no ordering information, so fall through to the
                // alphabetical-within-tier fallback.
                if (fhirTypes.Length == 1 && fhirTypes[0].IsAbstract) return (null, false);

                var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < fhirTypes.Length; j++)
                {
                    var cm = inspector.FindClassMapping(fhirTypes[j]);
                    if (cm != null && !order.ContainsKey(cm.Name))
                        order[cm.Name] = j;
                }
                return (order.Count > 0 ? order : null, false);
            }

            var nextType = propMap.ImplementingType;
            if (nextType is null) return (null, false);
            current = inspector.FindClassMapping(nextType);
            if (current is null) return (null, false);
        }
        return (null, false);
    }

    /// <summary>
    /// Checks whether <paramref name="path"/> begins with an extension slice segment such as
    /// <c>extension[adheresTo]</c> or <c>modifierExtension[foo]</c>, and if so tries to look
    /// up the specific named extension's profile URL from <paramref name="sd"/>'s differential,
    /// then resolve that extension's <c>value[x]</c> type ordering.
    /// Returns a tuple where <c>Resolved</c> is <c>true</c> when an external extension profile
    /// was detected (and <c>Order</c> is the resolved ordering or <c>null</c> when the SD could
    /// not be found), or <c>false</c> when no external extension slice was detected (fall through
    /// to the generic resolver path).
    /// </summary>
    private static (bool Resolved, Dictionary<string, int>? Order) GetChoiceTypeOrderForNamedExtensionSlice(
        string path, StructureDefinition sd, IResourceResolver resolver)
    {
        // Only applies to paths that start with "extension[name]..." or "modifierExtension[name]...".
        var firstDot = path.IndexOf('.');
        var firstSegment = firstDot >= 0 ? path[..firstDot] : path;
        var bracketOpen = firstSegment.IndexOf('[');
        var bracketClose = firstSegment.IndexOf(']');
        if (bracketOpen < 0 || bracketClose <= bracketOpen) return (false, null);

        var baseElementName = firstSegment[..bracketOpen]; // "extension" or "modifierExtension"
        if (!string.Equals(baseElementName, "extension", StringComparison.Ordinal)
            && !string.Equals(baseElementName, "modifierExtension", StringComparison.Ordinal))
            return (false, null);

        var sliceName = firstSegment[(bracketOpen + 1)..bracketClose];
        if (string.IsNullOrEmpty(sliceName) || sliceName.StartsWith('+') || sliceName.StartsWith('='))
            return (false, null);

        // Look up the extension element for this slice in the SD differential to get its profile URL.
        var sliceElementPath = (sd.Type ?? string.Empty) + "." + baseElementName + ":" + sliceName;
        var elements = (IList<ElementDefinition>?)sd.Differential?.Element;
        if (elements == null) return (false, null);

        var sliceEl = elements.FirstOrDefault(e =>
            e.Path == (sd.Type + "." + baseElementName) && e.SliceName == sliceName);
        if (sliceEl == null)
        {
            // Try the snapshot for lookup of already-expanded elements.
            sliceEl = sd.Snapshot?.Element?.FirstOrDefault(e =>
                e.Path == (sd.Type + "." + baseElementName) && e.SliceName == sliceName);
        }

        // If no type or no external profile URL → this is an inline extension slice; fall through.
        var profileUrl = sliceEl?.Type?.FirstOrDefault()?.Profile?.FirstOrDefault();
        if (string.IsNullOrEmpty(profileUrl)) return (false, null);

        // We have an external extension URL. Try to resolve it.
        var extSd = FindStructureDefinitionByUrl(profileUrl, resolver);
        if (extSd == null)
        {
            // External extension exists but can't be resolved → signal that no ordering is
            // available so the caller uses FSH source order.
            return (true, null);
        }

        // Resolve the value[x] element within the extension SD.
        var tailPath = firstDot >= 0 ? path[(firstDot + 1)..] : string.Empty;
        if (!string.IsNullOrEmpty(tailPath))
        {
            var extOrder = GetChoiceTypeOrderFromResolver(tailPath, extSd.Type ?? "Extension", resolver);
            return (true, extOrder);
        }

        return (true, null);
    }

    /// <summary>
    /// Finds a <see cref="StructureDefinition"/> by canonical URL using <paramref name="resolver"/>.
    /// Returns <c>null</c> when not found.
    /// </summary>
    private static StructureDefinition? FindStructureDefinitionByUrl(string url, IResourceResolver resolver)
    {
        try { return resolver.FindStructureDefinition(url); }
        catch { return null; }
    }

    /// <summary>
    /// Resolves choice-type ordering by navigating the base StructureDefinition's element list.
    /// Strips slice names and array indices from the path segments, constructs the full canonical
    /// path, and looks for the element in the snapshot (preferred, as it contains all inherited
    /// elements) or differential (fallback for non-expanded SDs).
    /// Returns <c>null</c> when the element or its types cannot be resolved.
    /// </summary>
    private static Dictionary<string, int>? GetChoiceTypeOrderFromResolver(
        string path, string sdType, IResourceResolver resolver)
    {
        // Strip slice names (colon syntax) and array indices from path segments.
        // Example: "item:condition.answerOption.value[x]" → "item.answerOption.value[x]"
        static string StripSlicesAndIndices(string rawPath)
        {
            var segs = rawPath.Split('.');
            for (int i = 0; i < segs.Length; i++)
            {
                var s = segs[i];
                var colon = s.IndexOf(':');
                if (colon >= 0) s = s[..colon];
                var bracket = s.IndexOf('[');
                if (bracket >= 0) s = s[..bracket];
                segs[i] = s;
            }
            return string.Join('.', segs);
        }

        var cleanPath = StripSlicesAndIndices(path);

        // Walk the path segment by segment. Each segment may belong to a different
        // StructureDefinition (e.g. when the path goes through an 'extension' segment,
        // we follow into the Extension SD).
        // segIndex advances by one per outer iteration, so the loop is always finite.
        var segments = cleanPath.Split('.');
        var currentType = sdType;
        var segIndex = 0;

        while (segIndex < segments.Length)
        {
            var seg = segments[segIndex];
            if (string.IsNullOrEmpty(seg)) return null;

            var typeSd = FindStructureDefinitionForType(currentType, resolver);
            if (typeSd == null) return null;

            // Snapshot is preferred because it contains all inherited elements.
            // Differential is used as fallback for SDs that have not been expanded.
            var elements = (IList<ElementDefinition>?)typeSd.Snapshot?.Element ?? typeSd.Differential?.Element;
            if (elements == null) return null;

            // Build the full path using the remaining segments and try an exact match.
            var remainingPath = string.Join('.', segments[segIndex..]);
            var fullElementPath = currentType + "." + remainingPath;
            var el = elements.FirstOrDefault(e => e.Path == fullElementPath);
            if (el != null)
            {
                // Found the target element.  Return the type codes in order (empty list → null).
                if (el.Type == null || el.Type.Count == 0) return null;
                var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < el.Type.Count; j++)
                {
                    var code = el.Type[j].Code;
                    if (!string.IsNullOrEmpty(code) && !order.ContainsKey(code))
                        order[code] = j;
                }
                return order.Count > 0 ? order : null;
            }

            // Exact match not found at this level — navigate one step by following the
            // current segment's element type into a nested SD.
            // When the element is polymorphic (multiple types), we follow Type[0] because
            // we're navigating intermediate path segments, not a final choice element.
            // The final element is found by the exact-match check above.
            var parentPath = currentType + "." + seg;
            var parentEl = elements.FirstOrDefault(e => e.Path == parentPath);
            if (parentEl == null || parentEl.Type == null || parentEl.Type.Count == 0) return null;

            var nextTypeCode = parentEl.Type[0].Code;
            if (string.IsNullOrEmpty(nextTypeCode)) return null;

            currentType = nextTypeCode;
            segIndex++;
        }

        return null;
    }

    /// <summary>
    /// Returns the first FHIR type code for the element at <paramref name="fshPath"/> in the
    /// base StructureDefinition identified by <paramref name="sdType"/> (e.g. <c>"ServiceRequest"</c>).
    /// Navigates through intermediate types when the path has multiple segments.
    /// Returns <c>null</c> when the type cannot be resolved.
    /// </summary>
    /// <param name="fshPath">The FSH path of the element (e.g. <c>"code"</c>).</param>
    /// <param name="sdType">The FHIR resource/datatype name (e.g. <c>"ServiceRequest"</c>).</param>
    /// <param name="resolver">The resource resolver used to look up base StructureDefinitions.</param>
    private static string? ResolveElementTypeCode(string fshPath, string sdType, IResourceResolver resolver)
    {
        if (string.IsNullOrEmpty(fshPath) || string.IsNullOrEmpty(sdType)) return null;

        // Strip slice notation and array indices from path segments.
        static string StripSlicesAndIndices(string rawPath)
        {
            var segs = rawPath.Split('.');
            for (int i = 0; i < segs.Length; i++)
            {
                var s = segs[i];
                var colon = s.IndexOf(':');
                if (colon >= 0) s = s[..colon];
                var bracket = s.IndexOf('[');
                if (bracket >= 0) s = s[..bracket];
                segs[i] = s;
            }
            return string.Join('.', segs);
        }

        var cleanPath = StripSlicesAndIndices(fshPath);
        var segments = cleanPath.Split('.');
        var currentType = sdType;
        var segIndex = 0;

        while (segIndex < segments.Length)
        {
            var seg = segments[segIndex];
            if (string.IsNullOrEmpty(seg)) return null;

            var typeSd = FindStructureDefinitionForType(currentType, resolver);
            if (typeSd == null) return null;

            var elements = (IList<ElementDefinition>?)typeSd.Snapshot?.Element ?? typeSd.Differential?.Element;
            if (elements == null) return null;

            var remainingPath = string.Join('.', segments[segIndex..]);
            var fullElementPath = currentType + "." + remainingPath;
            var el = elements.FirstOrDefault(e => e.Path == fullElementPath);
            if (el != null)
                return el.Type?.FirstOrDefault()?.Code;

            // Navigate one step.
            var parentPath = currentType + "." + seg;
            var parentEl = elements.FirstOrDefault(e => e.Path == parentPath);
            if (parentEl == null || parentEl.Type == null || parentEl.Type.Count == 0) return null;

            var nextTypeCode = parentEl.Type[0].Code;
            if (string.IsNullOrEmpty(nextTypeCode)) return null;

            currentType = nextTypeCode;
            segIndex++;
        }

        return null;
    }


    /// <summary>
    /// Parses a FSH target-type expression into a Firely <see cref="ElementDefinition.TypeRefComponent"/>.
    /// Handles bare type names as well as <c>Reference(...)</c>, <c>Canonical(...)</c>,
    /// and <c>CodeableReference(...)</c> expressions with optional " or "-separated targets.
    /// </summary>
    private static ElementDefinition.TypeRefComponent ParseTypeRef(string typeExpr, CompilerContext context, CompilerOptions? opts = null)
    {
        typeExpr = typeExpr.Trim();

        // Reference(X) or Reference(X or Y or ...)
        if (typeExpr.StartsWith("Reference(", StringComparison.Ordinal) && typeExpr.EndsWith(")"))
        {
            var inner = typeExpr[10..^1];
            var targets = SplitOrTargets(inner)
                .Select(t => ResolveTargetProfile(context.ResolveAlias(t), context, opts))
                .ToList();
            return new ElementDefinition.TypeRefComponent { Code = "Reference", TargetProfile = targets };
        }

        // Canonical(X|version) or Canonical(X or Y)
        if (typeExpr.StartsWith("Canonical(", StringComparison.Ordinal) && typeExpr.EndsWith(")"))
        {
            var inner = typeExpr[10..^1];
            var targets = SplitOrTargets(inner)
                .Select(t => ResolveTargetProfile(context.ResolveAlias(t), context, opts))
                .ToList();
            return new ElementDefinition.TypeRefComponent { Code = "canonical", TargetProfile = targets };
        }

        // CodeableReference(X) or CodeableReference(X or Y)
        if (typeExpr.StartsWith("CodeableReference(", StringComparison.Ordinal) && typeExpr.EndsWith(")"))
        {
            var inner = typeExpr[18..^1];
            var targets = SplitOrTargets(inner)
                .Select(t => ResolveTargetProfile(context.ResolveAlias(t), context, opts))
                .ToList();
            return new ElementDefinition.TypeRefComponent { Code = "CodeableReference", TargetProfile = targets };
        }

        // Bare type name (e.g. Quantity, string, boolean) — resolve through aliases as well
        var resolved = context.ResolveAlias(typeExpr);

        // If the name refers to a profile of a core FHIR type (e.g. SimpleQuantity → Quantity),
        // emit { Code = baseType, Profile = [canonicalUrl] } to match sushi behaviour.
        // Prefer resolver-based lookup (no version-specific inspector required).
        // Note: some FHIR model assemblies (e.g. R4 Firely) include POCO ClassMappings for
        // profiled datatypes like SimpleQuantity, so we check the resolver directly and
        // trust the StructureDefinition's Type field to identify the underlying FHIR type.
        if (opts?.Resolver is { } resolver
            && !resolved.Contains("://", StringComparison.Ordinal))
        {
            // Only emit a Profile constraint when the name isn't already a bare FHIR resource type.
            var resolvedSd = resolver.FindStructureDefinition("http://hl7.org/fhir/StructureDefinition/" + resolved)
                          ?? resolver.FindStructureDefinition(resolved);

            // Also look up in-IG compiled SDs (e.g. extension profiles like AssembleExpectation).
            if (resolvedSd == null)
                context.CompiledStructureDefinitions.TryGetValue(resolved, out resolvedSd);

            var isKnownResource = resolvedSd?.Kind == StructureDefinition.StructureDefinitionKind.Resource
                || opts.Inspector?.IsKnownResource(resolved) == true;
            if (!isKnownResource && resolvedSd is not null
                && !string.IsNullOrEmpty(resolvedSd.Type)
                && !string.IsNullOrEmpty(resolvedSd.Url)
                && !string.Equals(resolvedSd.Type, resolved, StringComparison.Ordinal))
            {
                return new ElementDefinition.TypeRefComponent
                {
                    Code = resolvedSd.Type,
                    Profile = new List<string> { resolvedSd.Url }
                };
            }
        }

        return new ElementDefinition.TypeRefComponent { Code = resolved };
    }

    /// <summary>
    /// Resolves a FSH target-profile name (e.g. <c>Bundle</c>, <c>Patient</c>) to its canonical
    /// StructureDefinition URL.  Absolute URLs and URN/URI-prefixed strings pass through unchanged.
    /// Falls back to the raw name when no resolution is possible (e.g. unknown user-defined profile).
    /// </summary>
    private static string ResolveTargetProfile(string target, CompilerContext context, CompilerOptions? opts)
    {
        if (string.IsNullOrEmpty(target)) return target;
        if (IsAbsoluteUrl(target)) return target;
        // URN/URI-prefixed strings (urn:oid:…, urn:uuid:…) also pass through.
        if (target.StartsWith("urn:", StringComparison.Ordinal)) return target;
        return ResolveBaseDefinitionCanonical(target, target, context, opts);
    }

    private static IEnumerable<string> SplitOrTargets(string inner) =>
        inner.Split([" or "], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
             .Select(t => t.Trim());

    private static void ApplyObeysRule(ObeysRule obeysRule, StructureDefinition sd, CompilerContext context)
    {
        if (obeysRule.InvariantNames.Count == 0) return;

        ElementDefinition targetEd;
        if (string.IsNullOrEmpty(obeysRule.Path))
            targetEd = sd.Differential.Element.First();
        else
            targetEd = GetOrCreateElement(obeysRule.Path, sd);

        targetEd.Constraint ??= new List<ElementDefinition.ConstraintComponent>();
        foreach (var invName in obeysRule.InvariantNames)
        {
            var constraint = new ElementDefinition.ConstraintComponent { Key = invName };

            // Populate from the referenced Invariant entity when available in context
            if (context.Invariants.TryGetValue(invName, out var inv))
            {
                constraint.Human = inv.Description;
                constraint.Expression = inv.Expression;
                constraint.Xpath = inv.XPath;
                constraint.Severity = inv.Severity?.TrimStart('#').ToLowerInvariant() switch
                {
                    "warning" => ConstraintSeverity.Warning,
                    _ => ConstraintSeverity.Error
                };
            }
            else
            {
                constraint.Severity = ConstraintSeverity.Error;
            }

            // Sushi stamps the owning StructureDefinition's canonical URL as the
            // constraint source so consumers can trace the invariant back to its
            // defining profile.
            if (!string.IsNullOrEmpty(sd.Url))
                constraint.Source = sd.Url;

            targetEd.Constraint.Add(constraint);
        }
    }

    private static void ApplyCaretValueRule(
        CaretValueRule caretValueRule,
        StructureDefinition sd,
        ModelInspector? inspector,
        Func<string, string>? aliasResolver,
        IResourceResolver canonicalResolver,
        Dictionary<string, int>? softIndexState = null,
        CompilerContext? context = null,
        CompilerOptions? opts = null)
    {
        if (string.IsNullOrEmpty(caretValueRule.CaretPath)) return;

        // Caret rules without a path target the StructureDefinition itself.
        // Path "." refers to the root ElementDefinition, not the SD.
        if (string.IsNullOrEmpty(caretValueRule.Path))
        {
            ApplySdCaretPath(caretValueRule, sd, inspector, aliasResolver, canonicalResolver, softIndexState);
        }
        else
        {
            var ed = GetOrCreateElement(caretValueRule.Path, sd);
            // Adjust absolute constraint[N] indices to differential-local indices.
            // FSH indexes constraints against the full snapshot (base + own), but our
            // differential only has the profile's own constraints.  Subtract the number
            // of inherited constraints from the parent SD to get the local index.
            var adjustedRule = AdjustConstraintIndex(caretValueRule, ed, sd, context, opts);
            // Pre-seed inherited list-typed element properties from the base element before
            // applying indexed caret assignments.  For example, `* . ^alias[0] = "Form Data"`
            // must preserve base-inherited aliases at other indices (e.g. alias[1]).
            PreSeedInheritedListFromBase(caretValueRule.CaretPath.TrimStart('^'), ed, sd, context, opts);
            // Each element gets its own soft-index state for element-level compound paths.
            ApplyEdCaretPath(adjustedRule, ed, inspector, aliasResolver, canonicalResolver);

            // When caret rules assign a pattern/fixed value onto a choice-type variant
            // slice and the value's type is a sub-type of the slice's declared type
            // (e.g. ^patternCoding on a valueCodeableConcept slice), wrap the value in
            // the correct container so the serialised pattern[x] / fixed[x] variant
            // matches the slice's declared type.
            if (!string.IsNullOrEmpty(ed.SliceName)
                && ed.Type is { Count: 1 } types
                && !string.IsNullOrEmpty(types[0].Code))
            {
                WrapChoiceSlicePattern(ed, types[0].Code);
            }
        }
    }

    /// <summary>
    /// When a caret path targets a specific index of a list property on an
    /// <see cref="ElementDefinition"/> (e.g. <c>alias[0]</c>), pre-seeds the element's
    /// list with values inherited from the corresponding base element so that indices not
    /// touched by FSH rules retain their inherited values.
    /// Does nothing when the property is not a list, the index is 0 and the list is already
    /// populated, or no base element is found.
    /// </summary>
    private static void PreSeedInheritedListFromBase(
        string caretPath,
        ElementDefinition ed,
        StructureDefinition sd,
        CompilerContext? context,
        CompilerOptions? opts)
    {
        if (context == null || opts == null) return;

        // Only applies to indexed paths like "alias[0]", "alias[1]", etc.
        var bracketStart = caretPath.IndexOf('[');
        if (bracketStart < 0) return;
        var bracketEnd = caretPath.IndexOf(']', bracketStart);
        if (bracketEnd < 0) return;
        var propertyName = caretPath[..bracketStart];
        var indexStr = caretPath[(bracketStart + 1)..bracketEnd];
        if (!int.TryParse(indexStr, out _)) return;

        // Resolve the base element from the parent SD.
        if (string.IsNullOrEmpty(sd.BaseDefinition)) return;
        var baseSd = ResolveStructureDefinition(sd.BaseDefinition, context, opts);
        if (baseSd == null) return;

        var basePath = RewritePathRoot(ed.Path ?? string.Empty, sd.Type, baseSd.Type);
        var source = (IEnumerable<ElementDefinition>?)baseSd.Snapshot?.Element
                  ?? baseSd.Differential?.Element;
        var baseEl = source?.FirstOrDefault(e =>
            e.Path == basePath && string.IsNullOrEmpty(e.SliceName));
        if (baseEl == null) return;

        // Look for a property whose FHIR name matches caretPath property.
        // We use the Firely SDK ModelInspector mapping.
        var mi = Hl7.Fhir.Introspection.ModelInspector.ForAssembly(typeof(ElementDefinition).Assembly);
        var classMap = mi?.FindClassMapping(typeof(ElementDefinition));
        if (classMap == null) return;

        var propMap = classMap.FindMappedElementByName(propertyName);
        if (propMap == null || !propMap.IsCollection) return;

        // Get the base list value.
        var baseList = propMap.GetValue(baseEl) as System.Collections.IList;
        if (baseList == null || baseList.Count == 0) return;

        // Get (or create) the list on the differential element.
        var edList = propMap.GetValue(ed) as System.Collections.IList;
        if (edList == null)
        {
            var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
            edList = (System.Collections.IList)Activator.CreateInstance(listType)!;
            propMap.SetValue(ed, edList);
        }

        // Only seed if the ed list is currently empty (don't overwrite explicit FSH values).
        if (edList.Count > 0) return;

        // Copy base list items into the ed list.
        foreach (var item in baseList)
            edList.Add(item);
    }

    /// <summary>
    /// When a caret rule targets <c>constraint[N]</c> and N is beyond the current
    /// differential constraint list, the index is absolute (snapshot-level) and must be
    /// adjusted by subtracting the number of inherited base constraints.
    /// </summary>
    private static CaretValueRule AdjustConstraintIndex(
        CaretValueRule rule,
        ElementDefinition ed,
        StructureDefinition sd,
        CompilerContext? context,
        CompilerOptions? opts)
    {
        var caretPath = rule.CaretPath.TrimStart('^');
        // Only adjust when the path starts with "constraint[N]" and N is out of bounds.
        if (!caretPath.StartsWith("constraint[", StringComparison.Ordinal)) return rule;

        var closeBracket = caretPath.IndexOf(']');
        if (closeBracket < 0) return rule;
        var indexStr = caretPath[11..closeBracket]; // After "constraint["
        if (!int.TryParse(indexStr, out var absIndex)) return rule;

        var ownCount = ed.Constraint?.Count ?? 0;
        if (absIndex < ownCount) return rule; // Index is already within own constraint list.

        // Need to determine how many base constraints the element inherits from the
        // entire parent chain.  A single level's differential only lists constraints
        // added at that level, so walk up the BaseDefinition chain and accumulate.
        // When a level has a snapshot (e.g. core FHIR resources from the spec ZIP),
        // its snapshot count is authoritative for that level and above, so stop there.
        int baseCount = 0;
        if (context != null && opts != null)
        {
            var currentBaseDef = sd.BaseDefinition;
            var currentFromType = sd.Type;
            var currentPath = ed.Path ?? string.Empty;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(currentBaseDef) && visited.Add(currentBaseDef))
            {
                var baseSd = ResolveStructureDefinition(currentBaseDef, context, opts);
                if (baseSd == null) break;

                var basePath = RewritePathRoot(currentPath, currentFromType, baseSd.Type);

                // If a snapshot is available, it already contains all inherited
                // constraints — use it and stop walking.
                if (baseSd.Snapshot?.Element != null)
                {
                    var snapEl = baseSd.Snapshot.Element.FirstOrDefault(e =>
                        e.Path == basePath && string.IsNullOrEmpty(e.SliceName));
                    if (snapEl != null)
                    {
                        baseCount += snapEl.Constraint?.Count ?? 0;
                        break;
                    }
                }

                // Otherwise, add whatever this level's differential contributes and
                // continue walking up.
                var diffEl = baseSd.Differential?.Element.FirstOrDefault(e =>
                    e.Path == basePath && string.IsNullOrEmpty(e.SliceName));
                if (diffEl != null)
                    baseCount += diffEl.Constraint?.Count ?? 0;

                currentBaseDef = baseSd.BaseDefinition;
                currentFromType = baseSd.Type;
                currentPath = basePath;
            }
        }

        if (baseCount == 0) return rule;

        var localIndex = absIndex - baseCount;
        if (localIndex < 0 || localIndex >= ownCount) return rule;

        // Return a copy of the rule with the adjusted index.
        var adjustedPath = "^constraint[" + localIndex + "]" + caretPath[(closeBracket + 1)..];
        return new CaretValueRule
        {
            Path = rule.Path,
            CaretPath = adjustedPath,
            Value = rule.Value,
            Position = rule.Position,
            Indent = rule.Indent,
        };
    }

    private static void ApplySdCaretPath(
        CaretValueRule rule,
        StructureDefinition sd,
        ModelInspector? inspector,
        Func<string, string>? aliasResolver,
        IResourceResolver canonicalResolver,
        Dictionary<string, int>? softIndexState = null)
    {
        var path = rule.CaretPath.TrimStart('^');

        // Try compound-path navigation first (handles context.type, context[+].type, etc.).
        if (path.Contains('.') || path.Contains('['))
        {
            var state = softIndexState ?? new Dictionary<string, int>(StringComparer.Ordinal);
            if (FhirCaretValueWriter.TrySetCompound(sd, path, rule.Value, state, inspector, aliasResolver, canonicalResolver))
                return;
        }
        else if (FhirCaretValueWriter.TrySet(sd, path, rule.Value, inspector, aliasResolver, canonicalResolver))
        {
            return;
        }

        // Fall back to an extension for paths not in the StructureDefinition model.
        sd.Extension ??= new List<FhirExtension>();
        sd.Extension.Add(new FhirExtension
        {
            Url = path,
            Value = FhirValueMapper.ToDataType(rule.Value, inspector, aliasResolver, canonicalResolver)
        });
    }

    private static void ApplyEdCaretPath(
        CaretValueRule rule,
        ElementDefinition ed,
        ModelInspector? inspector,
        Func<string, string>? aliasResolver, 
        IResourceResolver canonicalResolver)
    {
        var path = rule.CaretPath.TrimStart('^');

        // Try compound-path navigation first (handles binding.description, slicing.discriminator.*, etc.).
        if (path.Contains('.') || path.Contains('['))
        {
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            if (FhirCaretValueWriter.TrySetCompound(ed, path, rule.Value, state, inspector, aliasResolver, canonicalResolver))
                return;
        }
        else if (FhirCaretValueWriter.TrySet(ed, path, rule.Value, inspector, aliasResolver, canonicalResolver))
        {
            return;
        }
        else if (inspector != null && rule.Value != null
              && FhirCaretValueWriter.TrySetChoiceTypeLeaf(ed, path, rule.Value, inspector, aliasResolver, canonicalResolver))
        {
            // Handles choice-type paths like minValueDate, maxValueDate, etc. on ElementDefinition.
            return;
        }

        // Fall back to an extension for paths not in the ElementDefinition model.
        ed.Extension ??= new List<FhirExtension>();
        ed.Extension.Add(new FhirExtension
        {
            Url = path,
            Value = FhirValueMapper.ToDataType(rule.Value, inspector, aliasResolver, canonicalResolver)
        });
    }

    private static void ApplyAddElementRule(AddElementRule addEl, StructureDefinition sd)
    {
        if (string.IsNullOrEmpty(addEl.Path)) return;
        var ed = GetOrCreateElement(addEl.Path, sd);
        var parts = addEl.Cardinality.Split("..");
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var min)) ed.Min = min;
            // FSH permits open-ended cardinality (e.g. "1..") which leaves the upper bound
            // implicit.  Sushi omits the max element in that case; an empty string would
            // otherwise serialise as ""max": """ which is invalid FHIR.
            if (!string.IsNullOrEmpty(parts[1]))
                ed.Max = parts[1];
        }
        ApplyFlags(ed, addEl.Flags);
        if (!string.IsNullOrEmpty(addEl.ShortDescription)) ed.Short = addEl.ShortDescription;
        if (!string.IsNullOrEmpty(addEl.Definition)) ed.Definition = addEl.Definition;
        else if (!string.IsNullOrEmpty(addEl.ShortDescription)) ed.Definition = addEl.ShortDescription;
        if (addEl.TargetTypes.Count > 0)
            ed.Type = addEl.TargetTypes
                .Select(tt => new ElementDefinition.TypeRefComponent { Code = tt })
                .ToList();
    }

    private static void ApplyAddCRElementRule(AddCRElementRule addCr, StructureDefinition sd)
    {
        if (string.IsNullOrEmpty(addCr.Path)) return;
        var ed = GetOrCreateElement(addCr.Path, sd);
        var parts = addCr.Cardinality.Split("..");
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var min)) ed.Min = min;
            // FSH permits open-ended cardinality (e.g. "1..") which leaves the upper bound
            // implicit.  Sushi omits the max element in that case; an empty string would
            // otherwise serialise as ""max": """ which is invalid FHIR.
            if (!string.IsNullOrEmpty(parts[1]))
                ed.Max = parts[1];
        }
        ApplyFlags(ed, addCr.Flags);
        if (!string.IsNullOrEmpty(addCr.ShortDescription)) ed.Short = addCr.ShortDescription;
        if (!string.IsNullOrEmpty(addCr.Definition)) ed.Definition = addCr.Definition;
        if (!string.IsNullOrEmpty(addCr.ContentReference))
            ed.ContentReference = addCr.ContentReference;
    }

    // ─── ValueSet rule processors ─────────────────────────────────────────────

    private static void ApplyVsComponentRule(
        VsComponentRule rule, FhirValueSet fvs, CompilerContext context, CompilerOptions opts)
    {
        // ─── Explicit concept codes ──────────────────────────────────────────────
        // Per the FSH spec, codes in a ValueSet component are written as System#code or
        // #code (with an explicit "from system …").  All codes that share the same system
        // are grouped into a single ConceptSetComponent in compose.include/exclude.
        if (rule.IsConceptComponent && rule.ConceptCode != null)
        {
            // Split a system-qualified code (e.g. "AustralianStateCodes#ACT") into its
            // system and code parts.
            var (splitSystem, codeOnly) = FhirValueMapper.SplitCodeValue(rule.ConceptCode.Value);

            // Prefer an explicit FromSystem; fall back to the system embedded in the code.
            var resolvedSystem = !string.IsNullOrEmpty(rule.FromSystem)
                ? ResolveSystemName(rule.FromSystem, context, opts)
                : (!string.IsNullOrEmpty(splitSystem) ? ResolveSystemName(splitSystem, context, opts) : null);

            // Find an existing include/exclude component for this system so that all codes
            // from the same system end up in one ConceptSetComponent (as sushi/FSH spec requires).
            var targetList = rule.IsInclude == false ? fvs.Compose!.Exclude : fvs.Compose!.Include;
            var existing = resolvedSystem != null
                ? targetList.FirstOrDefault(c => c.System == resolvedSystem && (c.Filter == null || c.Filter.Count == 0))
                : null;

            var concept = new FhirValueSet.ConceptReferenceComponent
            {
                Code = codeOnly,
                Display = rule.ConceptCode.Display
            };

            if (existing != null)
            {
                // Append to the existing component rather than creating a duplicate.
                existing.Concept ??= new List<FhirValueSet.ConceptReferenceComponent>();
                existing.Concept.Add(concept);
                return;
            }

            // No existing component for this system — create a new one.
            var component = new FhirValueSet.ConceptSetComponent
            {
                System = resolvedSystem,
                Concept = new List<FhirValueSet.ConceptReferenceComponent> { concept }
            };

            if (rule.FromValueSets.Count > 0)
                component.ValueSet = rule.FromValueSets.Select(vs => context.ResolveAlias(vs)).ToList();

            targetList.Add(component);
            return;
        }

        // ─── Filter component or system/valueset-only component ─────────────────
        {
            var component = new FhirValueSet.ConceptSetComponent();

            if (!string.IsNullOrEmpty(rule.FromSystem))
                component.System = ResolveSystemName(rule.FromSystem, context, opts);

            if (rule.FromValueSets.Count > 0)
                component.ValueSet = rule.FromValueSets.Select(vs => context.ResolveAlias(vs)).ToList();

            if (rule.Filters.Count > 0)
            {
                component.Filter = rule.Filters
                    .Select(f => new FhirValueSet.FilterComponent
                    {
                        Property = f.Property,
                        Op = MapFilterOp(f.Operator),
                        // FSH filter values may be bare codes (#410607006) or strings.
                        // Extract the code-only part for FshCode values.
                        Value = f.Value switch
                        {
                            StringValue sv  => sv.Value,
                            FshCode fshCode => FhirValueMapper.SplitCodeValue(fshCode.Value).Code,
                            _               => f.Operator
                        }
                    })
                    .ToList();
            }

            if (rule.IsInclude == false)
                fvs.Compose!.Exclude.Add(component);
            else
                fvs.Compose!.Include.Add(component);
        }
    }

    /// <summary>
    /// Resolves a system name or alias reference to a full URI.
    /// <list type="number">
    ///   <item>Exact alias lookup.</item>
    ///   <item>Pre-scanned CodeSystem entity map (<see cref="CompilerContext.CodeSystemUrls"/>).</item>
    ///   <item>
    ///     Resolver look-up using common FHIR base URL patterns with the name converted to
    ///     kebab-case (covers FHIR-core CodeSystems such as <c>TaskCode</c> →
    ///     <c>http://hl7.org/fhir/CodeSystem/task-code</c>).
    ///   </item>
    ///   <item>
    ///     Canonical-base fallback: <c>{canonical}/CodeSystem/{name}</c> for project-local
    ///     CodeSystem names whose URL last segment matches the entity name.
    ///   </item>
    /// </list>
    /// </summary>
    private static string ResolveSystemName(string name, CompilerContext context, CompilerOptions opts)
    {
        // Alias lookup (handles $-prefixed aliases and any bare alias names).
        var resolved = context.ResolveAlias(name);
        if (resolved != name || IsAbsoluteUrl(resolved)) return resolved;

        // Pre-scanned CodeSystem entity map (populated before compilation starts).
        if (context.CodeSystemUrls.TryGetValue(name, out var csUrl))
            return csUrl;

        // Resolver-based look-up for FHIR-core / terminology CodeSystems.
        if (opts.Resolver != null)
        {
            var cs = opts.Resolver.ResolveByCanonicalUri(name) as FhirCodeSystem;
            if (cs != null) return cs.Url;
        }

        // Check to see if the ID was in the Specification.zip
        if (context.CanonicalsFromSpecificationZip.ContainsKey("CodeSystem#" + name))
            return context.CanonicalsFromSpecificationZip["CodeSystem#" + name];

        // Do not synthesize canonical URLs for unresolved names.
        // Canonical replacement should only happen when an actual CodeSystem is detected
        // via alias resolution, parsed entity map, resolver lookup, or specification index.
        return name;
    }

    private static string ToLowerCamel(string value) =>
        string.IsNullOrEmpty(value) || char.IsLower(value[0])
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];

    private static FilterOperator MapFilterOp(string op) =>
        op switch
        {
            "=" => FilterOperator.Equal,
            "is-a" => FilterOperator.IsA,
            "descendent-of" => FilterOperator.DescendentOf,
            "is-not-a" => FilterOperator.IsNotA,
            "regex" => FilterOperator.Regex,
            "in" => FilterOperator.In,
            "not-in" => FilterOperator.NotIn,
            "generalizes" => FilterOperator.Generalizes,
            "exists" => FilterOperator.Exists,
            _ => FilterOperator.Equal
        };

    private static void ApplyVsCaretValueRule(VsCaretValueRule rule, FhirValueSet fvs, ModelInspector? inspector, Func<string, string>? aliasResolver, IResourceResolver canonicalResolver)
    {
        var path = rule.CaretPath.TrimStart('^');
        // Use SetCsCaretPath so that multi-level dot-separated paths (e.g. "contact.telecom.system")
        // are navigated correctly by creating/reusing intermediate child objects.  A single-segment
        // path is handled identically to the previous TrySet call.
        var activeInspector = inspector ?? ModelInspector.ForAssembly(typeof(StructureDefinition).Assembly);
        SetCsCaretPath(fvs, path, rule.Value, activeInspector, aliasResolver, canonicalResolver);
        // Silently ignore if the path is not in the ValueSet model.
    }

    /// <summary>
    /// Expands a <see cref="VsInsertRule"/> by resolving the referenced <see cref="RuleSet"/>
    /// from the context and replaying any applicable VS rules against the ValueSet.
    /// </summary>
    private static void ApplyVsInsertRule(
        VsInsertRule insertRule, FhirValueSet fvs, CompilerContext context, CompilerOptions opts)
    {
        var resolved = RuleSetResolver.Resolve(
            insertRule.RuleSetReference, insertRule.IsParameterized, insertRule.Parameters, context);

        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);
        foreach (var rule in resolved)
        {
            switch (rule)
            {
                case VsComponentRule compRule:
                    ApplyVsComponentRule(compRule, fvs, context, opts);
                    break;
                case VsCaretValueRule vsCaretRule:
                    ApplyVsCaretValueRule(vsCaretRule, fvs, opts.Inspector, context.ResolveAlias, canonicalResolver);
                    break;
                // CaretValueRule (SD-style, no path) can appear in a RuleSet re-parsed
                // via a synthetic Profile wrapper and applies to the VS root.
                case CaretValueRule sdCaret when string.IsNullOrEmpty(sdCaret.Path):
                    var vsPath = sdCaret.CaretPath.TrimStart('^');
                    FhirCaretValueWriter.TrySet(fvs, vsPath, sdCaret.Value, opts.Inspector, context.ResolveAlias, canonicalResolver);
                    break;
                case VsInsertRule nestedInsert:
                    ApplyVsInsertRule(nestedInsert, fvs, context, opts);
                    break;
            }
        }
    }

    /// <summary>
    /// Applies a <see cref="CodeCaretValueRule"/> to specific concept references within the
    /// <see cref="FhirValueSet"/>.  The rule targets one or more codes within the compose
    /// include/exclude components; the caret path is applied to each matching
    /// <see cref="FhirValueSet.ConceptReferenceComponent"/>.
    /// </summary>
    private static void ApplyCodeCaretValueRule(
        CodeCaretValueRule rule, FhirValueSet fvs, ModelInspector? inspector, Func<string, string>? aliasResolver, IResourceResolver canonicalResolver)
    {
        if (rule.Codes.Count == 0 || string.IsNullOrEmpty(rule.CaretPath)) return;
        var path = rule.CaretPath.TrimStart('^');

        foreach (var codeStr in rule.Codes)
        {
            var bare = codeStr.TrimStart('#');
            var concept = FindConceptReferenceByCode(fvs, bare);
            if (concept != null)
                FhirCaretValueWriter.TrySet(concept, path, rule.Value, inspector, aliasResolver, canonicalResolver);
        }
    }

    /// <summary>
    /// Finds the first <see cref="FhirValueSet.ConceptReferenceComponent"/> in a ValueSet's
    /// compose (include and exclude) whose Code matches <paramref name="code"/>.
    /// </summary>
    private static FhirValueSet.ConceptReferenceComponent? FindConceptReferenceByCode(
        FhirValueSet fvs, string code)
    {
        if (fvs.Compose is null) return null;

        foreach (var component in fvs.Compose.Include.Concat(fvs.Compose.Exclude))
        {
            var match = component.Concept?.FirstOrDefault(c => c.Code == code);
            if (match != null) return match;
        }
        return null;
    }

    /// <summary>
    /// Expands a <see cref="CodeInsertRule"/> by resolving the referenced <see cref="RuleSet"/>
    /// and replaying any applicable code-level caret rules for the listed codes.
    /// </summary>
    private static void ApplyCodeInsertRule(
        CodeInsertRule insertRule, FhirValueSet fvs, CompilerContext context, CompilerOptions opts)
    {
        var resolved = RuleSetResolver.Resolve(
            insertRule.RuleSetReference, insertRule.IsParameterized, insertRule.Parameters, context);

        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);
        foreach (var rule in resolved)
        {
            switch (rule)
            {
                case CodeCaretValueRule codeCaretRule:
                    // Merge the enclosing rule's codes with any codes in the nested rule.
                    var effectiveCodes = insertRule.Codes.Count > 0 ? insertRule.Codes : codeCaretRule.Codes;
                    var merged = new CodeCaretValueRule
                    {
                        Indent = codeCaretRule.Indent,
                        Codes = effectiveCodes,
                        CaretPath = codeCaretRule.CaretPath,
                        Value = codeCaretRule.Value
                    };
                    ApplyCodeCaretValueRule(merged, fvs, opts.Inspector, context.ResolveAlias, canonicalResolver);
                    break;
                case CodeInsertRule nestedInsert:
                    ApplyCodeInsertRule(nestedInsert, fvs, context, opts);
                    break;
            }
        }
    }

    // ─── CodeSystem rule processors ───────────────────────────────────────────

    private static void ApplyConceptRule(Concept concept, FhirCodeSystem fcs)
    {
        if (concept.Codes.Count == 0) return;

        // Build the concept hierarchy: first code is the parent, rest are hierarchical sub-codes
        var rootCode = CleanConceptCode(concept.Codes[0]);
        var conceptDef = new FhirCodeSystem.ConceptDefinitionComponent
        {
            Code = rootCode,
            Display = concept.Display,
            Definition = concept.Definition
        };

        if (concept.Codes.Count > 1)
        {
            // Nested code path: each subsequent code is a child of the previous
            AddNestedConcept(conceptDef, concept.Codes, 1);
        }

        fcs.Concept!.Add(conceptDef);
    }

    private static void AddNestedConcept(
        FhirCodeSystem.ConceptDefinitionComponent parent,
        IReadOnlyList<string> codes,
        int index)
    {
        if (index >= codes.Count) return;
        var child = new FhirCodeSystem.ConceptDefinitionComponent
        {
            Code = CleanConceptCode(codes[index])
        };
        parent.Concept ??= new List<FhirCodeSystem.ConceptDefinitionComponent>();
        parent.Concept.Add(child);
        AddNestedConcept(child, codes, index + 1);
    }

    private static void ApplyCsCaretValueRule(
        CsCaretValueRule rule,
        FhirCodeSystem fcs,
        Dictionary<string, int> csIndexState,
        Dictionary<string, Dictionary<string, int>> conceptIndexStates,
        ModelInspector inspector,
        Func<string, string>? aliasResolver, 
        IResourceResolver canonicalResolver)
    {
        var path = rule.CaretPath.TrimStart('^');

        if (rule.Codes.Count > 0)
        {
            // Per-concept caret: apply to the matching concept(s) rather than the CodeSystem itself
            foreach (var codeStr in rule.Codes)
            {
                var cleanCode = CleanConceptCode(codeStr);
                var concept = FindConceptByCode(fcs.Concept, cleanCode);
                if (concept is null) continue;

                if (!conceptIndexStates.TryGetValue(cleanCode, out var conceptState))
                    conceptIndexStates[cleanCode] = conceptState = new Dictionary<string, int>(StringComparer.Ordinal);

                var resolvedPath = ResolveSoftIndices(path, conceptState);
                SetCsCaretPath(concept, resolvedPath, rule.Value, inspector, aliasResolver, canonicalResolver);
            }
        }
        else
        {
            var resolvedPath = ResolveSoftIndices(path, csIndexState);
            SetCsCaretPath(fcs, resolvedPath, rule.Value, inspector, aliasResolver, canonicalResolver);
        }
    }

    /// <summary>
    /// Expands a <see cref="CsInsertRule"/> by resolving the referenced <see cref="RuleSet"/>
    /// and replaying any applicable CS rules against the CodeSystem.
    /// The caller's <paramref name="csIndexState"/> and <paramref name="conceptIndexStates"/> are
    /// shared so that soft-index counters (<c>[+]</c>/<c>[=]</c>) remain continuous across
    /// multiple insert rules at the same nesting level.
    /// </summary>
    private static void ApplyCsInsertRule(
        CsInsertRule insertRule,
        FhirCodeSystem fcs,
        CompilerContext context,
        CompilerOptions opts,
        Dictionary<string, int>? csIndexState = null,
        Dictionary<string, Dictionary<string, int>>? conceptIndexStates = null)
    {
        var resolved = RuleSetResolver.Resolve(
            insertRule.RuleSetReference, insertRule.IsParameterized, insertRule.Parameters, context);

        var inspector = opts.Inspector ?? ModelInspector.ForAssembly(typeof(StructureDefinition).Assembly);
        // Use the caller's index-state dictionaries when provided so that [+] increments
        // accumulate across multiple insert-rule invocations (e.g. two `insert propertyConcept`
        // calls on the same CodeSystem should produce consecutive property[0] and property[1],
        // not both overwrite property[0]).
        csIndexState ??= new Dictionary<string, int>(StringComparer.Ordinal);
        conceptIndexStates ??= new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        Func<string, string> aliasResolver = context.ResolveAlias;
        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);

        foreach (var rule in resolved)
        {
            switch (rule)
            {
                case Concept concept:
                    ApplyConceptRule(concept, fcs);
                    break;
                case CsCaretValueRule csCaretRule:
                    ApplyCsCaretValueRule(csCaretRule, fcs, csIndexState, conceptIndexStates, inspector, aliasResolver, canonicalResolver);
                    break;
                case CaretValueRule sdCaret when string.IsNullOrEmpty(sdCaret.Path):
                    var csPath = ResolveSoftIndices(sdCaret.CaretPath.TrimStart('^'), csIndexState);
                    SetCsCaretPath(fcs, csPath, sdCaret.Value, inspector, aliasResolver, canonicalResolver);
                    break;
                case CsInsertRule nestedInsert:
                    ApplyCsInsertRule(nestedInsert, fcs, context, opts, csIndexState, conceptIndexStates);
                    break;
            }
        }
    }

    private static FhirCodeSystem.ConceptDefinitionComponent? FindConceptByCode(
        IEnumerable<FhirCodeSystem.ConceptDefinitionComponent>? concepts, string code)
    {
        if (concepts == null) return null;
        foreach (var c in concepts)
        {
            if (c.Code == code) return c;
            var child = FindConceptByCode(c.Concept, code);
            if (child != null) return child;
        }
        return null;
    }

    /// <summary>
    /// Strips the FSH code prefix (<c>#</c>) and, for codes with spaces that are delimited
    /// by double-quotes in FSH (e.g. <c>#"More than half the days"</c>), also removes the
    /// surrounding double-quote characters.
    /// </summary>
    private static string CleanConceptCode(string rawCode)
    {
        var code = rawCode.TrimStart('#');
        if (code.StartsWith('"') && code.EndsWith('"') && code.Length >= 2)
            code = code[1..^1];
        return code;
    }

    // ─── Shared helpers ───────────────────────────────────────────────────────

    private static ElementDefinition? FindElement(string path, StructureDefinition sd)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var type = GetElementPathPrefix(sd);

        if (path == ".")
            return sd.Differential.Element.FirstOrDefault(e => e.Path == type && e.SliceName == null);

        path = NormalizeSliceBrackets(path);

        var segments = path.Split('.');
        var fhirPathSegments = new List<string>(segments.Length);

        foreach (var seg in segments)
        {
            var colon = seg.IndexOf(':');
            fhirPathSegments.Add(colon < 0 ? seg : seg[..colon]);
        }

        var fhirPath = string.IsNullOrEmpty(type)
            ? string.Join('.', fhirPathSegments)
            : $"{type}.{string.Join('.', fhirPathSegments)}";

        return sd.Differential.Element.FirstOrDefault(e => e.Path == fhirPath && e.SliceName == null);
    }

    private static ElementDefinition GetOrCreateElement(string path, StructureDefinition sd)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required", nameof(path));

        var type = GetElementPathPrefix(sd);

        // Path "." refers to the root element whose path equals the type itself.
        if (path == ".")
        {
            var rootEd = sd.Differential.Element.FirstOrDefault(e => e.Path == type && e.SliceName == null);
            if (rootEd == null)
            {
                rootEd = new ElementDefinition(type) { Path = type, ElementId = type };
                sd.Differential.Element.Insert(0, rootEd);
            }
            return rootEd;
        }

        // Normalize FSH `name[sliceName]` notation to `name:sliceName` so that slice
        // references (e.g. `extension[label].value[x]`) resolve to the slice defined
        // by a prior `contains` rule rather than being treated as literal brackets.
        path = NormalizeSliceBrackets(path);

        // Split into segments and process each segment for potential slice notation
        // AND inline choice-type variant detection.
        // For a segment like `valueCodeableConcept` where the current-depth FHIR type
        // defines `value[x]` with a `CodeableConcept` variant, we rewrite the segment
        // to the canonical sliced form (`value[x]:valueCodeableConcept`) and remember
        // to emit the slicing parent (`<parent>.value[x]` with type-discriminated slicing).
        var segments = path.Split('.');
        var fhirPathSegments = new List<string>(segments.Length);
        var elementIdSegments = new List<string>(segments.Length);
        string? trailingSliceName = null;
        bool hasSliceInPath = false;

        var choiceCtx = sd.Annotation<ChoiceSliceContext>();
        // Track the current walk position as a (containingSd, elementId, typeName) triple
        // so that inlined child elements on the core type SD (e.g. the inlined
        // `Questionnaire.item.answer.value[x]` under the BackboneElement-typed `item.answer`)
        // are detected before falling back to the declared type's own SD.
        TypeLocation? currentLoc = string.IsNullOrEmpty(sd.Type)
            ? null
            : new TypeLocation(choiceCtx?.CoreTypeSd, sd.Type, sd.Type);
        string? terminalVariantName = null;
        string? terminalVariantType = null;
        List<(string fullChoicePath, string typeName)>? slicingParentsToEmit = null;

        for (int segIdx = 0; segIdx < segments.Length; segIdx++)
        {
            var seg = segments[segIdx];
            bool isTerminal = segIdx == segments.Length - 1;
            var colon = seg.IndexOf(':');

            if (colon >= 0)
            {
                var baseName = seg[..colon];
                var sliceName = seg[(colon + 1)..];
                fhirPathSegments.Add(baseName);
                elementIdSegments.Add($"{baseName}:{sliceName}");
                hasSliceInPath = true;
                trailingSliceName = isTerminal ? sliceName : null;

                // Advance type across this segment using the field (base) name.
                currentLoc = AdvanceChoiceType(currentLoc, baseName, choiceCtx);
                continue;
            }

            // Try inline choice-type variant detection: does `seg` match `<base>X`
            // where the current type defines `<base>[x]` with `X` as one of its types?
            var variant = DetectChoiceVariant(currentLoc, seg, choiceCtx);
            if (variant != null)
            {
                var (choiceBase, typeName) = variant.Value;
                fhirPathSegments.Add(choiceBase);                   // e.g. "value[x]"
                elementIdSegments.Add($"{choiceBase}:{seg}");       // e.g. "value[x]:valueCodeableConcept"
                hasSliceInPath = true;

                // Compute this slicing parent's full path (relative to current SD root).
                var parentOnlyFhirSegments = fhirPathSegments.Take(fhirPathSegments.Count - 1).ToList();
                var parentPrefix = string.IsNullOrEmpty(type)
                    ? string.Join('.', parentOnlyFhirSegments)
                    : parentOnlyFhirSegments.Count == 0 ? type : type + "." + string.Join('.', parentOnlyFhirSegments);
                var fullChoicePath = string.IsNullOrEmpty(parentPrefix)
                    ? choiceBase
                    : parentPrefix + "." + choiceBase;

                (slicingParentsToEmit ??= new()).Add((fullChoicePath, typeName));

                if (isTerminal)
                {
                    trailingSliceName = seg;
                    terminalVariantName = seg;
                    terminalVariantType = typeName;
                }

                // Advance into the variant's concrete type.  The containing SD's inlined
                // snapshot rarely has children under `X[x]` variant slices, so switch to
                // the variant type's SD for subsequent walk steps.
                currentLoc = choiceCtx != null
                    ? new TypeLocation(ResolveTypeSd(typeName, choiceCtx), typeName, typeName)
                    : null;
            }
            else
            {
                fhirPathSegments.Add(seg);
                elementIdSegments.Add(seg);
                trailingSliceName = null;
                currentLoc = AdvanceChoiceType(currentLoc, seg, choiceCtx);
            }
        }

        var fhirPath = string.IsNullOrEmpty(type)
            ? string.Join('.', fhirPathSegments)
            : $"{type}.{string.Join('.', fhirPathSegments)}";
        var elementId = string.IsNullOrEmpty(type)
            ? string.Join('.', elementIdSegments)
            : $"{type}.{string.Join('.', elementIdSegments)}";

        // Emit each required slicing parent element (once per SD compile).  Done before
        // creating the slice itself so insertion ordering (driven by element rank) works
        // naturally — the parent (value[x]) has sliceRank 0, slices have sliceRank > 0.
        if (slicingParentsToEmit != null && choiceCtx != null)
        {
            foreach (var (fullChoicePath, variantType) in slicingParentsToEmit)
                EnsureChoiceSlicingParent(sd, choiceCtx, fullChoicePath);
        }

        if (hasSliceInPath)
        {
            // Find existing element by ElementId (unique per slice branch).
            var existing = sd.Differential.Element.FirstOrDefault(e => e.ElementId == elementId);
            if (existing != null) return existing;

            var newEd = new ElementDefinition(fhirPath)
            {
                Path = fhirPath,
                ElementId = elementId
            };
            // When the slice marker is on the *last* segment, this element IS the slice
            // itself and must carry SliceName (per FHIR); sub-elements below a slice
            // retain the slice marker only in their id, never in SliceName.
            if (elementIdSegments[^1].Contains(':'))
                newEd.SliceName = trailingSliceName;

            // For a terminal choice-type variant slice, stamp the type and cardinality
            // (named choice-type variants are always 0..1, but skip max when the base
            // already defines the slice — the cardinality is inherited).
            if (terminalVariantName != null && terminalVariantType != null && choiceCtx != null)
            {
                if (newEd.Type == null || newEd.Type.Count == 0)
                    newEd.Type = [new ElementDefinition.TypeRefComponent { Code = terminalVariantType }];

                var baseHasSlice = choiceCtx.ParentBaseSd?.Differential?.Element?.Any(e =>
                    e.Path == fhirPath
                    && string.Equals(e.SliceName, terminalVariantName, StringComparison.Ordinal)) ?? false;
                if (newEd.MaxElement == null && !baseHasSlice)
                    newEd.Max = "1";
            }

            InsertElementInOrder(sd, newEd);
            return newEd;
        }

        var ed = sd.Differential.Element.FirstOrDefault(e => e.Path == fhirPath && e.SliceName == null);
        if (ed == null)
        {
            // C-EI1: Generate ElementDefinition.Id equal to the full path for non-slice elements.
            ed = new ElementDefinition(fhirPath) { Path = fhirPath, ElementId = fhirPath };
            InsertElementInOrder(sd, ed);
        }
        return ed;
    }

    // ─── Insertion-time differential element ordering ───────────────────────

    /// <summary>
    /// Per-SD context needed to compute insertion-time rank tuples by walking the
    /// parent profile chain's differential lists.  Attached to each
    /// <see cref="StructureDefinition"/> as an annotation while the SD is being compiled.
    /// </summary>
    private sealed class DifferentialOrderContext
    {
        public required CompilerContext Context { get; init; }
        public required CompilerOptions Options { get; init; }

        /// <summary>
        /// Sticky rank allocations for locally-introduced fields and slices (names that
        /// do not appear in any base differential).  Keyed by a string uniquely
        /// describing the scope (<c>parentId|kind|name</c>) so recomputation returns the
        /// same value for the same logical element.  Values start at
        /// <see cref="LocalRankBase"/> and increment monotonically — this keeps local
        /// elements after all base-defined ones while still giving each sibling a
        /// distinct rank so their descendants sort contiguously.
        /// </summary>
        public Dictionary<string, int> LocalRanks { get; } = new(StringComparer.Ordinal);
        public int NextLocalRank = LocalRankBase;
    }

    /// <summary>
    /// Starting value for sticky local-rank allocations.  Must exceed any realistic
    /// base-defined rank (children per element rarely exceed a few dozen) while
    /// leaving ample headroom before <see cref="int.MaxValue"/>.
    /// </summary>
    private const int LocalRankBase = int.MaxValue / 2;

    /// <summary>
    /// Maximum base-chain walk depth used by the differential-ordering ranker.
    /// Bounded to guard against pathological or cyclic <c>BaseDefinition</c> links;
    /// real FHIR type hierarchies never approach this depth (typically ≤5).
    /// </summary>
    private const int MaxBaseChainDepth = 10;

    /// <summary>
    /// Cached rank tuple for an <see cref="ElementDefinition"/>.  Invalidated when the
    /// element's <see cref="ElementDefinition.ElementId"/> changes.
    /// </summary>
    private sealed class ElementRankAnnotation
    {
        public required string ElementId { get; init; }
        public required int[] Tuple { get; init; }
    }

    /// <summary>
    /// Attaches the ordering context to <paramref name="sd"/> so that subsequent
    /// <see cref="InsertElementInOrder"/> calls can resolve rank tuples by walking
    /// the parent profile chain's differentials.  Safe to call multiple times.
    /// </summary>
    private static void AttachOrderingContext(
        StructureDefinition sd, CompilerContext context, CompilerOptions opts)
    {
        if (sd.Annotation<DifferentialOrderContext>() != null) return;
        sd.AddAnnotation(new DifferentialOrderContext { Context = context, Options = opts });
    }

    /// <summary>
    /// Inserts <paramref name="newEd"/> into <c>sd.Differential.Element</c> at the
    /// position determined by walking the base profile chain's differentials.
    /// Ordering is computed per-segment of <see cref="ElementDefinition.ElementId"/>
    /// against the closest base that defines children of each parent path.
    /// When no ordering context is attached (e.g. early bootstrap) or when no base
    /// defines the element, falls back to appending (preserving FSH insertion order).
    /// The root element (differential index 0) is never displaced.
    /// </summary>
    private static void InsertElementInOrder(StructureDefinition sd, ElementDefinition newEd)
    {
        var elements = sd.Differential.Element;
        if (elements.Count == 0)
        {
            elements.Add(newEd);
            return;
        }

        var orderCtx = sd.Annotation<DifferentialOrderContext>();
        if (orderCtx == null)
        {
            elements.Add(newEd);
            return;
        }

        var newTuple = GetOrComputeTuple(newEd, sd, orderCtx);

        // Forward scan from index 1 (root is pinned at 0).  Insert before the first
        // element whose tuple is strictly greater than newTuple; ties → new goes after.
        int insertAt = elements.Count;
        for (int i = 1; i < elements.Count; i++)
        {
            var t = GetOrComputeTuple(elements[i], sd, orderCtx);
            if (CompareTuples(newTuple, t) < 0)
            {
                insertAt = i;
                break;
            }
        }
        elements.Insert(insertAt, newEd);
    }

    /// <summary>
    /// Returns the rank tuple for <paramref name="ed"/>, computing and caching it on
    /// the element's annotations when not yet cached or when the cached value is stale
    /// (<see cref="ElementDefinition.ElementId"/> changed since the last compute).
    /// </summary>
    private static int[] GetOrComputeTuple(
        ElementDefinition ed, StructureDefinition sd, DifferentialOrderContext orderCtx)
    {
        var currentId = ed.ElementId ?? ed.Path ?? string.Empty;
        var anno = ed.Annotation<ElementRankAnnotation>();
        if (anno != null && anno.ElementId == currentId) return anno.Tuple;
        if (anno != null) ed.RemoveAnnotations<ElementRankAnnotation>();

        var tuple = ComputeRankTuple(currentId, sd, orderCtx);
        ed.AddAnnotation(new ElementRankAnnotation { ElementId = currentId, Tuple = tuple });
        return tuple;
    }

    /// <summary>
    /// Computes the rank tuple for an element with <paramref name="elementId"/>.
    /// The tuple has two integers per id-segment after the SD root type:
    /// <c>(fieldRank, sliceRank)</c>.  <c>fieldRank</c> is the 0-based index of the
    /// child field name among the direct children of the parent path in the closest
    /// base differential that defines any children there, or a sticky monotonic
    /// local-rank allocation when locally-introduced.  <c>sliceRank</c> is <c>0</c>
    /// for the bare (un-sliced) variant, <c>1 + index</c> for a slice defined in the
    /// base, or a sticky local-rank for a local slice — this guarantees base slices
    /// come first in base order, then local slices in FSH insertion order, and ties
    /// in base-defined prefixes are broken by distinct sticky values so that
    /// descendants sort contiguously under their own parent.
    /// </summary>
    private static int[] ComputeRankTuple(
        string elementId, StructureDefinition sd, DifferentialOrderContext orderCtx)
    {
        if (string.IsNullOrEmpty(elementId)) return [];
        var segments = elementId.Split('.');
        if (segments.Length <= 1) return []; // root element

        var rootType = GetElementPathPrefix(sd);
        var depth = segments.Length - 1;
        var tuple = new int[depth * 2];

        for (int k = 1; k < segments.Length; k++)
        {
            var (childField, childSlice) = ParseIdSegment(segments[k]);

            // parentId expressed in the *current* SD's namespace — used as the
            // sticky-rank dictionary key so local names are stable per (parent, scope).
            var parentIdInSd = k == 1
                ? rootType
                : rootType + "." + string.Join('.', segments, 1, k - 1);

            var (fieldRank, sliceRank) = RankInBaseChain(
                segments, k, rootType, sd.BaseDefinition, childField, childSlice,
                parentIdInSd, orderCtx, depth: 0);
            tuple[(k - 1) * 2] = fieldRank;
            tuple[(k - 1) * 2 + 1] = sliceRank;
        }
        return tuple;
    }

    /// <summary>
    /// Splits an id segment into (fieldName, sliceName) — e.g.
    /// <c>"coding:primary"</c> → <c>("coding", "primary")</c>;
    /// <c>"value[x]"</c> → <c>("value[x]", null)</c>.
    /// </summary>
    private static (string fieldName, string? sliceName) ParseIdSegment(string seg)
    {
        var colon = seg.IndexOf(':');
        return colon < 0 ? (seg, null) : (seg[..colon], seg[(colon + 1)..]);
    }

    /// <summary>
    /// Walks the FULL base profile chain (depth-guarded), collecting direct children of
    /// the parent path at every level, then assigns canonical ranks by merging those
    /// children base-first (ultimate-ancestor first).  This correctly handles the FHIR
    /// convention that derived profile differentials are SPARSE — inherited elements
    /// (e.g. <c>id</c>/<c>meta</c> from <c>Resource</c>, <c>extension</c>/
    /// <c>modifierExtension</c> from <c>DomainResource</c>) do not re-appear in the
    /// derived differential, yet must still be ordered canonically before any type-
    /// specific children.
    /// </summary>
    /// <remarks>
    /// A field introduced by a more-ancestral base gets a lower rank than one
    /// introduced by a more-derived profile, which matches the canonical element
    /// ordering sushi / FHIR snapshot generation produce.  First-occurrence wins when
    /// the same field appears at multiple levels (a derived profile re-listing an
    /// inherited element to add constraints does not change its rank).
    /// </remarks>
    private static (int fieldRank, int sliceRank) RankInBaseChain(
        string[] segments,
        int segmentIndex,
        string currentRoot,
        string? baseDefinition,
        string childField,
        string? childSlice,
        string parentIdInSd,
        DifferentialOrderContext orderCtx,
        int depth)
    {
        // Collect the chain (immediate base first, ultimate base last).
        var chain = new List<(string baseRoot, IList<ElementDefinition> diff)>();
        var curDef = baseDefinition;
        var curRoot = currentRoot;
        int walkDepth = depth;
        while (!string.IsNullOrEmpty(curDef) && walkDepth <= MaxBaseChainDepth)
        {
            var baseSd = ResolveStructureDefinition(curDef, orderCtx.Context, orderCtx.Options);
            if (baseSd == null) break;

            var baseRoot = baseSd.Type ?? curRoot;
            var diff = (IList<ElementDefinition>?)baseSd.Differential?.Element ?? Array.Empty<ElementDefinition>();
            chain.Add((baseRoot, diff));

            curRoot = baseRoot;
            curDef = baseSd.BaseDefinition;
            walkDepth++;
        }

        if (chain.Count == 0)
        {
            return AllocateLocal(parentIdInSd, childField, childSlice, orderCtx, fieldRankFromBase: null);
        }

        var fieldRanks = new Dictionary<string, int>(StringComparer.Ordinal);
        var sliceRanks = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        // Every non-root parent has type-inherited universal children that are not
        // re-stated in any specific-type profile's differential.  Pre-merge those
        // first so they receive the lowest (earliest) ranks — matching canonical
        // FHIR element order.  The type depends on the parent's last segment:
        //   • <c>.extension[:X]</c> and <c>.modifierExtension[:X]</c> are themselves
        //     <c>Extension</c>-typed (recursive), so children come from Extension's
        //     chain (id, extension, url, value[x]).
        //   • Anything else is assumed BackboneElement-typed (covers the common
        //     Resource-backbone case like <c>Questionnaire.item</c>) yielding
        //     id, extension, modifierExtension from Element + BackboneElement.
        // For primitive-typed parents the extra <c>modifierExtension</c> is
        // harmless: nothing will reference it, so it never contributes to ordering.
        if (segmentIndex >= 2)
        {
            var lastParentSeg = segments[segmentIndex - 1];
            var colon = lastParentSeg.IndexOf(':');
            var lastFieldName = colon < 0 ? lastParentSeg : lastParentSeg[..colon];

            var inheritedTypeUrl = lastFieldName is "extension" or "modifierExtension"
                ? "http://hl7.org/fhir/StructureDefinition/Extension"
                : "http://hl7.org/fhir/StructureDefinition/BackboneElement";

            MergeTypeCanonicalChildren(inheritedTypeUrl, fieldRanks, sliceRanks, orderCtx);
        }

        // Merge direct-children lists base-first (iterate chain in reverse) so that
        // fields introduced by more-ancestral bases get smaller ranks — matching
        // canonical FHIR element order.  First-occurrence wins: a derived profile
        // re-listing an inherited element does not shift its rank.
        int nextFieldRank = fieldRanks.Count;

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var (baseRoot, diff) = chain[i];
            string rewrittenParent = segmentIndex == 1
                ? baseRoot
                : baseRoot + "." + string.Join('.', segments, 1, segmentIndex - 1);
            var parentPrefix = rewrittenParent + ".";

            foreach (var el in diff)
            {
                var eid = el.ElementId ?? el.Path ?? string.Empty;
                if (!eid.StartsWith(parentPrefix, StringComparison.Ordinal)) continue;
                var rest = eid[parentPrefix.Length..];
                if (rest.Length == 0 || rest.Contains('.')) continue;

                var (fn, sn) = ParseIdSegment(rest);
                if (!fieldRanks.ContainsKey(fn))
                    fieldRanks[fn] = nextFieldRank++;

                if (sn != null)
                {
                    if (!sliceRanks.TryGetValue(fn, out var sliceMap))
                        sliceRanks[fn] = sliceMap = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (!sliceMap.ContainsKey(sn))
                        sliceMap[sn] = sliceMap.Count;
                }
            }
        }

        int? baseFieldRank = fieldRanks.TryGetValue(childField, out var fr) ? fr : null;

        if (childSlice == null)
        {
            // Bare (un-sliced) element at this path.
            return baseFieldRank.HasValue
                ? (baseFieldRank.Value, 0)
                : AllocateLocal(parentIdInSd, childField, null, orderCtx, fieldRankFromBase: null);
        }

        // Named slice.
        if (baseFieldRank.HasValue
            && sliceRanks.TryGetValue(childField, out var m)
            && m.TryGetValue(childSlice, out var baseSliceIdx))
        {
            return (baseFieldRank.Value, 1 + baseSliceIdx);
        }

        return AllocateLocal(parentIdInSd, childField, childSlice, orderCtx, fieldRankFromBase: baseFieldRank);
    }

    /// <summary>
    /// Allocates (or reuses) a sticky monotonic rank for a locally-introduced field
    /// or slice that does not appear in any base profile's differential chain.
    /// Using a sticky value — rather than a naive <see cref="int.MaxValue"/> — ensures
    /// distinct rank entries for sibling locally-added elements so their descendants
    /// remain grouped contiguously under their own parent.
    /// </summary>
    private static (int fieldRank, int sliceRank) AllocateLocal(
        string parentIdInSd,
        string childField,
        string? childSlice,
        DifferentialOrderContext orderCtx,
        int? fieldRankFromBase)
    {
        int fieldRank;
        if (fieldRankFromBase.HasValue)
        {
            fieldRank = fieldRankFromBase.Value;
        }
        else
        {
            var fieldKey = parentIdInSd + "|field|" + childField;
            if (!orderCtx.LocalRanks.TryGetValue(fieldKey, out fieldRank))
            {
                fieldRank = orderCtx.NextLocalRank++;
                orderCtx.LocalRanks[fieldKey] = fieldRank;
            }
        }

        if (childSlice == null) return (fieldRank, 0);

        var sliceKey = parentIdInSd + "|slice|" + childField + ":" + childSlice;
        if (!orderCtx.LocalRanks.TryGetValue(sliceKey, out var sliceRank))
        {
            sliceRank = orderCtx.NextLocalRank++;
            orderCtx.LocalRanks[sliceKey] = sliceRank;
        }
        return (fieldRank, sliceRank);
    }

    /// <summary>
    /// Walks a type's base chain (e.g. <c>Extension → Element</c>) and merges the
    /// direct children of that type's root into <paramref name="fieldRanks"/> and
    /// <paramref name="sliceRanks"/>, base-first.  Used as a fallback for FHIR
    /// recursive-type references (<c>.extension</c>, <c>.modifierExtension</c>)
    /// whose child ordering is governed by the referenced type rather than by a
    /// path entry in any profile differential.
    /// </summary>
    private static void MergeTypeCanonicalChildren(
        string typeCanonicalUrl,
        Dictionary<string, int> fieldRanks,
        Dictionary<string, Dictionary<string, int>> sliceRanks,
        DifferentialOrderContext orderCtx)
    {
        var chain = new List<(string root, IList<ElementDefinition> diff)>();
        var curDef = typeCanonicalUrl;
        int d = 0;
        while (!string.IsNullOrEmpty(curDef) && d <= MaxBaseChainDepth)
        {
            var baseSd = ResolveStructureDefinition(curDef, orderCtx.Context, orderCtx.Options);
            if (baseSd == null) break;
            var root = baseSd.Type ?? string.Empty;
            var diff = (IList<ElementDefinition>?)baseSd.Differential?.Element ?? Array.Empty<ElementDefinition>();
            chain.Add((root, diff));
            curDef = baseSd.BaseDefinition;
            d++;
        }

        int nextRank = fieldRanks.Count;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var (root, diff) = chain[i];
            if (string.IsNullOrEmpty(root)) continue;
            var prefix = root + ".";
            foreach (var el in diff)
            {
                var eid = el.ElementId ?? el.Path ?? string.Empty;
                if (!eid.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var rest = eid[prefix.Length..];
                if (rest.Length == 0 || rest.Contains('.')) continue;

                var (fn, sn) = ParseIdSegment(rest);
                if (!fieldRanks.ContainsKey(fn))
                    fieldRanks[fn] = nextRank++;
                if (sn != null)
                {
                    if (!sliceRanks.TryGetValue(fn, out var m))
                        sliceRanks[fn] = m = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (!m.ContainsKey(sn))
                        m[sn] = m.Count;
                }
            }
        }
    }

    /// <summary>
    /// Compares two rank tuples lexicographically.  When tuples differ in length, the
    /// shorter one compares less at the first position past its end (so parent paths
    /// sort before their descendants, and siblings at the same depth are ordered
    /// purely by their own rank entries).
    /// </summary>
    private static int CompareTuples(int[] a, int[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
        }
        return a.Length.CompareTo(b.Length);
    }

    /// <summary>
    /// Returns the element-path prefix to use when building element id/path values for
    /// a given StructureDefinition.  For logical models the <c>Type</c> property carries
    /// the full canonical URL, but element paths must use the short id (e.g.
    /// <c>SdcQuestionLibrary</c> rather than
    /// <c>http://hl7.org/fhir/uv/sdc/StructureDefinition/SdcQuestionLibrary</c>).
    /// </summary>
    private static string GetElementPathPrefix(StructureDefinition sd)
    {
        if (sd.Kind == StructureDefinition.StructureDefinitionKind.Logical
            && !string.IsNullOrEmpty(sd.Id)
            && (sd.Type?.Contains('/') ?? false))
        {
            return sd.Id;
        }
        return sd.Type ?? string.Empty;
    }

    /// <summary>
    /// Converts FSH slice-bracket notation (<c>name[sliceName]</c>) to colon notation
    /// (<c>name:sliceName</c>) in each path segment.  Bracket contents that are numeric,
    /// the literal <c>x</c> (FHIR choice-type marker like <c>value[x]</c>), or the
    /// soft-index tokens <c>+</c> / <c>=</c> are preserved as-is.
    /// </summary>
    private static string NormalizeSliceBrackets(string path)
    {
        if (!path.Contains('[')) return path;

        var segments = path.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var open = seg.IndexOf('[');
            if (open < 0) continue;
            var close = seg.IndexOf(']', open);
            if (close < 0) continue;

            var content = seg[(open + 1)..close];
            // Skip FHIR choice-type markers (value[x]), numeric indices, and soft-index tokens.
            if (content == "x" || content == "+" || content == "=") continue;
            if (int.TryParse(content, out _)) continue;

            var baseName = seg[..open];
            var after = seg[(close + 1)..]; // typically empty, but be safe
            segments[i] = $"{baseName}:{content}{after}";
        }
        return string.Join('.', segments);
    }

    private static void ApplyFlags(ElementDefinition ed, IEnumerable<string> flags)
    {
        foreach (var f in flags)
        {
            switch (f)
            {
                case "MS": ed.MustSupport = true; break;
                case "SU": ed.IsSummary = true; break;
                case "?!": ed.IsModifier = true; break;
                case "N":
                case "TU":
                case "D":
                    // Trial-use / normative / draft flags — no direct ElementDefinition mapping
                    break;
            }
        }
    }


    /// <summary>
    /// Post-compilation pass that resolves in-IG ValueSet names used in element bindings to
    /// their canonical URLs.  Binding rules compiled during SD construction store the entity
    /// name when the ValueSet is defined in the same IG but had not yet been compiled.
    /// </summary>
    private static void FixUpValueSetBindings(
        List<StructureDefinition> allSds,
        List<FhirResource> allResources,
        CompilerContext context,
        CompilerOptions opts)
    {
        // Build a lookup of ValueSet name → canonical URL from compiled resources.
        var vsUrlByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in allResources.OfType<FhirValueSet>())
        {
            if (!string.IsNullOrEmpty(resource.Name) && !string.IsNullOrEmpty(resource.Url))
                vsUrlByName.TryAdd(resource.Name, resource.Url);
            if (!string.IsNullOrEmpty(resource.Id) && !string.IsNullOrEmpty(resource.Url))
                vsUrlByName.TryAdd(resource.Id, resource.Url);
        }

        foreach (var sd in allSds)
        {
            foreach (var element in sd.Differential?.Element ?? [])
            {
                var binding = element.Binding;
                if (binding is null || string.IsNullOrEmpty(binding.ValueSet)) continue;
                if (IsAbsoluteUrl(binding.ValueSet)) continue;

                // Try in-IG compiled ValueSets first.
                if (vsUrlByName.TryGetValue(binding.ValueSet, out var vsUrl))
                {
                    binding.ValueSet = vsUrl;
                    continue;
                }

                // Try specification-zip ValueSets (keyed as "ValueSet#name").
                var specKey = $"ValueSet#{binding.ValueSet}";
                if (context.CanonicalsFromSpecificationZip.TryGetValue(specKey, out var specVsUrl))
                    binding.ValueSet = specVsUrl;
            }
        }
    }

    /// <summary>
    /// Removes differential elements that contain no constraints beyond identity/path and only
    /// exist as scaffolding parents for child element changes.
    /// </summary>
    private static void RemoveNoOpScaffoldElements(StructureDefinition sd)
    {
        var elements = sd.Differential?.Element;
        if (elements == null || elements.Count == 0) return;

        var rootType = GetElementPathPrefix(sd);

        var toRemove = elements
            .Where(ed => IsNoOpScaffoldElement(ed, elements, rootType))
            .ToList();

        foreach (var ed in toRemove)
            elements.Remove(ed);

        // Ensure the root element (if present) is always at position 0.  In multi-document
        // compilation the root can be displaced when profiles-of-profiles are fixed up after
        // compilation; the FHIR snapshot generator requires root to precede its children.
        if (!string.IsNullOrEmpty(rootType) && elements.Count > 1)
        {
            var rootIdx = elements.FindIndex(e =>
                string.Equals(e.Path, rootType, StringComparison.Ordinal)
                && string.IsNullOrEmpty(e.SliceName)
                && string.Equals(e.ElementId, rootType, StringComparison.Ordinal));
            if (rootIdx > 0)
            {
                var rootEd = elements[rootIdx];
                elements.RemoveAt(rootIdx);
                elements.Insert(0, rootEd);
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="ed"/> is an empty leaf element: it has no
    /// meaningful content (no constraints, flags, types, etc.) and no child elements in the
    /// differential.  Such elements contribute nothing to the differential and are pruned.
    /// This catches cases like <c>* version MS</c> when the parent profile already declares
    /// <c>* version 0..1 MS</c> — after redundancy removal the element is completely empty.
    /// </summary>
    private static bool IsEmptyLeafElement(ElementDefinition ed, List<ElementDefinition> allElements)
    {
        if (ed.Path == null || ed.ElementId == null) return false;
        // Only non-slice, non-root elements (path contains a dot).
        if (!string.IsNullOrEmpty(ed.SliceName)) return false;
        if (!ed.Path.Contains('.')) return false;
        // Must have path == elementId (not a sub-element of a slice).
        if (!string.Equals(ed.Path, ed.ElementId, StringComparison.Ordinal)) return false;

        // If it has children, don't remove it (the scaffold check handles that case).
        var hasChildren = allElements.Any(child =>
            !ReferenceEquals(child, ed)
            && child.Path != null
            && child.Path.StartsWith(ed.Path + ".", StringComparison.Ordinal));
        if (hasChildren) return false;

        // Check for meaningful content (same set as IsNoOpScaffoldElement).
        var hasMeaningfulContent =
            !string.IsNullOrEmpty(ed.Short)
            || !string.IsNullOrEmpty(ed.Definition)
            || !string.IsNullOrEmpty(ed.Comment)
            || !string.IsNullOrEmpty(ed.Requirements)
            || ed.Slicing != null
            || ed.Binding != null
            || ed.Type?.Count > 0
            || ed.Constraint?.Count > 0
            || ed.Mapping?.Count > 0
            || ed.Extension?.Count > 0
            || ed.MinElement != null
            || ed.MaxElement != null
            || ed.MustSupportElement != null
            || ed.IsModifierElement != null
            || ed.IsSummaryElement != null
            || ed.Fixed != null
            || ed.Pattern != null
            || ed.DefaultValue != null
            || ed.Example?.Count > 0;

        return !hasMeaningfulContent;
    }

    private static bool IsNoOpScaffoldElement(
        ElementDefinition ed,
        List<ElementDefinition> allElements,
        string rootType)
    {
        if (ed.Path == null || ed.ElementId == null) return false;
        if (!string.IsNullOrEmpty(ed.SliceName)) return false;
        if (!string.Equals(ed.Path, ed.ElementId, StringComparison.Ordinal)) return false;

        var hasChildren = allElements.Any(child =>
            !ReferenceEquals(child, ed)
            && child.Path != null
            && child.Path.StartsWith(ed.Path + ".", StringComparison.Ordinal));
        if (!hasChildren) return false;

        var hasMeaningfulContent =
            !string.IsNullOrEmpty(ed.Short)
            || !string.IsNullOrEmpty(ed.Definition)
            || !string.IsNullOrEmpty(ed.Comment)
            || !string.IsNullOrEmpty(ed.Requirements)
            || ed.Slicing != null
            || ed.Binding != null
            || ed.Type?.Count > 0
            || ed.Constraint?.Count > 0
            || ed.Mapping?.Count > 0
            || ed.Extension?.Count > 0
            || ed.MinElement != null
            || ed.MaxElement != null
            || ed.MustSupportElement != null
            || ed.IsModifierElement != null
            || ed.IsSummaryElement != null
            || ed.Fixed != null
            || ed.Pattern != null
            || ed.DefaultValue != null
            || ed.Example?.Count > 0;

        return !hasMeaningfulContent;
    }

    // ─── Inline choice-type slice detection ─────────────────────────────────

    /// <summary>
    /// Per-SD context supporting inline detection of named choice-type path variants
    /// (e.g. <c>valueCodeableConcept</c> → slice of <c>value[x]</c>) during rule-path
    /// resolution in <see cref="GetOrCreateElement"/>.  Attached as an SD annotation
    /// alongside <see cref="DifferentialOrderContext"/>.
    /// </summary>
    private sealed class ChoiceSliceContext
    {
        /// <summary>
        /// Resolver used to look up core-type StructureDefinitions for variant detection
        /// at arbitrary sub-path depth (e.g. <c>Questionnaire.item.answerOption.valueCoding</c>
        /// requires the <c>Questionnaire</c> SD to navigate through <c>item</c>/<c>answerOption</c>,
        /// then the backbone's type SD to resolve <c>value[x]</c>).
        /// </summary>
        public IResourceResolver? Resolver { get; init; }

        /// <summary>
        /// Immediate parent profile's SD (when the SD being compiled derives from another
        /// profile).  Consulted to avoid re-emitting slicing parents or redundant
        /// <c>max="1"</c> cardinality the parent already defines.
        /// </summary>
        public StructureDefinition? ParentBaseSd { get; init; }

        /// <summary>
        /// The SD for the SD's root FHIR type (e.g. <c>UsageContext</c>, <c>Questionnaire</c>).
        /// Used as the starting type for variant-detection lookups at path-segment 0.
        /// </summary>
        public StructureDefinition? CoreTypeSd { get; init; }

        /// <summary>
        /// Full choice-element paths (e.g. <c>UsageContext.value[x]</c>) whose slicing
        /// parent has already been emitted during this SD compile — guards against
        /// duplicate slicing parents when multiple rules target the same variant.
        /// </summary>
        public HashSet<string> HandledSlicingParents { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Cache of pre-computed variant lookups.  Keys use a two-space format to
        /// distinguish the two sources of <c>X[x]</c> children:
        /// <list type="bullet">
        ///   <item><c>"id:{elementId}"</c> — variants gathered by scanning forward from
        ///     <c>{elementId}</c> in a containing SD's element list (picks up inlined
        ///     BackboneElement children, e.g. <c>Questionnaire.item.answer.value[x]</c>).</item>
        ///   <item><c>"type:{typeName}"</c> — variants gathered from the root of the
        ///     named type's SD (fallback used when the containing SD has no inlined
        ///     children at the current position, e.g. walking through a named complex
        ///     type like <c>Quantity</c>).</item>
        /// </list>
        /// Value is the variant lookup: variant segment (e.g. <c>valueCoding</c>)
        /// → (<c>choiceBase</c> = "value[x]", <c>typeName</c> = "Coding").  Empty maps
        /// are cached for locations with no <c>X[x]</c> properties.
        /// </summary>
        public Dictionary<string, Dictionary<string, (string ChoiceBase, string TypeName)>> VariantMapCache { get; }
            = new(StringComparer.Ordinal);

        /// <summary>
        /// Cache of type-name → SD lookups to avoid repeated resolver calls during a
        /// single SD compile.
        /// </summary>
        public Dictionary<string, StructureDefinition?> TypeSdCache { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Attaches a <see cref="ChoiceSliceContext"/> to <paramref name="sd"/> so that
    /// <see cref="GetOrCreateElement"/> can inline-detect choice-type path variants.
    /// Safe to call multiple times; only the first call takes effect.
    /// </summary>
    private static void AttachChoiceSliceContext(
        StructureDefinition sd,
        StructureDefinition? parentBaseSd,
        StructureDefinition? coreTypeSd,
        IResourceResolver? resolver)
    {
        if (sd.Annotation<ChoiceSliceContext>() != null) return;
        sd.AddAnnotation(new ChoiceSliceContext
        {
            Resolver = resolver,
            ParentBaseSd = parentBaseSd,
            CoreTypeSd = coreTypeSd
        });
    }

    /// <summary>
    /// Looks up a <see cref="StructureDefinition"/> for a bare FHIR type name using the
    /// ChoiceSliceContext's resolver, caching per SD compile.  Returns <c>null</c> when
    /// the type cannot be resolved (context-less SDs or unknown types).
    /// </summary>
    private static StructureDefinition? ResolveTypeSd(string typeName, ChoiceSliceContext ctx)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        if (ctx.TypeSdCache.TryGetValue(typeName, out var cached)) return cached;

        // Prime the cache with CoreTypeSd on first miss so subsequent lookups go straight
        // through the dictionary fast path.
        if (ctx.CoreTypeSd != null
            && string.Equals(ctx.CoreTypeSd.Type, typeName, StringComparison.Ordinal))
        {
            ctx.TypeSdCache[typeName] = ctx.CoreTypeSd;
            return ctx.CoreTypeSd;
        }

        var sd = FindStructureDefinitionForType(typeName, ctx.Resolver);
        ctx.TypeSdCache[typeName] = sd;
        return sd;
    }

    /// <summary>
    /// Represents a navigation position during FSH path-walk type resolution.  Carries:
    /// <list type="bullet">
    ///   <item><see cref="ContainingSd"/> — the SD whose element list is currently being
    ///     scanned for inlined children (typically the core type SD for the root of the
    ///     SD being compiled, or a data-type SD once the walk descends into a named
    ///     complex type).  May be <c>null</c> if unresolvable.</item>
    ///   <item><see cref="ElementId"/> — the <c>ElementDefinition.ElementId</c> of the
    ///     current position inside <see cref="ContainingSd"/>.  Used to locate inlined
    ///     children by prefix match on subsequent elements' ElementIds.</item>
    ///   <item><see cref="TypeName"/> — the declared FHIR type code at the current
    ///     position; used as a fallback when the containing SD has no inlined children
    ///     at this location (e.g. traversing into a named complex type whose children
    ///     are not inlined into the outer snapshot).</item>
    /// </list>
    /// </summary>
    private readonly record struct TypeLocation(
        StructureDefinition? ContainingSd,
        string ElementId,
        string TypeName);

    /// <summary>
    /// Detects whether <paramref name="segment"/> is a named choice-type variant of some
    /// <c>&lt;base&gt;[x]</c> property at <paramref name="location"/>.  Variants are looked
    /// up first against inlined children of the current element (elements whose
    /// <c>ElementId</c> starts with <paramref name="location"/>.<see cref="TypeLocation.ElementId"/>
    /// + <c>'.'</c>) and only fall back to the declared type's SD when no inlined
    /// children exist.  Returns <c>(choiceBase, typeName)</c> on match, else <c>null</c>.
    /// </summary>
    private static (string ChoiceBase, string TypeName)? DetectChoiceVariant(
        TypeLocation? location, string segment, ChoiceSliceContext? ctx)
    {
        if (ctx == null || location is null || string.IsNullOrEmpty(segment))
            return null;

        var map = GetOrBuildVariantMap(location.Value, ctx);
        if (map.Count == 0) return null;
        return map.TryGetValue(segment, out var hit) ? hit : null;
    }

    /// <summary>
    /// Returns (building and caching on first access) the variant-name lookup for the
    /// children of <paramref name="location"/>.  Prefers inlined children in
    /// <see cref="TypeLocation.ContainingSd"/> (gathered by scanning forward from the
    /// current <see cref="TypeLocation.ElementId"/> until an element's id no longer has
    /// that prefix) — this is what picks up backbone-element children like
    /// <c>Questionnaire.item.answer.value[x]</c> that live on the containing SD, not on
    /// the <c>BackboneElement</c> type SD.  Falls back to the declared
    /// <see cref="TypeLocation.TypeName"/>'s own root elements when no inlined children
    /// exist.  Maps each <c>&lt;base&gt;&lt;TypeCode&gt;</c> variant name (e.g.
    /// <c>valueCoding</c>) to its <c>(&lt;base&gt;[x], TypeCode)</c> pair.  Only immediate
    /// (one-segment-deep) <c>X[x]</c> children are considered; each declared type on the
    /// <c>X[x]</c> element contributes one entry.
    /// </summary>
    private static Dictionary<string, (string ChoiceBase, string TypeName)> GetOrBuildVariantMap(
        TypeLocation location, ChoiceSliceContext ctx)
    {
        // 1) Preferred source: inlined children of the current element in the containing SD.
        if (location.ContainingSd != null && !string.IsNullOrEmpty(location.ElementId))
        {
            var inlineKey = "id:" + location.ElementId;
            if (ctx.VariantMapCache.TryGetValue(inlineKey, out var cachedInline)) return cachedInline;

            var inlineMap = BuildVariantMapFromInlineChildren(location.ContainingSd, location.ElementId);
            if (inlineMap.Count > 0)
            {
                ctx.VariantMapCache[inlineKey] = inlineMap;
                return inlineMap;
            }
            // Cache the empty result under the inline key so we don't re-scan, but still
            // consult the type-SD fallback below for this call.
            ctx.VariantMapCache[inlineKey] = inlineMap;
        }

        // 2) Fallback: root elements of the declared type's SD.
        if (string.IsNullOrEmpty(location.TypeName))
            return new Dictionary<string, (string ChoiceBase, string TypeName)>(StringComparer.Ordinal);

        var typeKey = "type:" + location.TypeName;
        if (ctx.VariantMapCache.TryGetValue(typeKey, out var cachedType)) return cachedType;

        var typeSd = ResolveTypeSd(location.TypeName, ctx);
        var typeMap = typeSd == null
            ? new Dictionary<string, (string ChoiceBase, string TypeName)>(StringComparer.Ordinal)
            : BuildVariantMapFromInlineChildren(typeSd, location.TypeName);
        ctx.VariantMapCache[typeKey] = typeMap;
        return typeMap;
    }

    /// <summary>
    /// Builds a variant map by scanning <paramref name="sd"/>'s element list (snapshot
    /// preferred, differential as fallback) forward from the element with ElementId
    /// <paramref name="parentElementId"/> and collecting its immediate <c>X[x]</c>
    /// children.  The scan short-circuits as soon as an element is found whose ElementId
    /// no longer starts with <c>parentElementId + "."</c>, making it efficient even for
    /// very large snapshots.
    /// </summary>
    private static Dictionary<string, (string ChoiceBase, string TypeName)>
        BuildVariantMapFromInlineChildren(StructureDefinition sd, string parentElementId)
    {
        var map = new Dictionary<string, (string ChoiceBase, string TypeName)>(StringComparer.Ordinal);
        var elements = (IEnumerable<ElementDefinition>?)sd.Snapshot?.Element
                    ?? sd.Differential?.Element;
        if (elements == null) return map;

        // Resolve the effective prefix to match against.  Prefer an exact ElementId
        // anchor (parentElementId) when present; fall back to the SD's root element id
        // + '.' for the type-SD case where the caller passed a bare type name that
        // doesn't match the root's full ElementId.
        string? prefix = null;
        string? fallbackPrefix = null;
        string? rootId = null;
        foreach (var el in elements)
        {
            rootId = el.ElementId;
            break;
        }
        if (!string.IsNullOrEmpty(rootId) && rootId != parentElementId)
            fallbackPrefix = rootId + ".";
        var primaryPrefix = parentElementId + ".";

        bool seenParent = false;
        bool stopped = false;
        foreach (var el in elements)
        {
            var elId = el.ElementId;
            if (string.IsNullOrEmpty(elId)) continue;

            // Phase 1: find parentElementId exactly and scan its direct-[x] descendants.
            if (!seenParent)
            {
                if (elId == parentElementId)
                {
                    seenParent = true;
                    prefix = primaryPrefix;
                }
                continue;
            }

            // Stop at the first element outside the parent's subtree.
            if (!elId.StartsWith(prefix!, StringComparison.Ordinal)) { stopped = true; break; }

            AddVariantFromRelativeId(elId[prefix!.Length..], el, map);
        }

        // Phase 2 fallback: never found an exact ElementId match (e.g. scanning a type
        // SD whose root id differs from parentElementId).  Scan using the root's id as
        // the implicit parent prefix.  Preserves prior type-SD behavior.
        if (!seenParent && !stopped && fallbackPrefix != null)
        {
            foreach (var el in elements)
            {
                var elId = el.ElementId;
                if (string.IsNullOrEmpty(elId) || !elId.StartsWith(fallbackPrefix, StringComparison.Ordinal)) continue;
                AddVariantFromRelativeId(elId[fallbackPrefix.Length..], el, map);
            }
        }

        return map;

        static void AddVariantFromRelativeId(
            string rel, ElementDefinition el,
            Dictionary<string, (string ChoiceBase, string TypeName)> into)
        {
            // Only immediate-child [x] properties (no further '.' in the relative id).
            if (rel.Contains('.')) return;
            // Strip any slice-name suffix (e.g. "value[x]:valueCoding") before shape-matching.
            var colonIdx = rel.IndexOf(':');
            if (colonIdx >= 0) rel = rel[..colonIdx];
            if (!rel.EndsWith("[x]", StringComparison.Ordinal)) return;

            var choiceBase = rel;                   // e.g. "value[x]"
            var baseName = rel[..^3];               // strip "[x]" → "value"

            if (el.Type == null) return;
            foreach (var typeRef in el.Type)
            {
                var typeName = typeRef.Code;
                if (string.IsNullOrEmpty(typeName)) continue;
                // FHIR convention: variant name = baseName + Capitalize(typeName).
                var variantName = baseName + char.ToUpperInvariant(typeName[0]) + typeName[1..];
                into.TryAdd(variantName, (choiceBase, typeName));
            }
        }
    }

    /// <summary>
    /// Advances <paramref name="location"/> across a non-variant path segment.  Looks
    /// first for an inlined child of the current element in the containing SD (the child
    /// whose ElementId is <c>location.ElementId + "." + fieldName</c>, or with a
    /// <c>"[x]"</c> suffix).  Falls back to resolving the declared type's SD and looking
    /// up <c>typeName + "." + fieldName</c> when no inlined child exists.  Returns
    /// <c>null</c> when the field cannot be resolved — callers tolerate this by skipping
    /// further variant detection at deeper segments.
    /// </summary>
    private static TypeLocation? AdvanceChoiceType(
        TypeLocation? location, string fieldName, ChoiceSliceContext? ctx)
    {
        if (ctx == null || location is null || string.IsNullOrEmpty(fieldName)) return null;
        var loc = location.Value;

        // 1) Preferred: find an inlined child in the containing SD.
        if (loc.ContainingSd != null && !string.IsNullOrEmpty(loc.ElementId))
        {
            var hit = FindInlineChild(loc.ContainingSd, loc.ElementId, fieldName);
            if (hit != null)
            {
                var (childId, childType) = hit.Value;
                return new TypeLocation(loc.ContainingSd, childId, childType ?? string.Empty);
            }
        }

        // 2) Fallback: resolve the declared type's SD and look up the child there.
        if (string.IsNullOrEmpty(loc.TypeName)) return null;
        var typeSd = ResolveTypeSd(loc.TypeName, ctx);
        if (typeSd == null) return null;

        var rootId = loc.TypeName;
        var fallbackHit = FindInlineChild(typeSd, rootId, fieldName);
        if (fallbackHit == null) return null;
        var (fallbackChildId, fallbackChildType) = fallbackHit.Value;
        return new TypeLocation(typeSd, fallbackChildId, fallbackChildType ?? string.Empty);
    }

    /// <summary>
    /// Scans <paramref name="sd"/>'s element list (snapshot preferred, differential as
    /// fallback) for a direct child of the element identified by
    /// <paramref name="parentElementId"/> whose relative id is either <paramref name="fieldName"/>
    /// or <paramref name="fieldName"/> + <c>"[x]"</c>.  Returns the matching element's
    /// ElementId and first declared type code, or <c>null</c> if no such child exists.
    /// The scan short-circuits as soon as an element is encountered whose id does not
    /// start with <c>parentElementId + "."</c>.
    /// </summary>
    private static (string ChildElementId, string? ChildTypeCode)? FindInlineChild(
        StructureDefinition sd, string parentElementId, string fieldName)
    {
        var elements = (IEnumerable<ElementDefinition>?)sd.Snapshot?.Element
                    ?? sd.Differential?.Element;
        if (elements == null) return null;

        var prefix = parentElementId + ".";
        var targetPlain = fieldName;
        var targetChoice = fieldName + "[x]";
        var targetPlainPath = parentElementId + "." + fieldName;
        var targetChoicePath = parentElementId + "." + fieldName + "[x]";

        bool seenParent = false;
        ElementDefinition? pathFallbackMatch = null;
        foreach (var el in elements)
        {
            var elId = el.ElementId;

            // Opportunistic path-based fallback for the case where parentElementId
            // is never seen as an exact ElementId (e.g. walking a type SD whose root
            // uses a bare type name as its id).  Captured during the same iteration
            // so we don't re-enumerate if the primary phase fails.
            if (!seenParent
                && pathFallbackMatch == null
                && (el.Path == targetPlainPath || el.Path == targetChoicePath))
            {
                pathFallbackMatch = el;
            }

            if (string.IsNullOrEmpty(elId)) continue;

            if (!seenParent)
            {
                if (elId == parentElementId) seenParent = true;
                continue;
            }

            if (!elId.StartsWith(prefix, StringComparison.Ordinal)) break;

            var rel = elId[prefix.Length..];
            // Strip any slice-name suffix before matching.
            var colonIdx = rel.IndexOf(':');
            if (colonIdx >= 0) rel = rel[..colonIdx];
            if (rel.Contains('.')) continue;

            if (rel == targetPlain || rel == targetChoice)
                return (elId, el.Type?.FirstOrDefault()?.Code);
        }

        // Primary phase didn't find the parent's subtree — use the path-based match
        // we captured above (preserves prior behavior when walking type SDs).
        if (!seenParent && pathFallbackMatch != null)
            return (pathFallbackMatch.ElementId ?? pathFallbackMatch.Path, pathFallbackMatch.Type?.FirstOrDefault()?.Code);

        return null;
    }

    /// <summary>
    /// Ensures <paramref name="fullChoicePath"/> has a slicing-parent element in
    /// <paramref name="sd"/>'s differential (type-discriminated, open, unordered).
    /// Skips insertion when the parent profile's differential already defines slicing
    /// on the same path (slicing is inherited).  Idempotent per SD compile via
    /// <see cref="ChoiceSliceContext.HandledSlicingParents"/>.
    /// </summary>
    private static void EnsureChoiceSlicingParent(
        StructureDefinition sd, ChoiceSliceContext ctx, string fullChoicePath)
    {
        if (!ctx.HandledSlicingParents.Add(fullChoicePath)) return;

        var baseAlreadyHasSlicing = ctx.ParentBaseSd?.Differential?.Element?.Any(e =>
            e.Path == fullChoicePath
            && string.IsNullOrEmpty(e.SliceName)
            && e.Slicing != null) ?? false;
        if (baseAlreadyHasSlicing) return;

        var parentEl = sd.Differential.Element.FirstOrDefault(e =>
            e.Path == fullChoicePath && string.IsNullOrEmpty(e.SliceName));
        if (parentEl == null)
        {
            parentEl = new ElementDefinition { Path = fullChoicePath, ElementId = fullChoicePath };
            InsertElementInOrder(sd, parentEl);
        }

        parentEl.Slicing ??= new ElementDefinition.SlicingComponent
        {
            Discriminator =
            [
                new ElementDefinition.DiscriminatorComponent
                {
                    Type = ElementDefinition.DiscriminatorType.Type,
                    Path = "$this"
                }
            ],
            Ordered = false,
            Rules   = ElementDefinition.SlicingRules.Open
        };
    }

    /// <summary>
    /// When a named choice-type slice element carries a pattern/fixed value that is a
    /// sub-type of the slice's declared type (e.g. a <c>Coding</c> pattern on a
    /// <c>valueCodeableConcept</c> slice whose declared type is <c>CodeableConcept</c>),
    /// wraps the value in an instance of the declared type so that the serialized FHIR
    /// JSON uses the correct <c>pattern[x]</c> / <c>fixed[x]</c> variant.
    /// Currently handles the common case of <c>Coding</c> → <c>CodeableConcept { coding: [&lt;value&gt;] }</c>.
    /// </summary>
    private static void WrapChoiceSlicePattern(ElementDefinition el, string typeName)
    {
        el.Pattern = WrapChoiceValue(el.Pattern, typeName);
        el.Fixed   = WrapChoiceValue(el.Fixed,   typeName);
    }

    /// <summary>
    /// Helper that rewrites a single <see cref="DataType"/> value to the correct container
    /// type when assigning a sub-type (e.g. <c>Coding</c>) onto a choice-type slice whose
    /// declared type is a super-type (e.g. <c>CodeableConcept</c>).  Returns the value
    /// unchanged when no wrapping is required.
    /// </summary>
    private static DataType? WrapChoiceValue(DataType? value, string typeName)
    {
        if (value == null) return null;
        if (string.Equals(value.TypeName, typeName, StringComparison.Ordinal)) return value;

        // Coding inside CodeableConcept: wrap in a CodeableConcept with .coding = [pattern].
        if (string.Equals(typeName, "CodeableConcept", StringComparison.Ordinal)
            && value is Hl7.Fhir.Model.Coding coding)
        {
            return new Hl7.Fhir.Model.CodeableConcept
            {
                Coding =
                [
                    new Hl7.Fhir.Model.Coding
                    {
                        System  = coding.System,
                        Code    = coding.Code,
                        Display = coding.Display,
                    }
                ]
            };
        }
        return value;
    }


    /// <summary>
    /// Removes cardinality values from differential elements when they are identical to the
    /// inherited cardinality on the corresponding base profile element.
    /// </summary>
    private static void RemoveRedundantCardinalityAgainstBase(
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        var elements = sd.Differential?.Element;
        if (elements == null || elements.Count == 0) return;
        if (string.IsNullOrEmpty(sd.BaseDefinition)) return;

        var toRemove = new List<ElementDefinition>();
        foreach (var ed in elements)
        {
            if (!string.IsNullOrEmpty(ed.SliceName)) continue;
            if (ed.MinElement == null && ed.MaxElement == null && ed.MustSupportElement == null) continue;

            var baseEd = ResolveBaseElement(sd, ed, context, opts);
            if (baseEd == null) continue;

            if (ed.MinElement != null)
            {
                var effectiveMin = baseEd.Min;
                if (effectiveMin == null)
                    effectiveMin = ResolveEffectiveMin(sd, ed, context, opts);
                if (effectiveMin != null && ed.Min == effectiveMin)
                    ed.MinElement = null;
            }

            // For max: compare against the effective inherited max.
            // When the nearest matching base element has no max set (null), the effective max is
            // inherited from a higher ancestor.  We walk further up the chain to find it so that
            // we don't incorrectly emit e.g. `max="1"` when the core FHIR type already has it.
            if (ed.MaxElement != null)
            {
                var effectiveMax = baseEd.Max;
                if (effectiveMax == null)
                    effectiveMax = ResolveEffectiveMax(sd, ed, context, opts);
                if (string.Equals(ed.Max, effectiveMax, StringComparison.Ordinal))
                    ed.MaxElement = null;
            }

            // Remove redundant mustSupport when it matches the base element.
            if (ed.MustSupportElement != null && ed.MustSupport == baseEd.MustSupport)
                ed.MustSupportElement = null;

            // If all three fields are now null (they were the only content), this element
            // contributes nothing to the differential.  Schedule it for removal when it is
            // also a leaf node with no children and no other meaningful content.
            if (ed.MinElement == null && ed.MaxElement == null && ed.MustSupportElement == null
                && IsEmptyLeafElement(ed, elements))
            {
                toRemove.Add(ed);
            }
        }

        foreach (var ed in toRemove)
            elements.Remove(ed);
    }

    /// <summary>
    /// Removes type constraints from differential elements when they are identical to the
    /// inherited type on the corresponding base profile element.  This prevents redundant
    /// <c>only X</c> constraints from appearing in the differential when the parent already
    /// declares the same type.
    /// </summary>
    private static void RemoveRedundantTypeConstraints(
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        var elements = sd.Differential?.Element;
        if (elements == null || elements.Count == 0) return;
        if (string.IsNullOrEmpty(sd.BaseDefinition)) return;

        foreach (var ed in elements)
        {
            if (ed.Type == null || ed.Type.Count == 0) continue;

            List<ElementDefinition.TypeRefComponent>? baseType = null;

            if (!string.IsNullOrEmpty(ed.SliceName))
            {
                // For a named slice, look up the same-named slice in the base SD so that
                // `* extension[foo] only SomeExtensionProfile` is suppressed when the
                // parent profile already defines the slice with the same type constraint.
                baseType = ResolveBaseSliceType(sd, ed, context, opts);
            }
            else
            {
                var baseEd = ResolveBaseElement(sd, ed, context, opts);
                if (baseEd?.Type != null && baseEd.Type.Count > 0)
                    baseType = baseEd.Type;
            }

            if (baseType != null && AreTypeListsEquivalent(ed.Type, baseType))
                ed.Type = null;
        }
    }

    /// <summary>
    /// Resolves the type list for a named slice in the nearest base SD that defines
    /// an element with the same path and slice name.
    /// </summary>
    private static List<ElementDefinition.TypeRefComponent>? ResolveBaseSliceType(
        StructureDefinition sd,
        ElementDefinition ed,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(sd.BaseDefinition) || string.IsNullOrEmpty(ed.SliceName)) return null;

        var currentBase = sd.BaseDefinition;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(currentBase) && visited.Add(currentBase))
        {
            var baseSd = ResolveStructureDefinition(currentBase, context, opts);
            if (baseSd == null) return null;

            var basePath = RewritePathRoot(ed.Path, sd.Type, baseSd.Type);
            var match = (baseSd.Snapshot?.Element ?? baseSd.Differential?.Element)
                ?.FirstOrDefault(e =>
                    e.Path == basePath
                    && string.Equals(e.SliceName, ed.SliceName, StringComparison.Ordinal));

            if (match?.Type != null && match.Type.Count > 0)
                return match.Type;

            currentBase = baseSd.BaseDefinition;
        }

        return null;
    }

    /// <summary>
    /// Removes slicing from differential elements when the base profile (or any ancestor)
    /// already defines slicing on the same path.  Sushi does not re-emit slicing in a
    /// child profile that inherits extension slicing from its parent.
    /// </summary>
    private static void RemoveRedundantSlicingAgainstBase(
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        var elements = sd.Differential?.Element;
        if (elements == null || elements.Count == 0) return;
        if (string.IsNullOrEmpty(sd.BaseDefinition)) return;

        foreach (var ed in elements)
        {
            if (ed.Slicing == null) continue;
            // Don't remove slicing that was introduced by this profile itself (it won't
            // appear in any base element).  Only remove when the base already has slicing.
            if (!string.IsNullOrEmpty(ed.SliceName)) continue;

            var baseSlicing = ResolveBaseSlicing(sd, ed, context, opts);
            if (baseSlicing != null)
                ed.Slicing = null;
        }
    }

    /// <summary>
    /// Looks up the slicing component for <paramref name="ed"/> in the DIRECT parent SD.
    /// Returns a non-null value only when the direct parent's differential already defines
    /// at least one named slice on the same path — meaning the slicing mechanism is already
    /// established by the parent, so the child profile does not need to re-emit it.
    /// When the parent has no named slices on this path (e.g., adding the first extension
    /// slices to a profile of a core resource), returns <c>null</c> so the child keeps its slicing.
    /// </summary>
    private static ElementDefinition.SlicingComponent? ResolveBaseSlicing(
        StructureDefinition sd,
        ElementDefinition ed,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(sd.BaseDefinition) || string.IsNullOrEmpty(ed.Path)) return null;

        // Only check the DIRECT parent — do not walk the full ancestor chain.
        var baseSd = ResolveStructureDefinition(sd.BaseDefinition, context, opts);
        if (baseSd == null) return null;

        var basePath = RewritePathRoot(ed.Path, sd.Type, baseSd.Type);

        // Check whether the direct parent already has at least one named slice on this path
        // in its differential.  When it does, the slicing discriminator is already established
        // by the parent and does not need to be re-emitted by the child.
        var parentDiff = baseSd.Differential?.Element;
        if (parentDiff == null) return null;

        var parentHasNamedSlice = parentDiff.Any(e =>
            e.Path == basePath
            && !string.IsNullOrEmpty(e.SliceName));

        if (!parentHasNamedSlice) return null;

        // Parent has named slices → slicing is inherited.  Return the bare slicing element
        // (or a placeholder) so the caller knows to remove the duplicate slicing.
        var slicingEl = parentDiff.FirstOrDefault(e =>
            e.Path == basePath
            && string.IsNullOrEmpty(e.SliceName)
            && e.Slicing != null);

        // Even if the parent has no bare slicing element itself (it inherited slicing from
        // a grandparent and only has named slices), the fact that it has named slices means
        // the slicing is established.  Return a synthetic placeholder.
        return slicingEl?.Slicing
            ?? new ElementDefinition.SlicingComponent
            {
                Rules = ElementDefinition.SlicingRules.Open
            };
    }

    /// <summary>
    /// Returns <c>true</c> when the direct parent SD already has at least one named slice
    /// on the extension path, OR when the SD is itself an Extension type (for which the
    /// slicing on <c>Extension.extension</c> is always inherited from core and never re-emitted).
    /// Used in <c>ApplyContainsRule</c> to decide whether to create a bare extension element.
    /// </summary>
    private static bool DirectParentHasNamedExtensionSlices(
        string fshExtensionPath,
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(sd.BaseDefinition)) return false;

        // For Extension SDs (type="Extension"), the Extension.extension slicing is always
        // inherited from the core Extension definition — never re-emit the slicing element.
        if (string.Equals(sd.Type, "Extension", StringComparison.Ordinal)) return true;

        var sdPathPrefix = GetElementPathPrefix(sd);
        var fullFhirPath = string.IsNullOrEmpty(fshExtensionPath)
            ? sdPathPrefix
            : sdPathPrefix + "." + fshExtensionPath;

        // When the extension path is on a nested data type element (e.g. item.code.extension
        // where item.code has type Coding), the slicing discriminator is already defined by
        // the data type itself — there's no need to emit a bare slicing element.
        // Detect this by checking whether the parent element (the path before ".extension")
        // is a known FHIR data type in the resolved ancestor chain.
        var parentOfExtension = fullFhirPath.EndsWith(".extension", StringComparison.Ordinal)
            ? fullFhirPath[..^".extension".Length]
            : fullFhirPath.EndsWith(".modifierExtension", StringComparison.Ordinal)
            ? fullFhirPath[..^".modifierExtension".Length]
            : null;
        if (parentOfExtension != null && IsDataTypeElement(parentOfExtension, sd, context, opts))
            return true;

        // Walk the full ancestor chain. If ANY ancestor has named extension slices on this
        // path, the slicing is inherited and we don't need to re-emit the bare element.
        var currentBase = sd.BaseDefinition;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(currentBase) && visited.Add(currentBase))
        {
            var baseSd = ResolveStructureDefinition(currentBase, context, opts);
            if (baseSd == null) break;

            if (string.Equals(baseSd.Type, "Extension", StringComparison.Ordinal))
                return true;

            var basePath = RewritePathRoot(fullFhirPath, sd.Type ?? sdPathPrefix, baseSd.Type ?? GetElementPathPrefix(baseSd));
            var parentDiff = baseSd.Differential?.Element;
            if (parentDiff?.Any(e =>
                e.Path == basePath
                && !string.IsNullOrEmpty(e.SliceName)) == true)
                return true;

            currentBase = baseSd.BaseDefinition;
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the FHIR element at <paramref name="fhirPath"/> (e.g.
    /// <c>Questionnaire.item.code</c>) has a FHIR complex data type (not BackboneElement or
    /// Resource) in the resolved ancestor chain.  Used to avoid emitting redundant bare
    /// extension slicing elements for paths like <c>item.code.extension</c> where the data
    /// type's own extension slicing is already defined by the FHIR spec.
    /// </summary>
    private static bool IsDataTypeElement(string fhirPath, StructureDefinition sd, CompilerContext context, CompilerOptions opts)
    {
        // Only relevant for paths with more than one segment (resource-level is never data type).
        if (!fhirPath.Contains('.')) return false;

        var currentBase = sd.BaseDefinition;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var sdType = sd.Type ?? GetElementPathPrefix(sd);

        while (!string.IsNullOrEmpty(currentBase) && visited.Add(currentBase))
        {
            var baseSd = ResolveStructureDefinition(currentBase, context, opts);
            if (baseSd == null) break;

            var baseType = baseSd.Type ?? GetElementPathPrefix(baseSd);
            var rewritten = RewritePathRoot(fhirPath, sdType, baseType);

            var source = (IEnumerable<ElementDefinition>?)baseSd.Snapshot?.Element
                      ?? baseSd.Differential?.Element;
            var match = source?.FirstOrDefault(e =>
                string.Equals(e.Path, rewritten, StringComparison.Ordinal)
                && string.IsNullOrEmpty(e.SliceName));

            if (match?.Type?.Count > 0)
            {
                var typeName = match.Type[0].Code;
                // Only COMPLEX data types (Coding, Identifier, CodeableConcept, etc.) are
                // considered "data type elements" whose extension slicing is always inherited.
                // Primitives (string, boolean, decimal, code, etc.) require an explicit bare
                // extension element in the differential.
                // Exclude: structural types (BackboneElement, Resource, DomainResource) and
                // all FHIR primitive types that start with a lowercase letter (FHIR naming
                // convention: all primitive type codes begin lowercase).
                return !string.IsNullOrEmpty(typeName)
                    && !string.Equals(typeName, "BackboneElement", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(typeName, "Resource", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(typeName, "DomainResource", StringComparison.OrdinalIgnoreCase)
                    && char.IsUpper(typeName[0]);  // primitives start lowercase; complex types start uppercase
            }

            currentBase = baseSd.BaseDefinition;
        }

        return false;
    }

    private static bool AreTypeListsEquivalent(
        List<ElementDefinition.TypeRefComponent> a,
        List<ElementDefinition.TypeRefComponent> b)
    {
        if (a.Count != b.Count) return false;

        // Build a stable key for each type ref for comparison.
        static string TypeRefKey(ElementDefinition.TypeRefComponent t) =>
            $"{t.Code}|{string.Join(",", (t.Profile ?? []).Order())}|{string.Join(",", (t.TargetProfile ?? []).Order())}";

        // Sort by code to compare sets irrespective of order.
        var aSorted = a.OrderBy(x => x.Code, StringComparer.Ordinal).ToList();
        var bSorted = b.OrderBy(x => x.Code, StringComparer.Ordinal).ToList();

        for (int i = 0; i < aSorted.Count; i++)
        {
            var ai = aSorted[i];
            var bi = bSorted[i];

            if (!string.Equals(ai.Code, bi.Code, StringComparison.Ordinal)) return false;

            // When the compiled type has no profile/targetProfile constraints, treat it as
            // equivalent to the base type when the base's profiles are all abstract "any"
            // sentinels (e.g. Reference(Resource)).  This handles the common case where
            // FSH writes `only Reference` (no target) and the base has Reference(Resource),
            // which FHIR uses to indicate "Reference to any resource" — semantically identical.
            var aHasProfiles = (ai.Profile?.Any() ?? false) || (ai.TargetProfile?.Any() ?? false);
            if (!aHasProfiles)
            {
                // Accept the base as equivalent when it also has no profiles, or when its
                // targetProfiles consist exclusively of the abstract FHIR Resource type.
                var bProfiles = (bi.Profile ?? []).Concat(bi.TargetProfile ?? []).ToList();
                var bIsUnconstrained = bProfiles.Count == 0
                    || bProfiles.All(u => string.Equals(u,
                        "http://hl7.org/fhir/StructureDefinition/Resource",
                        StringComparison.Ordinal));
                if (!bIsUnconstrained) return false;
            }
            else
            {
                if (TypeRefKey(ai) != TypeRefKey(bi)) return false;
            }
        }
        return true;
    }

    private static ElementDefinition? ResolveBaseElement(
        StructureDefinition sd,
        ElementDefinition ed,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(sd.BaseDefinition) || string.IsNullOrEmpty(ed.Path)) return null;

        var currentBase = sd.BaseDefinition;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(currentBase) && visited.Add(currentBase))
        {
            var baseSd = ResolveStructureDefinition(currentBase, context, opts);
            if (baseSd == null) return null;

            var basePath = RewritePathRoot(ed.Path, sd.Type, baseSd.Type);
            var match = (baseSd.Snapshot?.Element ?? baseSd.Differential?.Element)
                ?.FirstOrDefault(e => e.Path == basePath && string.IsNullOrEmpty(e.SliceName));
            if (match != null) return match;

            currentBase = baseSd.BaseDefinition;
        }

        return null;
    }

    /// <summary>
    /// Resolves the effective <c>max</c> value for an element by walking the ancestor chain.
    /// This is used when the nearest parent element exists (for other reasons like constraints)
    /// but has no explicit <c>max</c> set — in that case the effective max is inherited from a
    /// higher ancestor (e.g. the core FHIR type's snapshot).
    /// </summary>
    private static string? ResolveEffectiveMax(
        StructureDefinition sd,
        ElementDefinition ed,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(sd.BaseDefinition) || string.IsNullOrEmpty(ed.Path)) return null;

        var currentBase = sd.BaseDefinition;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(currentBase) && visited.Add(currentBase))
        {
            var baseSd = ResolveStructureDefinition(currentBase, context, opts);
            if (baseSd == null) return null;

            var basePath = RewritePathRoot(ed.Path, sd.Type, baseSd.Type);
            var source = (IEnumerable<ElementDefinition>?)baseSd.Snapshot?.Element
                      ?? baseSd.Differential?.Element;
            var match = source?.FirstOrDefault(e =>
                e.Path == basePath && string.IsNullOrEmpty(e.SliceName) && e.MaxElement != null);
            if (match != null) return match.Max;

            currentBase = baseSd.BaseDefinition;
        }

        return null;
    }

    /// <summary>
    /// Walks the ancestor SD chain to find the effective inherited <c>min</c> for the element,
    /// consulting snapshot then differential when no min is set on the direct parent element.
    /// </summary>
    private static int? ResolveEffectiveMin(
        StructureDefinition sd,
        ElementDefinition ed,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(sd.BaseDefinition) || string.IsNullOrEmpty(ed.Path)) return null;

        var currentBase = sd.BaseDefinition;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(currentBase) && visited.Add(currentBase))
        {
            var baseSd = ResolveStructureDefinition(currentBase, context, opts);
            if (baseSd == null) return null;

            var basePath = RewritePathRoot(ed.Path, sd.Type, baseSd.Type);
            var source = (IEnumerable<ElementDefinition>?)baseSd.Snapshot?.Element
                      ?? baseSd.Differential?.Element;
            var match = source?.FirstOrDefault(e =>
                e.Path == basePath && string.IsNullOrEmpty(e.SliceName) && e.MinElement != null);
            if (match != null) return match.Min;

            currentBase = baseSd.BaseDefinition;
        }

        return null;
    }

    private static StructureDefinition? ResolveStructureDefinition(
        string canonical,
        CompilerContext context,
        CompilerOptions opts)
    {
        if (context.CompiledStructureDefinitions.TryGetValue(canonical, out var compiled))
            return compiled;

        return opts.Resolver?.FindStructureDefinition(canonical);
    }

    private static string RewritePathRoot(string path, string? fromRoot, string? toRoot)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(fromRoot) || string.IsNullOrEmpty(toRoot))
            return path;

        if (path == fromRoot) return toRoot;
        if (path.StartsWith(fromRoot + ".", StringComparison.Ordinal))
            return toRoot + path[fromRoot.Length..];
        return path;
    }

    /// <summary>
    /// Walks the profile chain to find the underlying FHIR base type.
    /// Returns the FHIR type name (e.g. <c>"Questionnaire"</c>) or <c>null</c> when unresolvable.
    /// </summary>
    private static string? ResolveUnderlyingFhirType(
        string typeName,
        CompilerContext context,
        CompilerOptions opts,
        Dictionary<string, StructureDefinition> byUrl,
        int depth)
    {
        if (depth > 10) return null; // Prevent infinite loops.

        if (IsKnownFhirType(typeName, opts.Inspector, opts.Resolver)) return typeName;

        // Look up in compiled SDs by name/id.
        StructureDefinition? parentSd;
        if (!context.CompiledStructureDefinitions.TryGetValue(typeName, out parentSd))
        {
            // Try by URL in compiled SDs.
            if (IsAbsoluteUrl(typeName))
                byUrl.TryGetValue(typeName, out parentSd);
        }

        if (parentSd != null)
        {
            if (!string.IsNullOrEmpty(parentSd.Type))
            {
                if (IsKnownFhirType(parentSd.Type, opts.Inspector, opts.Resolver)) return parentSd.Type;

                var resolvedFromType = ResolveUnderlyingFhirType(parentSd.Type, context, opts, byUrl, depth + 1);
                if (!string.IsNullOrEmpty(resolvedFromType)) return resolvedFromType;
            }

            if (!string.IsNullOrEmpty(parentSd.BaseDefinition))
                return ResolveUnderlyingFhirType(parentSd.BaseDefinition, context, opts, byUrl, depth + 1);

            return null;
        }

        // Fall back to the resolver for profiles from external sources (e.g. specification.zip).
        if (opts.Resolver != null)
        {
            var lookupName = IsAbsoluteUrl(typeName) ? typeName
                : context.CanonicalsFromSpecificationZip.TryGetValue($"StructureDefinition#{typeName}", out var specUrl)
                    ? specUrl : typeName;

            // Preferred: resolver-based walk - no inspector required.
            var resolvedType = context.ResolveBaseTypeFromResolver(lookupName, opts.Resolver);
            if (resolvedType != null) return resolvedType;

            // Fallback: inspector-based walk when inspector is also available.
            if (opts.Inspector != null)
            {
                var imr = BuildMergedResolver(context, opts.Resolver);
                var classMap = context.ResolveClassMappingForProfile(lookupName, opts.Inspector, imr, out _);
                if (classMap != null) return classMap.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="typeName"/> is a base FHIR type known to the
    /// resolver (preferred) or the model inspector (fallback).
    /// Checks e.g. <c>"Questionnaire"</c>, <c>"Extension"</c>, <c>"string"</c>.
    /// </summary>
    private static bool IsKnownFhirType(string typeName, ModelInspector? inspector, IResourceResolver? resolver = null)
    {
        // Only treat a resolver-found SD as a "known FHIR type" when it is a Specialization
        // (base type definition), not a Constraint (profile).  Profiles like cqllibrary must
        // be further resolved to their underlying FHIR resource type (e.g. Library).
        if (resolver != null)
        {
            var sd = FindStructureDefinitionForType(typeName, resolver);
            if (sd?.Derivation == StructureDefinition.TypeDerivationRule.Specialization)
                return true;
        }
        if (inspector == null) return false;
        return inspector.FindClassMapping(typeName) != null;
    }

    /// <summary>
    /// Looks up a FHIR core StructureDefinition for a bare type name (e.g. <c>"Patient"</c>)
    /// using the standard <c>http://hl7.org/fhir/StructureDefinition/{typeName}</c> canonical.
    /// Returns <c>null</c> when the resolver is unavailable or the type is not found.
    /// </summary>
    private static StructureDefinition? FindStructureDefinitionForType(string typeName, IResourceResolver? resolver)
    {
        if (resolver is null || string.IsNullOrEmpty(typeName)) return null;
        // Avoid double-lookup when already a URL.
        if (IsAbsoluteUrl(typeName))
            return resolver.FindStructureDefinition(typeName);
        // Try bare name via ResolveByUri — picks up compiled SDs registered by entity name (e.g. "SDCUsageContext").
        if (resolver.ResolveByUri(typeName) is StructureDefinition byName)
            return byName;
        return resolver.FindStructureDefinition("http://hl7.org/fhir/StructureDefinition/" + typeName);
    }

    /// <summary>
    /// Builds a merged <see cref="IResourceResolver"/> that consults compiled StructureDefinitions
    /// first (via <see cref="AliasResolver"/>) and then the external resolver when provided.
    /// </summary>
    private static IResourceResolver BuildMergedResolver(
        CompilerContext context, IResourceResolver? externalResolver)
    {
        var compiled = new AliasResolver(context.CompiledStructureDefinitions);
        return externalResolver == null ? compiled : new MultiResolver(compiled, externalResolver);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="url"/> is an absolute URI.
    /// Supports non-HTTP schemes such as <c>urn:</c> used by code-system identifiers.
    /// </summary>
    private static bool IsAbsoluteUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && !string.IsNullOrEmpty(uri.Scheme);

    private static string? NormalizeLineEndings(string? value) =>
        value?.Replace("\r\n", "\n").Replace("\r", "\n");

    /// <summary>
    /// Resolves a base-definition value to a canonical URL.
    /// When <paramref name="resolvedName"/> is already an absolute URL it is returned unchanged.
    /// Otherwise:
    /// 1. The name is looked up in the specification-zip canonicals index as
    ///    <c>"StructureDefinition#{name}"</c>.
    /// 2. The resolver is used to look up the canonical URL directly from the StructureDefinition
    ///    (preferred, no version-specific inspector required).
    /// 3. If the model inspector recognises it as a FHIR core type, the canonical is constructed
    ///    as <c>http://hl7.org/fhir/StructureDefinition/{name}</c>.
    /// 4. If a compiled StructureDefinition with this name/id has been registered, its URL is used.
    /// 5. Falls back to constructing a URL using the IG canonical base for non-base-FHIR names.
    /// </summary>
    private static string ResolveBaseDefinitionCanonical(
        string resolvedName, string fallbackTypeName, CompilerContext context, CompilerOptions? opts = null)
    {
        if (IsAbsoluteUrl(resolvedName)) return resolvedName;

        // Check in the specification-zip index (keyed as "StructureDefinition#name").
        var specKey = $"StructureDefinition#{resolvedName}";
        if (context.CanonicalsFromSpecificationZip.TryGetValue(specKey, out var specCanonical))
            return specCanonical;

        var inspector = opts?.Inspector;
        var resolver = opts?.Resolver;

        // Preferred: use the resolver to find the canonical URL from the StructureDefinition itself.
        if (resolver != null)
        {
            var sd = FindStructureDefinitionForType(resolvedName, resolver);
            if (sd?.Url != null) return sd.Url;
        }

        // Fall back to inspector for known FHIR core types.
        if (inspector != null && inspector.FindClassMapping(resolvedName) != null)
        {
            var coreCanonical = inspector.CanonicalUriForFhirCoreType(resolvedName);
            if (!string.IsNullOrEmpty(coreCanonical)) return coreCanonical;
            // Fall back to the well-known FHIR base URL pattern.
            return $"http://hl7.org/fhir/StructureDefinition/{resolvedName}";
        }

        // Check if a compiled StructureDefinition with this name/id has already been registered.
        if (context.CompiledStructureDefinitions.TryGetValue(resolvedName, out var compiledSd)
            && !string.IsNullOrEmpty(compiledSd.Url))
            return compiledSd.Url;

        // Construct a canonical URL using the IG canonical base when available,
        // but only for non-base-FHIR names (base types are handled above).
        if (opts != null && !string.IsNullOrEmpty(opts.CanonicalBase))
            return $"{opts.CanonicalBase.TrimEnd('/')}/StructureDefinition/{resolvedName}";

        // Last resort: return the name as-is (will be an invalid non-canonical but preserves data).
        return resolvedName;
    }

    /// <summary>
    /// Constructs a canonical URL for a resource given its local id/name and the
    /// resource-type-specific path segment (e.g. "StructureDefinition", "ValueSet",
    /// "CodeSystem"). When no <see cref="CompilerOptions.CanonicalBase"/> is set the
    /// id is returned as-is so that downstream tools can still work with relative ids.
    /// </summary>
    private static string? ResolveUrl(string? idOrName, CompilerOptions opts, string resourceType = "StructureDefinition")
    {
        if (string.IsNullOrEmpty(idOrName)) return null;
        if (IsAbsoluteUrl(idOrName)) return idOrName;
        if (string.IsNullOrEmpty(opts.CanonicalBase)) return idOrName;
        return $"{opts.CanonicalBase.TrimEnd('/')}/{resourceType}/{idOrName}";
    }

    // ─── Instance builder ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts a FSH <see cref="Hl7.FhirShorthand.Serialization.Models.Instance"/> entity to a FHIR resource.
    /// Requires a version-specific <see cref="CompilerOptions.Inspector"/> to resolve the
    /// <c>InstanceOf</c> type name to a CLR type; returns <c>null</c> when the type cannot
    /// be resolved or the inspector is not supplied.
    /// </summary>
    /// <param name="forContained">
    /// When <c>true</c>, the <c>#inline</c> usage guard is skipped so that the instance can be
    /// embedded as a contained resource rather than emitted as a standalone output.
    /// </param>
    public static FhirResource? BuildInstance(
        Hl7.FhirShorthand.Serialization.Models.Instance instance, CompilerContext context, CompilerOptions? options = null,
        bool forContained = false)
    {
        var opts = options ?? new CompilerOptions();
        if (string.IsNullOrEmpty(instance.InstanceOf)) return null;

        var inspector = opts.Inspector;
        if (inspector is null) return null;  // instance compilation requires a version-specific inspector

        // C-IN4: #inline instances should NOT be emitted as standalone resources.
        // When compiling for a contained[] slot the inline guard is intentionally bypassed.
        var usage = instance.Usage?.TrimStart('#').ToLowerInvariant();
        if (!forContained && usage == "inline") return null;

        // Resolve alias → type name, then strip any URL prefix to get the bare FHIR type name.
        var resolvedInstanceOf = context.ResolveAlias(instance.InstanceOf);

        // Resolve the type name
        var ar = new AliasResolver(context.CompiledStructureDefinitions);
        IResourceResolver imr = (options?.Resolver == null) ? ar : new MultiResolver(ar, options?.Resolver);
        var classMap = context.ResolveClassMappingForProfile(resolvedInstanceOf, inspector, imr, out string? resolvedCanonicalUrl);

        if (classMap is null || !classMap.IsResource)
        {
            // Still unresolved — emit a warning so callers can surface the miss.
            context.Warnings.Add(new CompilerWarning
            {
                EntityName = instance.Name,
                Message = $"Instance '{instance.Name}' InstanceOf '{instance.InstanceOf}' could not be resolved to a known FHIR resource type; instance skipped."
            });
            return null;
        }

        var typeName = classMap.Name;

        if (Activator.CreateInstance(classMap.NativeType) is not FhirResource resource)
            return null;

        // C-IN1: Set resource Id from entity name (kebab-case by convention, but use Name as-is).
        resource.Id = instance.Name;

        // C-IN2: Set meta.profile to the InstanceOf canonical URL when it looks like a URL,
        // or when a canonical base is set (so we can construct it).
        var instanceOfUrl = resolvedCanonicalUrl;
        if (!IsAbsoluteUrl(instanceOfUrl))
        {
            // Not a URL — try to build one with the canonical base if available.
            if (!string.IsNullOrEmpty(opts.CanonicalBase))
                instanceOfUrl = $"{opts.CanonicalBase.TrimEnd('/')}/StructureDefinition/{resolvedInstanceOf}";
            else
                instanceOfUrl = null; // can't determine URL; skip meta.profile
        }
        if (!string.IsNullOrEmpty(instanceOfUrl) && !inspector.IsKnownResource(resolvedInstanceOf))
        {
            resource.Meta ??= new Meta();
            resource.Meta.Profile = [instanceOfUrl];
        }

        // Apply instance rules.
        ApplyInstanceRules(instance.Rules, resource, context, opts, inspector);

        return resource;
    }

    /// <summary>
    /// Attempts to set a string property by name on a FHIR resource.
    /// Silently ignores properties that don't exist or are not string-typed.
    /// </summary>
    private static void TrySetStringProperty(Base obj, ClassMapping classMap, string propName, string value)
    {
        var propMap = classMap.FindMappedElementByName(propName);
        if (propMap is null) return;
        if (propMap.ImplementingType == typeof(FhirString))
            propMap.SetValue(obj, new FhirString(value));
        else if (propMap.ImplementingType == typeof(Markdown))
            propMap.SetValue(obj, new Markdown(value));
        else if (propMap.ImplementingType == typeof(string))
            propMap.SetValue(obj, value);
    }

    private static void ApplyInstanceRules(
        IEnumerable<InstanceRule> rules,
        FhirResource resource,
        CompilerContext context,
        CompilerOptions opts,
        ModelInspector inspector,
        Dictionary<string, int>? softIndexState = null)
    {
        // C-FP2: Soft-index state — maps path prefix → current resolved index.
        // The state dictionary persists across ALL rules in this entity's rule list so that
        // each [+] accumulates sequentially and [=] can reference a [+] from a previous rule.
        // When called recursively (for InstanceInsertRule expansion), the outer state is passed
        // through so that [+] counters continue from wherever the outer rules left off.
        // [+] increments (or starts at 0) and stores the new index in state.
        // [=] reuses the stored index (defaults to 0 if no prior [+] has been seen for this prefix).
        softIndexState ??= new Dictionary<string, int>(StringComparer.Ordinal);

        // C-IN5: Extension-aware alias resolver.  When a named-index segment like
        // extension[InitialExpressionExtension] is encountered, the name is first checked
        // against the alias table; if no alias is found, it falls back to looking up the
        // entity name in CompiledStructureDefinitions so that the canonical URL (Url) is
        // returned.  This turns the local FSH entity name into the correct extension URL.
        // As a further fallback, search compiled StructureDefinitions for an extension slice
        // with the given name (e.g. `expansionProperty` → the extension profile URL declared
        // in a `contains` rule) so that extension slice names from profiles are resolved.
        string ResolveExtensionAwareAlias(string name)
        {
            var resolved = context.ResolveAlias(name);
            if (resolved == name && !IsAbsoluteUrl(name) && !name.StartsWith('$'))
            {
                // Try direct SD lookup by FSH entity name / id.
                if (context.CompiledStructureDefinitions.TryGetValue(name, out var sd) &&
                    !string.IsNullOrEmpty(sd.Url))
                {
                    return sd.Url;
                }
                // Resolve bare CodeSystem entity names to their canonical URLs
                // (e.g. KeyboardTypeCodes → http://hl7.org/fhir/uv/sdc/CodeSystem/keyboardType).
                if (context.CodeSystemUrls.TryGetValue(name, out var csUrl))
                    return csUrl;
                // Try extension slice name lookup across all compiled profiles.
                var sliceUrl = context.FindExtensionUrlBySliceName(name);
                if (sliceUrl != null)
                    return sliceUrl;
            }
            return resolved;
        }

        var canonicalResolver = BuildMergedResolver(context, opts.Resolver);
        foreach (var rule in rules)
        {
            switch (rule)
            {
                // InstancePathRule (`* input[+]` with no value) acts as a soft-index
                // counter advance when rules are expanded from parameterised RuleSets.
                // The [+] in the path must be resolved to move softIndexState forward
                // so that subsequent [=] references in sibling rules use the same slot.
                case InstancePathRule pathRule when !string.IsNullOrEmpty(pathRule.Path):
                    ResolveSoftIndices(pathRule.Path, softIndexState);
                    break;

                case InstanceFixedValueRule fixedRule when
                    !string.IsNullOrEmpty(fixedRule.Path) && fixedRule.Value != null:
                    // Skip empty-string values produced by empty parameter substitutions in
                    // rulesets (e.g. `* definition = ""` when definition param is omitted).
                    // This matches sushi behavior: empty parameter values do not set FHIR
                    // properties, which avoids spurious `"definition": ""` fields in the output.
                    if (fixedRule.Value is StringValue emptyCheck && string.IsNullOrEmpty(emptyCheck.Value))
                        break;
                    var resolvedPath = ResolveSoftIndices(fixedRule.Path, softIndexState);
                    // C-RT1: resourceType as type discriminator for abstract Resource-typed properties.
                    // When the rule is `X.resourceType = "Patient"` and the property at path X is an
                    // abstract Resource, create a concrete instance of the named type and set it.
                    // This handles patterns like Bundle.entry.resource where the property type is
                    // the abstract Resource base class and the FSH specifies the concrete type via
                    // the resourceType field (e.g. `* resource.resourceType = "Patient"`).
                    if (fixedRule.Value is StringValue rtSv &&
                        resolvedPath.Contains('.') &&
                        resolvedPath.EndsWith(".resourceType", StringComparison.Ordinal))
                    {
                        var resourceParentPath = resolvedPath[..^".resourceType".Length];
                        if (TryCreateConcreteResourceAtPath(resource, resourceParentPath, rtSv.Value, inspector, ResolveExtensionAwareAlias, canonicalResolver))
                            break;
                    }
                    // When the value is a NameValue (cross-instance reference) and the leaf
                    // property accepts a Resource, build the referenced instance and embed it
                    // inline.  This covers both DomainResource.contained[] and any other
                    // Resource-typed leaf (e.g. Parameters.parameter[].resource).
                    if (fixedRule.Value is NameValue nameRef &&
                        (context.Instances.TryGetValue(nameRef.Value, out var refInstance) ||
                         TryFindInstanceByFhirId(context.Instances, nameRef.Value, out refInstance)))
                    {
                        var segments = SplitInstancePath(resolvedPath);
                        if (segments.Length > 0)
                        {
                            // Navigate to the parent of the leaf.
                            Base? parent = resource;
                            for (int si = 0; si < segments.Length - 1 && parent != null; si++)
                            {
                                var (sn, sIdx, sni) = ParseInstanceSegment(segments[si]);
                                parent = GetOrCreateInstanceChild(parent, sn, sIdx, inspector, sni, ResolveExtensionAwareAlias, canonicalResolver);
                            }
                            if (parent != null)
                            {
                                var (leafName, _, _) = ParseInstanceSegment(segments[^1]);
                                var parentClassMap = inspector.FindClassMapping(parent.GetType());
                                var leafPropMap = parentClassMap?.FindMappedElementByName(leafName);
                                if (leafPropMap != null &&
                                    typeof(Hl7.Fhir.Model.Resource).IsAssignableFrom(leafPropMap.ImplementingType))
                                {
                                    var embeddedResource = BuildInstance(refInstance, context, opts, forContained: true);
                                    if (embeddedResource != null)
                                    {
                                        if (leafPropMap.IsCollection)
                                        {
                                            // e.g. DomainResource.contained[]
                                            var list = leafPropMap.GetValue(parent) as System.Collections.IList;
                                            if (list is null)
                                            {
                                                var listType = typeof(List<>).MakeGenericType(leafPropMap.ImplementingType);
                                                list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                                                leafPropMap.SetValue(parent, list);
                                            }
                                            list.Add(embeddedResource);
                                        }
                                        else
                                        {
                                            // e.g. Parameters.ParameterComponent.Resource
                                            leafPropMap.SetValue(parent, embeddedResource);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    SetInstancePath(resource, resolvedPath, ResolveInstanceCanonical(fixedRule.Value, context, opts, inspector), inspector, ResolveExtensionAwareAlias, canonicalResolver);
                    // C-EXT1: After setting a value that navigates through a named Extension
                    // (e.g. extension[code]), sync the soft-index state for that extension list
                    // so that a subsequent extension[+] correctly appends AFTER the named
                    // extension rather than overwriting it at index 0.
                    SyncSoftIndexForNamedExtensions(resolvedPath, resource, softIndexState, inspector, ResolveExtensionAwareAlias, canonicalResolver);
                    break;

                case InstanceInsertRule insertRule:
                    var resolved = RuleSetResolver.Resolve(
                        insertRule.RuleSetReference, insertRule.IsParameterized,
                        insertRule.Parameters, context, useInstanceWrapper: true);
                    var instanceRules = resolved.OfType<InstanceRule>().ToList();
                    // C-RL1 (Instance): when the insert rule has a path context (e.g. it is
                    // indented under `* rest`), prepend that path to every resolved rule so
                    // that the ruleset expansion is applied at the correct location.
                    if (!string.IsNullOrEmpty(insertRule.Path) && instanceRules.Count > 0)
                    {
                        instanceRules = instanceRules
                            .Select(r => CloneInstanceRuleWithPath(r,
                                string.IsNullOrEmpty(r.Path)
                                    ? insertRule.Path
                                    : CombineFshPaths(insertRule.Path, r.Path)))
                            .ToList();
                    }
                    ApplyInstanceRules(instanceRules, resource, context, opts, inspector, softIndexState);
                    break;
            }
        }
    }

    /// <summary>
    /// Resolves soft-index tokens (<c>[+]</c> and <c>[=]</c>) in an instance path to
    /// numeric indices, updating <paramref name="state"/> as a side-effect.
    /// </summary>
    /// <remarks>
    /// The FSH spec (§Soft Indexing) defines:
    /// <list type="bullet">
    ///   <item>
    ///     <c>[+]</c> — increment the running index for the path-prefix up to this segment
    ///     and store the new index in <paramref name="state"/>.
    ///   </item>
    ///   <item>
    ///     <c>[=]</c> — reuse the stored index for this prefix (0 if no prior <c>[+]</c>
    ///     has been seen; note: <c>[=]</c> does NOT update <paramref name="state"/>).
    ///   </item>
    /// </list>
    /// </remarks>
    private static string ResolveSoftIndices(string path, Dictionary<string, int> state)
    {
        if (!path.Contains("[+]") && !path.Contains("[=]")) return path;

        // Process segment by segment, keeping track of the resolved prefix.
        var segments = path.Split('.');
        var resolvedSegments = new string[segments.Length];
        var resolvedPrefix = string.Empty;

        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];

            if (seg.Contains("[+]"))
            {
                var baseName = seg.Replace("[+]", string.Empty).TrimEnd();
                var prefixKey = string.IsNullOrEmpty(resolvedPrefix)
                    ? baseName
                    : $"{resolvedPrefix}.{baseName}";

                // Increment index for this path prefix.
                var currentIdx = state.TryGetValue(prefixKey, out var existing) ? existing + 1 : 0;
                state[prefixKey] = currentIdx;

                resolvedSegments[i] = $"{baseName}[{currentIdx}]";
                resolvedPrefix = string.IsNullOrEmpty(resolvedPrefix)
                    ? $"{baseName}[{currentIdx}]"
                    : $"{resolvedPrefix}.{baseName}[{currentIdx}]";
            }
            else if (seg.Contains("[=]"))
            {
                var baseName = seg.Replace("[=]", string.Empty).TrimEnd();
                var prefixKey = string.IsNullOrEmpty(resolvedPrefix)
                    ? baseName
                    : $"{resolvedPrefix}.{baseName}";

                // Reuse the last [+] index for this prefix (default to 0 if none seen yet).
                var currentIdx = state.TryGetValue(prefixKey, out var existing) ? existing : 0;

                resolvedSegments[i] = $"{baseName}[{currentIdx}]";
                resolvedPrefix = string.IsNullOrEmpty(resolvedPrefix)
                    ? $"{baseName}[{currentIdx}]"
                    : $"{resolvedPrefix}.{baseName}[{currentIdx}]";
            }
            else
            {
                resolvedSegments[i] = seg;
                resolvedPrefix = string.IsNullOrEmpty(resolvedPrefix)
                    ? seg
                    : $"{resolvedPrefix}.{seg}";
            }
        }

        return string.Join('.', resolvedSegments);
    }

    /// <summary>
    /// After setting a value at a path that contains named Extension accesses (e.g.
    /// <c>extension[code]</c>), syncs the soft-index state so that a subsequent
    /// <c>extension[+]</c> at the same level starts AFTER the named extension rather than
    /// colliding with it at index 0.
    /// </summary>
    /// <remarks>
    /// FSH soft indices (<c>[+]</c>) are tracked independently from named indices
    /// (<c>extension[name]</c>).  When a named Extension is added to a list (e.g. at
    /// position 0), the soft-index counter for that list has no knowledge of the newly
    /// added element and defaults to starting at 0 on the first <c>[+]</c>, which
    /// overwrites the named extension.  Syncing the state to <c>listCount - 1</c> after
    /// any named access ensures the next <c>[+]</c> resolves to <c>listCount</c>
    /// (appended after all existing elements).
    /// </remarks>
    private static void SyncSoftIndexForNamedExtensions(
        string resolvedPath,
        Base resource,
        Dictionary<string, int> state,
        ModelInspector inspector,
        Func<string, string>? aliasResolver, 
        IResourceResolver canonicalResolver)
    {
        var segments = SplitInstancePath(resolvedPath);
        // Skip the last segment (the leaf value being set — not an intermediate node).
        var parentSegmentCount = segments.Length - 1;
        if (parentSegmentCount <= 0) return;

        Base? current = resource;
        var resolvedSegmentParts = new List<string>(parentSegmentCount);

        for (int i = 0; i < parentSegmentCount; i++)
        {
            if (current is null) break;
            var seg = segments[i];
            var (name, idx, namedIdx) = ParseInstanceSegment(seg);

            if (namedIdx != null && name == "extension")
            {
                // Build the soft-index key matching ResolveSoftIndices' prefixKey calculation.
                var parentPrefix = string.Join('.', resolvedSegmentParts);
                var key = string.IsNullOrEmpty(parentPrefix) ? name : $"{parentPrefix}.{name}";

                // Determine the actual position of this named extension in the list.
                // Using the named extension's list position (rather than listCount - 1) prevents
                // auto-added sibling extensions (e.g. "value%5Bx%5D") from inflating the counter
                // and corrupting subsequent [=] index resolution.
                var classMap = inspector.FindClassMapping(current.GetType());
                var propMap = classMap?.FindMappedElementByName(name);
                if (propMap?.IsCollection == true)
                {
                    var list = propMap.GetValue(current) as System.Collections.IList;
                    if (list != null && list.Count > 0)
                    {
                        var resolvedUrl = aliasResolver != null ? aliasResolver(namedIdx) : namedIdx;
                        int namedExtPos = -1;
                        for (int li = 0; li < list.Count; li++)
                        {
                            if (list[li] is Hl7.Fhir.Model.Extension ext && ext.Url == resolvedUrl)
                            {
                                namedExtPos = li;
                                break;
                            }
                        }
                        // If not yet in the list, it will be appended at list.Count.
                        if (namedExtPos < 0) namedExtPos = list.Count;
                        if (!state.TryGetValue(key, out var currentState) || currentState < namedExtPos)
                            state[key] = namedExtPos;
                    }
                }
            }

            // Navigate to next segment. Stop if navigation returns null (non-navigable type).
            current = GetOrCreateInstanceChild(current, name, idx, inspector, namedIdx, aliasResolver, canonicalResolver);
            resolvedSegmentParts.Add(seg);
        }
    }


    /// <paramref name="path"/>, creating intermediate objects and list elements as needed.
    /// Returns <c>true</c> when the leaf value was set successfully.
    /// </summary>
    private static bool SetInstancePath(Base obj, string path, FshValue value, ModelInspector inspector, Func<string, string>? aliasResolver, IResourceResolver canonicalResolver)
    {
        var segments = SplitInstancePath(path);
        if (segments.Length == 0) return false;

        var current = obj;
        Base? parentOfLeaf = null;
        string? parentPropName = null;

        // Navigate to the parent of the leaf element.
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var (segName, segIdx, segNamedIdx) = ParseInstanceSegment(segments[i]);
            parentOfLeaf = current;
            parentPropName = segName;
            current = GetOrCreateInstanceChild(current, segName, segIdx, inspector, segNamedIdx, aliasResolver, canonicalResolver);
            if (current is null) return false;
        }

        // Set the leaf.
        var (leafName, leafIdx, _) = ParseInstanceSegment(segments[segments.Length - 1]);
        bool success;
        if (FhirCaretValueWriter.TrySetIndexed(current, leafName, leafIdx, value, inspector, aliasResolver, canonicalResolver))
            success = true;
        else
            success = FhirCaretValueWriter.TrySetChoiceTypeLeaf(current, leafName, value, inspector, aliasResolver, canonicalResolver);

        // C-EXT2 (Sushi compat): when setting the `url` of an Extension to a value that
        // contains FHIR choice-type brackets (e.g. "value[x]"), also append a sibling empty
        // Extension with the percent-encoded URL (e.g. "value%5Bx%5D").  Sushi emits this
        // pair to signal the choice-type nature of the property to downstream tooling.
        if (success && leafName == "url" &&
            current is Hl7.Fhir.Model.Extension &&
            value is StringValue sv && sv.Value.Contains('[') &&
            parentOfLeaf != null && parentPropName != null)
        {
            var encodedUrl = sv.Value.Replace("[", "%5B").Replace("]", "%5D");
            var gClassMap = inspector.FindClassMapping(parentOfLeaf.GetType());
            var gPropMap = gClassMap?.FindMappedElementByName(parentPropName);
            if (gPropMap?.IsCollection == true &&
                gPropMap.GetValue(parentOfLeaf) is System.Collections.IList extList)
            {
                bool siblingExists = extList.Cast<object>()
                    .Any(e => e is Hl7.Fhir.Model.Extension sib && sib.Url == encodedUrl);
                if (!siblingExists)
                    extList.Add(new Hl7.Fhir.Model.Extension { Url = encodedUrl });
            }
        }

        return success;
    }

    /// <summary>
    /// Sets a CodeSystem caret-value path on <paramref name="obj"/>, navigating dot-separated
    /// segments and falling back to FHIR choice-type handling for the leaf element when the
    /// direct property lookup fails.  This fallback is intentionally scoped to the CodeSystem
    /// caret-value path so that it does not alter behavior for general instance rule paths.
    /// </summary>
    private static bool SetCsCaretPath(Base obj, string path, FshValue value, ModelInspector inspector, Func<string, string>? aliasResolver, IResourceResolver canonicalResolver)
    {
        var segments = SplitInstancePath(path);
        if (segments.Length == 0) return false;

        var current = obj;

        // Navigate to the parent of the leaf element.
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var (segName, segIdx, segNamedIdx) = ParseInstanceSegment(segments[i]);
            current = GetOrCreateInstanceChild(current, segName, segIdx, inspector, segNamedIdx, aliasResolver, canonicalResolver);
            if (current is null) return false;
        }

        // Set the leaf — try normal path first, then choice-type fallback.
        var (leafName, leafIdx, _) = ParseInstanceSegment(segments[^1]);
        if (FhirCaretValueWriter.TrySetIndexed(current, leafName, leafIdx, value, inspector, aliasResolver, canonicalResolver))
            return true;

        // Choice-type fallback: e.g. "valueDecimal", "admitReasonCoding" — scan right-to-left
        // for a suffix that is a recognised FHIR DataType name.
        return FhirCaretValueWriter.TrySetChoiceTypeLeaf(current, leafName, value, inspector, aliasResolver, canonicalResolver);
    }

    /// <summary>
    /// Navigates into (or creates) a child element of <paramref name="parent"/> by property
    /// <paramref name="name"/> at list <paramref name="index"/>.
    /// Returns <c>null</c> when the property is not found or cannot be instantiated.
    /// </summary>
    /// <param name="namedIndex">
    /// When the bracket content was not a numeric index (e.g. <c>extension[$alias]</c> or
    /// <c>extension[http://...]</c>), this is the raw bracket text.  For Extension collections
    /// the value is resolved via <paramref name="aliasResolver"/> and used as
    /// <see cref="Extension.Url"/>.  When the extension list already contains an entry with that
    /// url it is reused; otherwise a new entry is appended (regardless of
    /// <paramref name="index"/>).
    /// </param>
    private static Base? GetOrCreateInstanceChild(
        Base parent, string name, int index, ModelInspector inspector,
        string? namedIndex, Func<string, string>? aliasResolver, IResourceResolver canonicalResolver)
    {
        var classMap = inspector.FindClassMapping(parent.GetType());
        if (classMap is null) return null;

        var propMap = classMap.FindMappedElementByName(name);
        if (propMap is null)
        {
            // Choice-type fallback: the path segment uses a typed variant name such as
            // "valueExpression" where the underlying FHIR property is "value[x]".
            // Scan right-to-left for an uppercase boundary, check whether the suffix names a
            // recognized FHIR DataType, and whether the base is a mapped property.  When found,
            // get-or-create an instance of the concrete DataType and return it so the caller can
            // continue navigating into its children.
            return GetOrCreateChoiceTypeChild(parent, name, classMap, inspector);
        }

        // Determine the concrete instantiable type.
        var concreteType = propMap.ImplementingType;
        if (concreteType is null || concreteType.IsAbstract)
        {
            // For abstract types, return the existing value if present (e.g., a concrete Resource
            // that was pre-created by TryCreateConcreteResourceAtPath via a resourceType rule).
            if (!propMap.IsCollection)
                return propMap.GetValue(parent) as Base;
            // For abstract collection types, return the item at the requested index if present.
            if (propMap.GetValue(parent) is System.Collections.IList existingList && existingList.Count > index)
                return existingList[index] as Base;
            return null;
        }

        if (propMap.IsCollection)
        {
            var list = propMap.GetValue(parent) as System.Collections.IList;
            if (list is null)
            {
                var listType = typeof(List<>).MakeGenericType(concreteType);
                list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                propMap.SetValue(parent, list);
            }

            // Named-slice: when the bracket content is a non-numeric alias or URL reference
            // (e.g. extension[$questionnaire-versionAlgorithm]) find-or-create by Extension.Url.
            if (namedIndex is not null && concreteType == typeof(Hl7.Fhir.Model.Extension))
            {
                var resolvedUrl = aliasResolver is not null
                    ? aliasResolver(namedIndex)
                    : namedIndex;
                // Percent-encode bracket characters in absolute URLs so that FHIR choice-type
                // markers like [x] serialize as %5Bx%5D.  Only applied to absolute URLs;
                // relative identifiers and slice names are left unchanged.
                // Uri.AbsoluteUri is not used here: it appends a trailing slash to bare-host
                // URIs and does not encode [ ] in paths on .NET.
                if (IsAbsoluteUrl(resolvedUrl))
                    resolvedUrl = resolvedUrl.Replace("[", "%5B").Replace("]", "%5D");

                // Reuse existing extension with the same url if present.
                foreach (var item in list)
                {
                    if (item is Hl7.Fhir.Model.Extension existing && existing.Url == resolvedUrl)
                        return existing;
                }

                // Create a new Extension with the resolved url.
                var newExt = new Hl7.Fhir.Model.Extension { Url = resolvedUrl };
                list.Add(newExt);
                return newExt;
            }

            // Named-slice on a non-Extension collection (e.g. Parameters.parameter[response]):
            // find-or-create the item whose `name` property matches the namedIndex value.
            // This handles FHIR elements like Parameters.ParameterComponent where slices
            // are identified by the `name` property rather than a URL.
            if (namedIndex is not null)
            {
                var concreteClassMap = inspector.FindClassMapping(concreteType);
                var namePropMap = concreteClassMap?.FindMappedElementByName("name");
                if (namePropMap != null)
                {
                    // Search for an existing item with the matching name.
                    foreach (var item in list)
                    {
                        if (item is Base existing)
                        {
                            var nameVal = namePropMap.GetValue(existing);
                            var nameStr = nameVal switch
                            {
                                FhirString fs => fs.Value,
                                string s => s,
                                _ => null
                            };
                            if (string.Equals(nameStr, namedIndex, StringComparison.Ordinal))
                                return existing;
                        }
                    }

                    // Create a new item and set its name property to the namedIndex value.
                    var newItem = Activator.CreateInstance(concreteType) as Base;
                    if (newItem != null)
                    {
                        if (namePropMap.ImplementingType == typeof(FhirString))
                            namePropMap.SetValue(newItem, new FhirString(namedIndex));
                        else if (namePropMap.ImplementingType == typeof(string))
                            namePropMap.SetValue(newItem, namedIndex);
                        list.Add(newItem);
                        return newItem;
                    }
                }
            }

            while (list.Count <= index)
                list.Add(Activator.CreateInstance(concreteType));

            return list[index] as Base;
        }
        else
        {
            var child = propMap.GetValue(parent) as Base;
            if (child is null)
            {
                child = Activator.CreateInstance(concreteType) as Base;
                if (child is null) return null;
                propMap.SetValue(parent, child);
            }
            return child;
        }
    }

    /// <summary>
    /// Handles intermediate path segments that use FHIR choice-type syntax
    /// (e.g. <c>valueExpression</c> → <c>value[x]</c> of type <c>Expression</c>).
    /// When a matching base property and DataType suffix are found, gets or creates an instance
    /// of the concrete DataType on the parent and returns it for further navigation.
    /// Returns <c>null</c> when no valid split is found.
    /// </summary>
    private static Base? GetOrCreateChoiceTypeChild(
        Base parent, string name, ClassMapping classMap, ModelInspector inspector)
    {
        for (int i = name.Length - 1; i >= 1; i--)
        {
            if (!char.IsUpper(name[i])) continue;

            var typeSuffix = name[i..];
            var baseName   = name[..i];

            var suffixType = inspector.FindClassMapping(typeSuffix);
            if (suffixType is null) continue;
            if (suffixType.NativeType.IsAbstract) continue;

            var basePropMap = classMap.FindMappedElementByName(baseName);
            if (basePropMap is null) continue;

            // The concrete type must be assignable to the property's implementing type.
            if (!basePropMap.ImplementingType.IsAssignableFrom(suffixType.NativeType)) continue;
            if (basePropMap.IsCollection) continue; // Don't handle collection value[x] here

            // Return the existing child if it's already the right type; otherwise create new.
            var existing = basePropMap.GetValue(parent) as Base;
            if (existing is not null && suffixType.NativeType.IsAssignableFrom(existing.GetType()))
                return existing;

            var child = Activator.CreateInstance(suffixType.NativeType) as Base;
            if (child is null) return null;
            basePropMap.SetValue(parent, child);
            return child;
        }

        return null;
    }

    /// <summary>Splits a FHIR instance path on <c>.</c> boundaries.</summary>
    private static string[] SplitInstancePath(string path) =>
        path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses a path segment such as <c>name</c>, <c>name[2]</c>, or
    /// <c>name[$alias]</c> into its name, zero-based numeric index (defaulting to 0), and
    /// optional named index string (non-<c>null</c> when the bracket content is not a plain
    /// integer — e.g. an alias like <c>$questionnaire-versionAlgorithm</c> or a URL).
    /// </summary>
    private static (string Name, int Index, string? NamedIndex) ParseInstanceSegment(string segment)
    {
        var bracketStart = segment.IndexOf('[');
        if (bracketStart < 0) return (segment, 0, null);

        var name = segment[..bracketStart];
        var bracketEnd = segment.IndexOf(']', bracketStart);
        var idxStr = bracketEnd > bracketStart + 1
            ? segment[(bracketStart + 1)..bracketEnd]
            : "0";

        if (int.TryParse(idxStr, out var idx))
            return (name, idx, null);

        // Non-numeric bracket content — return it as the named index.
        return (name, 0, idxStr);
    }

    // ─── Mapping compiler ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies a FSH <see cref="Hl7.FhirShorthand.Serialization.Models.Mapping"/> entity to a target
    /// <see cref="StructureDefinition"/> by:
    /// <list type="bullet">
    ///   <item>Adding a <c>mapping</c> identity declaration to <c>sd.Mapping</c>.</item>
    ///   <item>
    ///     Adding per-element <see cref="ElementDefinition.MappingComponent"/> entries for
    ///     each <see cref="MappingMapRule"/> in the entity.
    ///   </item>
    /// </list>
    /// </summary>
    private static void ApplyMappingToSD(
        Hl7.FhirShorthand.Serialization.Models.Mapping mapping, StructureDefinition sd, CompilerContext context, CompilerOptions? opts = null)
    {
        // Register the mapping identity on the StructureDefinition.
        var identity = mapping.Id ?? mapping.Name;
        sd.Mapping ??= new List<StructureDefinition.MappingComponent>();
        if (!sd.Mapping.Any(m => m.Identity == identity))
        {
            sd.Mapping.Add(new StructureDefinition.MappingComponent
            {
                Identity = identity,
                Uri = mapping.Target,
                Name = mapping.Title,
                Comment = mapping.Description
            });
        }

        // Apply per-element mapping rules.
        foreach (var rule in mapping.Rules)
        {
            if (rule is not MappingMapRule mapRule) continue;

            ElementDefinition targetEd;
            if (string.IsNullOrEmpty(mapRule.Path) || mapRule.Path == ".")
                targetEd = sd.Differential.Element.First();
            else
            {
                // Resolve extension entity names used in bracket notation to their actual
                // local slice names in the differential.  For example, a mapping rule
                // "* modifierExtension[RenderingCriticalExtension] -> ..." uses the FSH
                // extension entity name, but the profile uses "named rendering-criticalExtension"
                // as the local slice name.  Resolve before calling GetOrCreateElement so
                // we target the correct existing slice rather than creating a duplicate.
                var resolvedPath = ResolveEntityNamesToSliceNames(mapRule.Path, sd, context, opts);
                targetEd = GetOrCreateElement(resolvedPath, sd);
            }

            targetEd.Mapping ??= new List<ElementDefinition.MappingComponent>();
            var map = new ElementDefinition.MappingComponent
            {
                Identity = identity,
                Map = mapRule.Target
            };
            if (mapRule.Language?.Length > 0)
            {
                var part = mapRule.Language.Split('#');
                map.Language = part.Last();
            }
            targetEd.Mapping.Add(map);
        }
    }

    /// <summary>
    /// Replaces bracket-notation extension entity names in a FSH rule path with the
    /// actual local slice names used in the compiled StructureDefinition's differential.
    /// For example, <c>modifierExtension[RenderingCriticalExtension]</c> resolves to
    /// <c>modifierExtension[rendering-criticalExtension]</c> when the SD's differential
    /// has a named slice <c>rendering-criticalExtension</c> whose type profile URL matches
    /// the compiled URL of the <c>RenderingCriticalExtension</c> extension entity.
    /// </summary>
    private static string ResolveEntityNamesToSliceNames(
        string fshPath,
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions? opts)
    {
        if (!fshPath.Contains('[')) return fshPath;

        var pathPrefix = GetElementPathPrefix(sd);
        var segments = fshPath.Split('.');
        var result = new System.Text.StringBuilder();

        foreach (var seg in segments)
        {
            if (result.Length > 0) result.Append('.');

            var bracketStart = seg.IndexOf('[');
            if (bracketStart < 0)
            {
                result.Append(seg);
                continue;
            }

            var bracketEnd = seg.IndexOf(']', bracketStart);
            if (bracketEnd < 0)
            {
                result.Append(seg);
                continue;
            }

            var fieldName = seg[..bracketStart];
            var bracketContent = seg[(bracketStart + 1)..bracketEnd];

            // Skip if already a numeric index or a known slice name directly in the SD.
            if (int.TryParse(bracketContent, out _))
            {
                result.Append(seg);
                continue;
            }

            // Try to find an existing slice in the SD differential whose type profile
            // URL corresponds to the compiled extension entity named bracketContent.
            var existingSlice = FindSliceByExtensionEntityName(fieldName, bracketContent, pathPrefix, sd, context, opts);
            if (existingSlice != null && !string.IsNullOrEmpty(existingSlice.SliceName))
                result.Append($"{fieldName}[{existingSlice.SliceName}]");
            else
                result.Append(seg);
        }

        return result.ToString();
    }

    /// <summary>
    /// Searches the SD's differential for a named slice on <paramref name="fieldName"/>
    /// whose type profile URL matches the compiled URL of the extension entity named
    /// <paramref name="entityName"/>.  Used to resolve extension entity names to their
    /// local FSH slice names in mapping rules.
    /// </summary>
    private static ElementDefinition? FindSliceByExtensionEntityName(
        string fieldName,
        string entityName,
        string sdPathPrefix,
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions? opts)
    {
        // Build the expected FHIR path for the field (e.g. "CodeSystem.modifierExtension").
        var fieldPath = string.IsNullOrEmpty(sdPathPrefix) ? fieldName : $"{sdPathPrefix}.{fieldName}";

        // Resolve the entity name to a compiled extension URL.
        string? entityUrl = null;
        if (context.CompiledStructureDefinitions.TryGetValue(entityName, out var entitySd))
            entityUrl = entitySd.Url;

        // Look for a named slice on this field path whose type profile matches the entity URL.
        foreach (var el in sd.Differential.Element)
        {
            if (!string.Equals(el.Path, fieldPath, StringComparison.Ordinal)) continue;
            if (string.IsNullOrEmpty(el.SliceName)) continue;

            // Direct name match (the local slice name equals the entity name).
            if (string.Equals(el.SliceName, entityName, StringComparison.OrdinalIgnoreCase))
                return el;

            // Match by type profile URL.
            if (entityUrl != null)
            {
                var profileUrl = el.Type?.FirstOrDefault()?.Profile?.FirstOrDefault();
                if (profileUrl != null && string.Equals(profileUrl, entityUrl, StringComparison.OrdinalIgnoreCase))
                    return el;
            }
        }

        return null;
    }

    // ─── Rule path-prefix helper (C-RL1) ────────────────────────────────────

    /// <summary>
    /// Resolves a <see cref="Hl7.FhirShorthand.Serialization.Models.Canonical"/> value whose URL is not yet absolute by
    /// looking up the referenced entity name in the compilation context and reading the
    /// explicit <c>* url = "..."</c> rule from the loaded instance.
    /// Returns the value unchanged when the URL is already absolute, when the entity is
    /// not in context, or when the entity has no explicit <c>url</c> rule.
    /// All other <see cref="FshValue"/> types are returned unchanged.
    /// </summary>
    private static FshValue ResolveInstanceCanonical(
        FshValue value,
        CompilerContext context,
        CompilerOptions opts,
        ModelInspector inspector)
    {
        // Resolve Reference to a local FSH instance name:
        //   Reference(myInstance) → Reference(ResourceType/instanceId)
        // This matches sushi's behaviour of qualifying local-instance references with
        // the FHIR base resource type prefix (e.g. QuestionnaireResponse/id).
        // The !Contains('/') guard skips references that are already qualified (ResourceType/id)
        // or relative URL paths; IsAbsoluteUrl skips fully-qualified http(s) references.
        if (value is Reference refVal &&
            !IsAbsoluteUrl(refVal.Type) &&
            !refVal.Type.Contains('/'))
        {
            if (context.Instances.TryGetValue(refVal.Type, out var referencedInst))
            {
                // Inline instances are embedded as contained resources; reference them with
                // the "#id" fragment syntax rather than the "ResourceType/id" prefix.
                var usage = referencedInst.Usage?.TrimStart('#').ToLowerInvariant();
                if (usage == "inline")
                {
                    var id = GetFixedStringValue(referencedInst.Rules, "id") ?? referencedInst.Name;
                    return new Reference { Type = $"#{id}", Display = refVal.Display };
                }

                var fhirType = ResolveInstanceFhirType(referencedInst.InstanceOf, context, inspector);
                if (fhirType != null)
                {
                    // Use the explicit * id = "..." rule when present, otherwise the instance name.
                    var id = GetFixedStringValue(referencedInst.Rules, "id") ?? referencedInst.Name;
                    return new Reference { Type = $"{fhirType}/{id}", Display = refVal.Display };
                }
            }
            return value;
        }

        if (value is not Hl7.FhirShorthand.Serialization.Models.Canonical can) return value;
        if (IsAbsoluteUrl(can.Url)) return value;

        if (context.Instances.TryGetValue(can.Url, out var canonicalRefInst))
        {
            // Use the explicit `* url = "..."` rule from the referenced instance.
            var urlValue = GetFixedStringValue(canonicalRefInst.Rules, "url");
            if (!string.IsNullOrEmpty(urlValue))
                return new Hl7.FhirShorthand.Serialization.Models.Canonical { Url = urlValue, Version = can.Version };
        }

        // Resolve Canonical references to compiled StructureDefinitions (Profiles, Extensions, etc.)
        // by entity name or id.  This handles `Canonical(SDCParametersQuestionnairePopulateIn)`
        // which references a Profile defined in another FSH file.
        if (context.CompiledStructureDefinitions.TryGetValue(can.Url, out var refSd)
            && !string.IsNullOrEmpty(refSd.Url))
        {
            return new Hl7.FhirShorthand.Serialization.Models.Canonical { Url = refSd.Url, Version = can.Version };
        }

        // Resolve Canonical references to pre-scanned ValueSet entities by name or id
        // (e.g. Canonical(QuestionnaireBehaviorConditions) → .../ValueSet/formBehaviorConditions).
        if (context.ValueSetUrls.TryGetValue(can.Url, out var vsCanonicalUrl))
            return new Hl7.FhirShorthand.Serialization.Models.Canonical { Url = vsCanonicalUrl, Version = can.Version };

        // Resolve Canonical references to pre-scanned CodeSystem entities by name or id.
        if (context.CodeSystemUrls.TryGetValue(can.Url, out var csCanonicalUrl))
            return new Hl7.FhirShorthand.Serialization.Models.Canonical { Url = csCanonicalUrl, Version = can.Version };

        // Fall back to constructing a canonical URL from the CanonicalBase when available.
        var resolved = ResolveBaseDefinitionCanonical(can.Url, can.Url, context, opts);
        if (!string.IsNullOrEmpty(resolved) && resolved != can.Url)
            return new Hl7.FhirShorthand.Serialization.Models.Canonical { Url = resolved, Version = can.Version };

        return value;
    }

    /// <summary>
    /// Resolves an FSH <c>InstanceOf</c> type name to the underlying FHIR base resource type
    /// (e.g. <c>SDCQuestionnaireResponse</c> → <c>QuestionnaireResponse</c>).
    /// Checks the ModelInspector for directly-known FHIR types first, then walks the compiled
    /// StructureDefinition chain in <paramref name="context"/> to handle profiled types.
    /// Returns <c>null</c> when the type cannot be resolved.
    /// </summary>
    private static string? ResolveInstanceFhirType(
        string? instanceOf, CompilerContext context, ModelInspector inspector)
    {
        if (string.IsNullOrEmpty(instanceOf)) return null;

        var typeName = context.ResolveAlias(instanceOf);

        // Direct FHIR resource type (e.g. "Task", "QuestionnaireResponse").
        if (inspector.IsKnownResource(typeName))
            return typeName;

        // Walk the compiled StructureDefinition chain to find the underlying FHIR type.
        // context.CompiledStructureDefinitions is indexed by entity name, URL, last URL segment,
        // and id, so a bare profile name like "SDCQuestionnaireResponse" will match.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = typeName;
        while (visited.Add(current) &&
               context.CompiledStructureDefinitions.TryGetValue(current, out var sd))
        {
            if (!string.IsNullOrEmpty(sd.Type) && inspector.IsKnownResource(sd.Type))
                return sd.Type;

            if (string.IsNullOrEmpty(sd.BaseDefinition))
                break;

            // Use the last URL segment as the next lookup key (matches how
            // RegisterStructureDefinition indexes compiled SDs).
            var lastSlash = sd.BaseDefinition.LastIndexOf('/');
            current = lastSlash >= 0 ? sd.BaseDefinition[(lastSlash + 1)..] : sd.BaseDefinition;
        }

        return null;
    }

    /// <summary>
    /// Returns the value of the first <see cref="InstanceFixedValueRule"/> in
    /// <paramref name="rules"/> whose <see cref="FshRule.Path"/> equals
    /// <paramref name="path"/> and whose value is a <see cref="StringValue"/>.
    /// Returns <c>null</c> when no matching rule is found.
    /// </summary>
    private static string? GetFixedStringValue(IEnumerable<InstanceRule> rules, string path) =>
        rules.OfType<InstanceFixedValueRule>()
             .FirstOrDefault(r => string.Equals(r.Path, path, StringComparison.Ordinal)
                                  && r.Value is StringValue)
             ?.Value is StringValue sv ? sv.Value : null;

    /// <summary>
    /// Tries to find an <see cref="Instance"/> in <paramref name="instances"/> whose computed
    /// FHIR <c>id</c> matches <paramref name="fhirId"/>.
    /// The FHIR id is either the explicit <c>* id = "…"</c> rule value or, when no such rule
    /// exists, the FSH entity name (which the compiler uses as the default id).
    /// This allows inline instances to be referenced by the id they will receive in the compiled
    /// output rather than by their FSH entity name.
    /// </summary>
    private static bool TryFindInstanceByFhirId(
        Dictionary<string, Hl7.FhirShorthand.Serialization.Models.Instance> instances,
        string fhirId,
        out Hl7.FhirShorthand.Serialization.Models.Instance? result)
    {
        foreach (var inst in instances.Values)
        {
            var id = GetFixedStringValue(inst.Rules, "id") ?? inst.Name;
            if (string.Equals(id, fhirId, StringComparison.Ordinal))
            {
                result = inst;
                return true;
            }
        }
        result = null;
        return false;
    }

    /// <summary>
    /// When <paramref name="abstractPath"/> leads to an abstract <see cref="FhirResource"/>-typed
    /// FHIR property (e.g. <c>Bundle.entry.resource</c>), creates a concrete instance of
    /// <paramref name="resourceTypeName"/> and sets it at that path.
    /// Returns <c>true</c> when a concrete resource was created; <c>false</c> otherwise.
    /// </summary>
    /// <remarks>
    /// This handles FSH patterns like <c>* resource.resourceType = "Patient"</c> where the
    /// <c>resource</c> property is typed as the abstract <c>Resource</c> base class and the
    /// concrete type must be inferred from the <c>resourceType</c> discriminator rule.
    /// </remarks>
    private static bool TryCreateConcreteResourceAtPath(
        Base root,
        string abstractPath,
        string resourceTypeName,
        ModelInspector inspector,
        Func<string, string>? aliasResolver, 
        IResourceResolver canonicalResolver)
    {
        var segments = SplitInstancePath(abstractPath);
        if (segments.Length == 0) return false;

        // Navigate to the parent of the abstract-typed property.
        Base? parent = root;
        for (int si = 0; si < segments.Length - 1 && parent != null; si++)
        {
            var (sn, sIdx, sni) = ParseInstanceSegment(segments[si]);
            parent = GetOrCreateInstanceChild(parent, sn, sIdx, inspector, sni, aliasResolver, canonicalResolver);
        }
        if (parent == null) return false;

        var (propName, propIdx, _) = ParseInstanceSegment(segments[^1]);
        var parentClassMap = inspector.FindClassMapping(parent.GetType());
        var propMap = parentClassMap?.FindMappedElementByName(propName);
        if (propMap?.ImplementingType == null ||
            !propMap.ImplementingType.IsAbstract ||
            !typeof(FhirResource).IsAssignableFrom(propMap.ImplementingType))
            return false;

        // Find and instantiate the concrete resource type.
        var concreteClassMap = inspector.FindClassMapping(resourceTypeName);
        if (concreteClassMap?.NativeType == null || concreteClassMap.NativeType.IsAbstract)
            return false;

        if (Activator.CreateInstance(concreteClassMap.NativeType) is not FhirResource concreteResource)
            return false;

        if (propMap.IsCollection)
        {
            var list = propMap.GetValue(parent) as System.Collections.IList;
            if (list is null)
            {
                var listType = typeof(List<>).MakeGenericType(propMap.ImplementingType);
                list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                propMap.SetValue(parent, list);
            }
            while (list.Count <= propIdx)
                list.Add(Activator.CreateInstance(concreteClassMap.NativeType)!);
            list[propIdx] = concreteResource;
        }
        else
        {
            // Only set if not already populated.
            if (propMap.GetValue(parent) == null)
                propMap.SetValue(parent, concreteResource);
        }

        return true;
    }

    /// <summary>
    /// Returns a shallow copy of <paramref name="rule"/> with <see cref="FshRule.Path"/>
    /// replaced by <paramref name="newPath"/>. Returns <c>null</c> for rule types that
    /// do not support a simple path replacement.
    /// </summary>
    private static FshRule? CloneRuleWithPath(FshRule rule, string newPath)
    {
        switch (rule)
        {
            case CardRule r:
                return new CardRule { Position = r.Position, Indent = r.Indent, Path = newPath, Cardinality = r.Cardinality, Flags = r.Flags };
            case FlagRule r:
                return new FlagRule { Position = r.Position, Indent = r.Indent, Path = newPath, AdditionalPaths = r.AdditionalPaths, Flags = r.Flags };
            case ValueSetRule r:
                return new ValueSetRule { Position = r.Position, Indent = r.Indent, Path = newPath, ValueSetName = r.ValueSetName, Strength = r.Strength };
            case FixedValueRule r:
                return new FixedValueRule { Position = r.Position, Indent = r.Indent, Path = newPath, Value = r.Value, Exactly = r.Exactly };
            case ContainsRule r:
                return new ContainsRule { Position = r.Position, Indent = r.Indent, Path = newPath, Items = r.Items };
            case OnlyRule r:
                return new OnlyRule { Position = r.Position, Indent = r.Indent, Path = newPath, TargetTypes = r.TargetTypes };
            case ObeysRule r:
                return new ObeysRule { Position = r.Position, Indent = r.Indent, Path = newPath, InvariantNames = r.InvariantNames };
            case CaretValueRule r:
                return new CaretValueRule { Position = r.Position, Indent = r.Indent, Path = newPath, CaretPath = r.CaretPath, Value = r.Value };
            case PathRule r:
                return new PathRule { Position = r.Position, Indent = r.Indent, Path = newPath };
            case LrCardRule r:
                return new LrCardRule { Position = r.Position, Indent = r.Indent, Path = newPath, Cardinality = r.Cardinality, Flags = r.Flags };
            case LrFlagRule r:
                return new LrFlagRule { Position = r.Position, Indent = r.Indent, Path = newPath, AdditionalPaths = r.AdditionalPaths, Flags = r.Flags };
            default:
                return null;
        }
    }

    /// <summary>
    /// Returns a shallow copy of <paramref name="rule"/> with <see cref="FshRule.Path"/>
    /// replaced by <paramref name="newPath"/>. Handles all concrete <see cref="InstanceRule"/>
    /// subtypes; returns the original rule unchanged for any unrecognised subtype.
    /// </summary>
    private static InstanceRule CloneInstanceRuleWithPath(InstanceRule rule, string newPath) =>
        rule switch
        {
            InstanceFixedValueRule r => new InstanceFixedValueRule
            {
                Position = r.Position,
                Indent = r.Indent,
                Path = newPath,
                Value = r.Value,
                Exactly = r.Exactly
            },
            InstancePathRule r => new InstancePathRule
            {
                Position = r.Position,
                Indent = r.Indent,
                Path = newPath
            },
            InstanceInsertRule r => new InstanceInsertRule
            {
                Position = r.Position,
                Indent = r.Indent,
                Path = newPath,
                RuleSetReference = r.RuleSetReference,
                Parameters = r.Parameters,
                IsParameterized = r.IsParameterized
            },
            _ => rule
        };
}
