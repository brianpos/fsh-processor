using fsh_processor.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using FhirCode = Hl7.Fhir.Model.Code;
using FhirExtension = Hl7.Fhir.Model.Extension;
using FhirResource = Hl7.Fhir.Model.Resource;
using FhirValueSet = Hl7.Fhir.Model.ValueSet;
using FhirCodeSystem = Hl7.Fhir.Model.CodeSystem;

namespace fsh_compiler;

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

        return CompileWithContext(docList, context, opts);
    }

    private static CompileResult<List<FhirResource>> CompileWithContext(
        IEnumerable<FshDoc> docs, CompilerContext context, CompilerOptions opts)
    {
        var errors = new List<CompilerError>();
        var resources = new List<FhirResource>();

        // Track entity-name → StructureDefinition for Mapping deferred processing.
        var sdByEntityName = new Dictionary<string, StructureDefinition>(StringComparer.Ordinal);

        // Collect Mapping entities for a second pass (they annotate already-compiled SDs).
        var pendingMappings = new List<(fsh_processor.Models.Mapping Mapping, string EntityName)>();

        foreach (var doc in docs)
        {
            foreach (var entity in doc.Entities)
            {
                try
                {
                    FhirResource? resource = entity switch
                    {
                        Profile profile => BuildProfile(profile, context, opts),
                        fsh_processor.Models.Extension ext => BuildExtension(ext, context, opts),
                        Logical logical => BuildLogical(logical, context, opts),
                        fsh_processor.Models.Resource fshResource => BuildResource(fshResource, context, opts),
                        fsh_processor.Models.ValueSet vs => BuildValueSet(vs, context, opts),
                        fsh_processor.Models.CodeSystem cs => BuildCodeSystem(cs, context, opts),
                        fsh_processor.Models.Instance inst => BuildInstance(inst, context, opts),
                        fsh_processor.Models.Mapping => null, // handled in second pass below
                        // Alias, RuleSet, and Invariant produce no FHIR resource
                        _ => null
                    };

                    if (resource != null)
                    {
                        resources.Add(resource);
                        if (resource is StructureDefinition sd)
                            sdByEntityName.TryAdd(entity.Name, sd);
                    }

                    // Queue Mapping entities for deferred processing.
                    if (entity is fsh_processor.Models.Mapping mapping)
                        pendingMappings.Add((mapping, entity.Name));
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
        }

        // Second pass: apply Mapping entities to the already-compiled StructureDefinitions.
        foreach (var (mapping, entityName) in pendingMappings)
        {
            try
            {
                if (mapping.Source is null ||
                    !sdByEntityName.TryGetValue(mapping.Source, out var targetSd))
                {
                    context.Warnings.Add(new CompilerWarning
                    {
                        EntityName = entityName,
                        Message = mapping.Source is null
                            ? "Mapping has no Source; skipped."
                            : $"Mapping Source '{mapping.Source}' does not match any compiled StructureDefinition; skipped."
                    });
                    continue;
                }

                ApplyMappingToSD(mapping, targetSd, context);
            }
            catch (Exception ex)
            {
                errors.Add(new CompilerError
                {
                    EntityName = entityName,
                    Message = ex.Message,
                    Position = mapping.Position
                });
            }
        }

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

        // C-PR3: Determine the profile's Kind from the parent type via the ModelInspector.
        // For profiles of resources → "resource"; for datatypes → "complex-type".
        // Fall back to "resource" when the type can't be resolved (most common case).
        var parentTypeName = profile.Parent?.Value ?? "DomainResource";
        var resolvedParent = context.ResolveAlias(parentTypeName);
        var kind = InferKindFromType(resolvedParent, opts.Inspector);

        // C-PR4: StructureDefinition.Type must be the bare FHIR type name.
        // When the parent is a URL (e.g. from an alias), strip to the last segment and
        // check whether that segment is a known base FHIR type via the ModelInspector.
        // Only use the stripped name when it is a recognised type; otherwise fall back to
        // the pre-alias-resolution name so profiles-of-profiles don't produce a bogus type.
        var typeValue = ExtractBareTypeName(resolvedParent, parentTypeName, opts.Inspector);

        var sd = new StructureDefinition
        {
            Id = profile.Id?.Value,
            Url = ResolveUrl(profile.Id?.Value, opts, "StructureDefinition"),
            Name = profile.Name,
            Title = profile.Title?.Value,
            Description = profile.Description?.Value,
            Status = PublicationStatus.Active,
            Experimental = false,
            Abstract = false,
            Kind = kind,
            Type = typeValue,
            BaseDefinition = resolvedParent,
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

        // Ensure root element (no ElementId — snapshot generator will derive it from the type)
        sd.Differential.Element.Add(new ElementDefinition(sd.Type) { Path = sd.Type });

        ApplySdRules(profile.Rules, sd, context, opts);
        return sd;
    }

    /// <summary>
    /// Infers the <see cref="StructureDefinition.StructureDefinitionKind"/> for a profile
    /// given its parent type name (possibly a URL).  Uses the optional <paramref name="inspector"/>
    /// to look up whether the type is a resource.  Defaults to <c>Resource</c> when the
    /// type cannot be resolved.
    /// </summary>
    private static StructureDefinition.StructureDefinitionKind InferKindFromType(
        string? parentTypeOrUrl, ModelInspector? inspector)
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
    /// checks whether it is a known base FHIR type via <paramref name="inspector"/>.
    /// Falls back to <paramref name="originalParent"/> (pre-alias-resolution name) so that
    /// profiles-of-profiles (where the URL segment is a profile id, not a type name) don't
    /// produce a bogus type value.
    /// </summary>
    private static string ExtractBareTypeName(
        string resolvedParent, string originalParent, ModelInspector? inspector)
    {
        if (!IsAbsoluteUrl(resolvedParent))
            return resolvedParent;   // Already bare (e.g. "Patient", "DomainResource").

        var lastSlash = resolvedParent.LastIndexOf('/');
        var segment = lastSlash >= 0 ? resolvedParent[(lastSlash + 1)..] : resolvedParent;

        // Only use the URL segment when the ModelInspector recognises it as a known FHIR type.
        // Otherwise keep the original pre-alias name (the next best approximation).
        if (inspector != null && inspector.FindClassMapping(segment) != null)
            return segment;

        // Alias name like "$someAlias" → strip the leading alias prefix to get a displayable fallback.
        return originalParent.TrimStart(AliasPrefix);
    }

    /// <summary>
    /// Converts a FSH <see cref="fsh_processor.Models.Extension"/> entity to a
    /// FHIR <see cref="StructureDefinition"/> of kind <c>complex-type</c>.
    /// </summary>
    public static StructureDefinition BuildExtension(
        fsh_processor.Models.Extension ext, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var sd = new StructureDefinition
        {
            Id = ext.Id,
            Url = ResolveUrl(ext.Id, opts, "StructureDefinition"),
            Name = ext.Name,
            Title = ext.Title,
            Description = ext.Description,
            Status = PublicationStatus.Active,
            Experimental = false,
            Abstract = false,
            Type = "Extension",
            BaseDefinition = context.ResolveAlias(ext.Parent ?? "http://hl7.org/fhir/StructureDefinition/Extension"),
            Derivation = StructureDefinition.TypeDerivationRule.Constraint,
            Kind = StructureDefinition.StructureDefinitionKind.ComplexType,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        sd.Differential.Element.Add(new ElementDefinition("Extension") { Path = "Extension" });

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
        return sd;
    }

    /// <summary>
    /// Converts a FSH <see cref="Logical"/> entity to a FHIR <see cref="StructureDefinition"/>
    /// of kind <c>logical</c>.
    /// </summary>
    public static StructureDefinition BuildLogical(
        Logical logical, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();

        // Logical models use the full canonical URL as Type (spec §Logical).
        var logicalUrl = ResolveUrl(logical.Id, opts, "StructureDefinition");
        var logicalType = !string.IsNullOrEmpty(logicalUrl) ? logicalUrl : logical.Name;

        var sd = new StructureDefinition
        {
            Id = logical.Id,
            Url = logicalUrl,
            Name = logical.Name,
            Title = logical.Title,
            Description = logical.Description,
            Status = PublicationStatus.Active,
            Experimental = false,
            Abstract = false,
            Type = logicalType,
            BaseDefinition = context.ResolveAlias(logical.Parent ?? "http://hl7.org/fhir/StructureDefinition/Base"),
            Derivation = StructureDefinition.TypeDerivationRule.Specialization,
            Kind = StructureDefinition.StructureDefinitionKind.Logical,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        sd.Differential.Element.Add(new ElementDefinition(logicalType) { Path = logicalType });

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
        return sd;
    }

    /// <summary>
    /// Converts a FSH <see cref="fsh_processor.Models.Resource"/> entity to a FHIR
    /// <see cref="StructureDefinition"/> of kind <c>resource</c>.
    /// </summary>
    public static StructureDefinition BuildResource(
        fsh_processor.Models.Resource fshResource, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var sd = new StructureDefinition
        {
            Id = fshResource.Id,
            Url = ResolveUrl(fshResource.Id, opts, "StructureDefinition"),
            Name = fshResource.Name,
            Title = fshResource.Title,
            Description = fshResource.Description,
            Status = PublicationStatus.Active,
            Experimental = false,
            Abstract = false,
            Type = fshResource.Name,
            BaseDefinition = context.ResolveAlias(fshResource.Parent ?? "http://hl7.org/fhir/StructureDefinition/DomainResource"),
            Derivation = StructureDefinition.TypeDerivationRule.Specialization,
            Kind = StructureDefinition.StructureDefinitionKind.Resource,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        sd.Differential.Element.Add(new ElementDefinition(sd.Type) { Path = sd.Type });

        ApplySdRules(fshResource.Rules, sd, context, opts);
        return sd;
    }

    // ─── ValueSet builder ────────────────────────────────────────────────────

    /// <summary>
    /// Converts a FSH <see cref="fsh_processor.Models.ValueSet"/> entity to a FHIR
    /// <see cref="FhirValueSet"/> resource.
    /// </summary>
    public static FhirValueSet BuildValueSet(
        fsh_processor.Models.ValueSet vs, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var fvs = new FhirValueSet
        {
            Id = vs.Id,
            Url = ResolveUrl(vs.Id, opts, "ValueSet"),
            Name = vs.Name,
            Title = vs.Title,
            Description = vs.Description,
            Status = PublicationStatus.Active,
            Experimental = false,
            Compose = new FhirValueSet.ComposeComponent
            {
                Include = new List<FhirValueSet.ConceptSetComponent>(),
                Exclude = new List<FhirValueSet.ConceptSetComponent>()
            }
        };

        foreach (var rule in vs.Rules)
        {
            switch (rule)
            {
                case VsComponentRule compRule:
                    ApplyVsComponentRule(compRule, fvs, context);
                    break;

                case VsCaretValueRule caretRule:
                    ApplyVsCaretValueRule(caretRule, fvs, opts.Inspector);
                    break;

                case VsInsertRule insertRule:
                    ApplyVsInsertRule(insertRule, fvs, context, opts);
                    break;

                case CodeCaretValueRule codeCaretRule:
                    ApplyCodeCaretValueRule(codeCaretRule, fvs, opts.Inspector);
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
    /// Converts a FSH <see cref="fsh_processor.Models.CodeSystem"/> entity to a FHIR
    /// <see cref="FhirCodeSystem"/> resource.
    /// </summary>
    public static FhirCodeSystem BuildCodeSystem(
        fsh_processor.Models.CodeSystem cs, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var fcs = new FhirCodeSystem
        {
            Id = cs.Id,
            Url = ResolveUrl(cs.Id, opts, "CodeSystem"),
            Name = cs.Name,
            Title = cs.Title,
            Description = cs.Description,
            Status = PublicationStatus.Active,
            Experimental = false,
            Content = CodeSystemContentMode.Complete,
            Concept = new List<FhirCodeSystem.ConceptDefinitionComponent>()
        };

        foreach (var rule in PropagateConceptCodesToCaretRules(cs.Rules))
        {
            switch (rule)
            {
                case Concept concept:
                    ApplyConceptRule(concept, fcs);
                    break;

                case CsCaretValueRule caretRule:
                    ApplyCsCaretValueRule(caretRule, fcs, opts.Inspector);
                    break;

                case CsInsertRule insertRule:
                    ApplyCsInsertRule(insertRule, fcs, context, opts);
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
                    : $"{parentPath}.{rule.Path}";
            }
            else
            {
                effectivePath = rule.Path;
            }

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

    private static void ApplySdRules(
        IEnumerable<FshRule> rules,
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        // C-FP1: Apply indented-rule path composition before processing.
        // This expands relative child paths (e.g. `* value[x]` under `* extension[x]`)
        // into their fully-qualified form (`extension[x].value[x]`).
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
                    ApplyFixedValueRule(fixedValueRule, sd, opts.Inspector);
                    break;

                case ContainsRule containsRule:
                    ApplyContainsRule(containsRule, sd, context);
                    break;

                case OnlyRule onlyRule:
                    ApplyOnlyRule(onlyRule, sd, context);
                    break;

                case ObeysRule obeysRule:
                    ApplyObeysRule(obeysRule, sd, context);
                    break;

                case CaretValueRule caretValueRule:
                    ApplyCaretValueRule(caretValueRule, sd, opts.Inspector);
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
                                var clone = CloneRuleWithPath(r, $"{pathPrefix}.{r.Path}");
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
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var min)) ed.Min = min;
            ed.Max = parts[1];
        }
        ApplyFlags(ed, flags);
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
            ValueSet = context.ResolveAlias(valueSetRule.ValueSetName)
        };
    }

    private static void ApplyFixedValueRule(FixedValueRule fixedValueRule, StructureDefinition sd, ModelInspector? inspector = null)
    {
        if (string.IsNullOrEmpty(fixedValueRule.Path) || fixedValueRule.Value is null) return;
        var ed = GetOrCreateElement(fixedValueRule.Path, sd);
        var dt = FhirValueMapper.ToDataType(fixedValueRule.Value, inspector);
        if (dt != null)
        {
            // "exactly" modifier → fixed[x]; omitted → pattern[x]
            if (fixedValueRule.Exactly)
                ed.Fixed = dt;
            else
                ed.Pattern = dt;
        }
    }

    private static void ApplyContainsRule(ContainsRule containsRule, StructureDefinition sd, CompilerContext context)
    {
        if (string.IsNullOrEmpty(containsRule.Path) || containsRule.Items.Count == 0) return;

        var ed = GetOrCreateElement(containsRule.Path, sd);
        ed.Slicing ??= new ElementDefinition.SlicingComponent
        {
            Rules = ElementDefinition.SlicingRules.Open,
            Ordered = false,
            Discriminator = new List<ElementDefinition.DiscriminatorComponent>()
        };

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
                if (int.TryParse(parts[0], out var min)) sliceEd.Min = min;
                sliceEd.Max = parts[1];
            }
            ApplyFlags(sliceEd, item.Flags);

            // Gap 10: when NamedAlias is set, populate ed.Type with the type from item.Name.
            if (item.NamedAlias != null)
            {
                var resolvedType = context.ResolveAlias(item.Name);
                sliceEd.Type =
                [
                    new ElementDefinition.TypeRefComponent { Code = resolvedType }
                ];
            }
        }
    }

    private static void ApplyOnlyRule(OnlyRule onlyRule, StructureDefinition sd, CompilerContext context)
    {
        if (string.IsNullOrEmpty(onlyRule.Path) || onlyRule.TargetTypes.Count == 0) return;
        var ed = GetOrCreateElement(onlyRule.Path, sd);
        ed.Type = onlyRule.TargetTypes
            .Select(tt => ParseTypeRef(tt, context))
            .ToList();
    }

    /// <summary>
    /// Parses a FSH target-type expression into a Firely <see cref="ElementDefinition.TypeRefComponent"/>.
    /// Handles bare type names as well as <c>Reference(...)</c>, <c>Canonical(...)</c>,
    /// and <c>CodeableReference(...)</c> expressions with optional " or "-separated targets.
    /// </summary>
    private static ElementDefinition.TypeRefComponent ParseTypeRef(string typeExpr, CompilerContext context)
    {
        typeExpr = typeExpr.Trim();

        // Reference(X) or Reference(X or Y or ...)
        if (typeExpr.StartsWith("Reference(", StringComparison.Ordinal) && typeExpr.EndsWith(")"))
        {
            var inner = typeExpr[10..^1];
            var targets = SplitOrTargets(inner).Select(t => context.ResolveAlias(t)).ToList();
            return new ElementDefinition.TypeRefComponent { Code = "Reference", TargetProfile = targets };
        }

        // Canonical(X|version) or Canonical(X or Y)
        if (typeExpr.StartsWith("Canonical(", StringComparison.Ordinal) && typeExpr.EndsWith(")"))
        {
            var inner = typeExpr[10..^1];
            var targets = SplitOrTargets(inner).Select(t => context.ResolveAlias(t)).ToList();
            return new ElementDefinition.TypeRefComponent { Code = "canonical", TargetProfile = targets };
        }

        // CodeableReference(X) or CodeableReference(X or Y)
        if (typeExpr.StartsWith("CodeableReference(", StringComparison.Ordinal) && typeExpr.EndsWith(")"))
        {
            var inner = typeExpr[18..^1];
            var targets = SplitOrTargets(inner).Select(t => context.ResolveAlias(t)).ToList();
            return new ElementDefinition.TypeRefComponent { Code = "CodeableReference", TargetProfile = targets };
        }

        // Bare type name (e.g. Quantity, string, boolean) — resolve through aliases as well
        return new ElementDefinition.TypeRefComponent { Code = context.ResolveAlias(typeExpr) };
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

            targetEd.Constraint.Add(constraint);
        }
    }

    private static void ApplyCaretValueRule(CaretValueRule caretValueRule, StructureDefinition sd, ModelInspector? inspector)
    {
        if (string.IsNullOrEmpty(caretValueRule.CaretPath)) return;

        // Caret rules without a path target the StructureDefinition itself.
        if (string.IsNullOrEmpty(caretValueRule.Path) || caretValueRule.Path == ".")
        {
            ApplySdCaretPath(caretValueRule, sd, inspector);
        }
        else
        {
            var ed = GetOrCreateElement(caretValueRule.Path, sd);
            ApplyEdCaretPath(caretValueRule, ed, inspector);
        }
    }

    private static void ApplySdCaretPath(CaretValueRule rule, StructureDefinition sd, ModelInspector? inspector)
    {
        var path = rule.CaretPath.TrimStart('^');
        if (FhirCaretValueWriter.TrySet(sd, path, rule.Value, inspector)) return;

        // Fall back to an extension for paths not in the StructureDefinition model
        sd.Extension ??= new List<FhirExtension>();
        sd.Extension.Add(new FhirExtension
        {
            Url = path,
            Value = FhirValueMapper.ToDataType(rule.Value, inspector)
        });
    }

    private static void ApplyEdCaretPath(CaretValueRule rule, ElementDefinition ed, ModelInspector? inspector)
    {
        var path = rule.CaretPath.TrimStart('^');
        if (FhirCaretValueWriter.TrySet(ed, path, rule.Value, inspector)) return;

        // Fall back to an extension for paths not in the ElementDefinition model
        ed.Extension ??= new List<FhirExtension>();
        ed.Extension.Add(new FhirExtension
        {
            Url = path,
            Value = FhirValueMapper.ToDataType(rule.Value, inspector)
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
            ed.Max = parts[1];
        }
        ApplyFlags(ed, addEl.Flags);
        if (!string.IsNullOrEmpty(addEl.ShortDescription)) ed.Short = addEl.ShortDescription;
        if (!string.IsNullOrEmpty(addEl.Definition)) ed.Definition = addEl.Definition;
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
        VsComponentRule rule, FhirValueSet fvs, CompilerContext context)
    {
        var component = new FhirValueSet.ConceptSetComponent();

        if (!string.IsNullOrEmpty(rule.FromSystem))
            component.System = context.ResolveAlias(rule.FromSystem);

        if (rule.FromValueSets.Count > 0)
            component.ValueSet = rule.FromValueSets.Select(vs => context.ResolveAlias(vs)).ToList();

        if (rule.IsConceptComponent && rule.ConceptCode != null)
        {
            component.Concept = new List<FhirValueSet.ConceptReferenceComponent>
            {
                new FhirValueSet.ConceptReferenceComponent
                {
                    Code = rule.ConceptCode.Value.TrimStart('#'),
                    Display = rule.ConceptCode.Display
                }
            };
        }

        if (rule.Filters.Count > 0)
        {
            component.Filter = rule.Filters
                .Select(f => new FhirValueSet.FilterComponent
                {
                    Property = f.Property,
                    Op = MapFilterOp(f.Operator),
                    Value = f.Value is StringValue sv ? sv.Value : f.Operator
                })
                .ToList();
        }

        if (rule.IsInclude == false)
            fvs.Compose!.Exclude.Add(component);
        else
            fvs.Compose!.Include.Add(component);
    }

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

    private static void ApplyVsCaretValueRule(VsCaretValueRule rule, FhirValueSet fvs, ModelInspector? inspector)
    {
        var path = rule.CaretPath.TrimStart('^');
        FhirCaretValueWriter.TrySet(fvs, path, rule.Value, inspector);
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

        foreach (var rule in resolved)
        {
            switch (rule)
            {
                case VsComponentRule compRule:
                    ApplyVsComponentRule(compRule, fvs, context);
                    break;
                case VsCaretValueRule vsCaretRule:
                    ApplyVsCaretValueRule(vsCaretRule, fvs, opts.Inspector);
                    break;
                // CaretValueRule (SD-style, no path) can appear in a RuleSet re-parsed
                // via a synthetic Profile wrapper and applies to the VS root.
                case CaretValueRule sdCaret when string.IsNullOrEmpty(sdCaret.Path):
                    var vsPath = sdCaret.CaretPath.TrimStart('^');
                    FhirCaretValueWriter.TrySet(fvs, vsPath, sdCaret.Value, opts.Inspector);
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
        CodeCaretValueRule rule, FhirValueSet fvs, ModelInspector? inspector)
    {
        if (rule.Codes.Count == 0 || string.IsNullOrEmpty(rule.CaretPath)) return;
        var path = rule.CaretPath.TrimStart('^');

        foreach (var codeStr in rule.Codes)
        {
            var bare = codeStr.TrimStart('#');
            var concept = FindConceptReferenceByCode(fvs, bare);
            if (concept != null)
                FhirCaretValueWriter.TrySet(concept, path, rule.Value, inspector);
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
                    ApplyCodeCaretValueRule(merged, fvs, opts.Inspector);
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
        var rootCode = concept.Codes[0].TrimStart('#');
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
            Code = codes[index].TrimStart('#')
        };
        parent.Concept ??= new List<FhirCodeSystem.ConceptDefinitionComponent>();
        parent.Concept.Add(child);
        AddNestedConcept(child, codes, index + 1);
    }

    private static void ApplyCsCaretValueRule(CsCaretValueRule rule, FhirCodeSystem fcs, ModelInspector? inspector)
    {
        var path = rule.CaretPath.TrimStart('^');

        if (rule.Codes.Count > 0)
        {
            // Per-concept caret: apply to the matching concept(s) rather than the CodeSystem itself
            foreach (var codeStr in rule.Codes)
            {
                var concept = FindConceptByCode(fcs.Concept, codeStr.TrimStart('#'));
                if (concept != null)
                    FhirCaretValueWriter.TrySet(concept, path, rule.Value, inspector);
            }
        }
        else
        {
            FhirCaretValueWriter.TrySet(fcs, path, rule.Value, inspector);
            // Silently ignore if the path is not in the CodeSystem model.
        }
    }

    /// <summary>
    /// Expands a <see cref="CsInsertRule"/> by resolving the referenced <see cref="RuleSet"/>
    /// and replaying any applicable CS rules against the CodeSystem.
    /// </summary>
    private static void ApplyCsInsertRule(
        CsInsertRule insertRule, FhirCodeSystem fcs, CompilerContext context, CompilerOptions opts)
    {
        var resolved = RuleSetResolver.Resolve(
            insertRule.RuleSetReference, insertRule.IsParameterized, insertRule.Parameters, context);

        foreach (var rule in resolved)
        {
            switch (rule)
            {
                case Concept concept:
                    ApplyConceptRule(concept, fcs);
                    break;
                case CsCaretValueRule csCaretRule:
                    ApplyCsCaretValueRule(csCaretRule, fcs, opts.Inspector);
                    break;
                case CaretValueRule sdCaret when string.IsNullOrEmpty(sdCaret.Path):
                    var csPath = sdCaret.CaretPath.TrimStart('^');
                    FhirCaretValueWriter.TrySet(fcs, csPath, sdCaret.Value, opts.Inspector);
                    break;
                case CsInsertRule nestedInsert:
                    ApplyCsInsertRule(nestedInsert, fcs, context, opts);
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

    // ─── Shared helpers ───────────────────────────────────────────────────────

    private static ElementDefinition GetOrCreateElement(string path, StructureDefinition sd)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required", nameof(path));

        var type = sd.Type ?? string.Empty;
        var fullPath = string.IsNullOrEmpty(type) ? path : $"{type}.{path}";

        // Handle slice names: path:sliceName
        if (path.Contains(':'))
        {
            var colonIndex = path.IndexOf(':');
            var basePath = path[..colonIndex];
            var sliceName = path[(colonIndex + 1)..];
            var sliceFullPath = string.IsNullOrEmpty(type) ? basePath : $"{type}.{basePath}";
            var sliceElementId = $"{sliceFullPath}:{sliceName}";

            var sliceEd = sd.Differential.Element
                .FirstOrDefault(e => e.Path == sliceFullPath && e.SliceName == sliceName);

            if (sliceEd == null)
            {
                sliceEd = new ElementDefinition(sliceFullPath)
                {
                    Path = sliceFullPath,
                    SliceName = sliceName,
                    // C-EI1: Generate ElementDefinition.Id (path:sliceName)
                    ElementId = sliceElementId
                };
                sd.Differential.Element.Add(sliceEd);
            }
            return sliceEd;
        }

        var ed = sd.Differential.Element.FirstOrDefault(e => e.Path == fullPath && e.SliceName == null);
        if (ed == null)
        {
            // C-EI1: Generate ElementDefinition.Id equal to the full path for non-slice elements.
            ed = new ElementDefinition(fullPath) { Path = fullPath, ElementId = fullPath };
            sd.Differential.Element.Add(ed);
        }
        return ed;
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
    /// Returns <c>true</c> when <paramref name="url"/> is an absolute HTTP/HTTPS URL.
    /// </summary>
    private static bool IsAbsoluteUrl(string url) =>
        url.StartsWith("http://", StringComparison.Ordinal) ||
        url.StartsWith("https://", StringComparison.Ordinal);

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
    /// Converts a FSH <see cref="fsh_processor.Models.Instance"/> entity to a FHIR resource.
    /// Requires a version-specific <see cref="CompilerOptions.Inspector"/> to resolve the
    /// <c>InstanceOf</c> type name to a CLR type; returns <c>null</c> when the type cannot
    /// be resolved or the inspector is not supplied.
    /// </summary>
    /// <param name="forContained">
    /// When <c>true</c>, the <c>#inline</c> usage guard is skipped so that the instance can be
    /// embedded as a contained resource rather than emitted as a standalone output.
    /// </param>
    public static FhirResource? BuildInstance(
        fsh_processor.Models.Instance instance, CompilerContext context, CompilerOptions? options = null,
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
        var typeName = resolvedInstanceOf;
        var lastSlash = typeName.LastIndexOf('/');
        if (lastSlash >= 0)
            typeName = typeName[(lastSlash + 1)..];

        var classMap = inspector.FindClassMapping(typeName);
        if (classMap is null || !classMap.IsResource) return null;

        if (Activator.CreateInstance(classMap.NativeType) is not FhirResource resource)
            return null;

        // C-IN1: Set resource Id from entity name (kebab-case by convention, but use Name as-is).
        resource.Id = instance.Name;

        // C-IN2: Set meta.profile to the InstanceOf canonical URL when it looks like a URL,
        // or when a canonical base is set (so we can construct it).
        var instanceOfUrl = resolvedInstanceOf;
        if (!IsAbsoluteUrl(instanceOfUrl))
        {
            // Not a URL — try to build one with the canonical base if available.
            if (!string.IsNullOrEmpty(opts.CanonicalBase))
                instanceOfUrl = $"{opts.CanonicalBase.TrimEnd('/')}/StructureDefinition/{resolvedInstanceOf}";
            else
                instanceOfUrl = null; // can't determine URL; skip meta.profile
        }
        if (!string.IsNullOrEmpty(instanceOfUrl))
        {
            resource.Meta ??= new Meta();
            resource.Meta.Profile = [instanceOfUrl];
        }

        // C-IN3: Set Title and Description on conformance resources that support them.
        if (!string.IsNullOrEmpty(instance.Title) || !string.IsNullOrEmpty(instance.Description))
        {
            var resClassMap = inspector.FindClassMapping(resource.GetType());
            if (resClassMap != null)
            {
                if (!string.IsNullOrEmpty(instance.Title))
                    TrySetStringProperty(resource, resClassMap, "title", instance.Title);
                if (!string.IsNullOrEmpty(instance.Description))
                    TrySetStringProperty(resource, resClassMap, "description", instance.Description);
            }
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
        ModelInspector inspector)
    {
        // C-FP2: Soft-index state — maps path prefix → current resolved index.
        // The state dictionary persists across ALL rules in this entity's rule list so that
        // each [+] accumulates sequentially and [=] can reference a [+] from a previous rule.
        // [+] increments (or starts at 0) and stores the new index in state.
        // [=] reuses the stored index (defaults to 0 if no prior [+] has been seen for this prefix).
        var softIndexState = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            switch (rule)
            {
                case InstanceFixedValueRule fixedRule when
                    !string.IsNullOrEmpty(fixedRule.Path) && fixedRule.Value != null:
                    var resolvedPath = ResolveSoftIndices(fixedRule.Path, softIndexState);

                    // C-IN6: `* contained = <Name>` — embed a named Instance as a contained resource.
                    // The path base must be "contained" (with optional index, e.g. "contained[+]")
                    // and the value must be a NameValue (cross-instance reference).
                    if (fixedRule.Value is NameValue nameRef &&
                        resource is DomainResource domRes)
                    {
                        var pathBase = resolvedPath.Split('[')[0];
                        if (string.Equals(pathBase, "contained", StringComparison.Ordinal) &&
                            context.Instances.TryGetValue(nameRef.Value, out var refInstance))
                        {
                            var containedResource = BuildInstance(refInstance, context, opts, forContained: true);
                            if (containedResource != null)
                            {
                                domRes.Contained ??= [];
                                domRes.Contained.Add(containedResource);
                            }
                            break;
                        }
                    }

                    SetInstancePath(resource, resolvedPath, fixedRule.Value, inspector);
                    break;

                case InstanceInsertRule insertRule:
                    var resolved = RuleSetResolver.Resolve(
                        insertRule.RuleSetReference, insertRule.IsParameterized,
                        insertRule.Parameters, context);
                    ApplyInstanceRules(
                        resolved.OfType<InstanceRule>(), resource, context, opts, inspector);
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
    /// Sets a value on <paramref name="obj"/> by following the dot-separated
    /// <paramref name="path"/>, creating intermediate objects and list elements as needed.
    /// Returns <c>true</c> when the leaf value was set successfully.
    /// </summary>
    private static bool SetInstancePath(Base obj, string path, FshValue value, ModelInspector inspector)
    {
        var segments = SplitInstancePath(path);
        if (segments.Length == 0) return false;

        var current = obj;

        // Navigate to the parent of the leaf element.
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var (segName, segIdx) = ParseInstanceSegment(segments[i]);
            current = GetOrCreateInstanceChild(current, segName, segIdx, inspector);
            if (current is null) return false;
        }

        // Set the leaf.
        var (leafName, leafIdx) = ParseInstanceSegment(segments[segments.Length - 1]);
        return FhirCaretValueWriter.TrySetIndexed(current, leafName, leafIdx, value, inspector);
    }

    /// <summary>
    /// Navigates into (or creates) a child element of <paramref name="parent"/> by property
    /// <paramref name="name"/> at list <paramref name="index"/>.
    /// Returns <c>null</c> when the property is not found or cannot be instantiated.
    /// </summary>
    private static Base? GetOrCreateInstanceChild(Base parent, string name, int index, ModelInspector inspector)
    {
        var classMap = inspector.FindClassMapping(parent.GetType());
        if (classMap is null) return null;

        var propMap = classMap.FindMappedElementByName(name);
        if (propMap is null) return null;

        // Determine the concrete instantiable type.
        var concreteType = propMap.ImplementingType;
        if (concreteType is null || concreteType.IsAbstract) return null;

        if (propMap.IsCollection)
        {
            var list = propMap.GetValue(parent) as System.Collections.IList;
            if (list is null)
            {
                var listType = typeof(List<>).MakeGenericType(concreteType);
                list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                propMap.SetValue(parent, list);
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

    /// <summary>Splits a FHIR instance path on <c>.</c> boundaries.</summary>
    private static string[] SplitInstancePath(string path) =>
        path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses a path segment such as <c>name</c> or <c>name[2]</c> into its name and
    /// zero-based list index (defaulting to 0 when no brackets are present).
    /// </summary>
    private static (string Name, int Index) ParseInstanceSegment(string segment)
    {
        var bracketStart = segment.IndexOf('[');
        if (bracketStart < 0) return (segment, 0);

        var name = segment[..bracketStart];
        var bracketEnd = segment.IndexOf(']', bracketStart);
        var idxStr = bracketEnd > bracketStart + 1
            ? segment[(bracketStart + 1)..bracketEnd]
            : "0";
        return (name, int.TryParse(idxStr, out var idx) ? idx : 0);
    }

    // ─── Mapping compiler ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies a FSH <see cref="fsh_processor.Models.Mapping"/> entity to a target
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
        fsh_processor.Models.Mapping mapping, StructureDefinition sd, CompilerContext context)
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
                targetEd = GetOrCreateElement(mapRule.Path, sd);

            targetEd.Mapping ??= new List<ElementDefinition.MappingComponent>();
            targetEd.Mapping.Add(new ElementDefinition.MappingComponent
            {
                Identity = identity,
                Map = mapRule.Target,
                Language = mapRule.Language
            });
        }
    }

    // ─── Rule path-prefix helper (C-RL1) ────────────────────────────────────

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
}
