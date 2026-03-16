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
                        // Alias, RuleSet, and Invariant produce no FHIR resource; Instance requires version-specific types
                        _ => null
                    };

                    if (resource != null)
                        resources.Add(resource);
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

        return errors.Count > 0
            ? CompileResult<List<FhirResource>>.FromFailure(errors)
            : CompileResult<List<FhirResource>>.FromSuccess(resources);
    }

    // ─── StructureDefinition builders ────────────────────────────────────────

    /// <summary>
    /// Converts a FSH <see cref="Profile"/> entity to a FHIR <see cref="StructureDefinition"/>.
    /// </summary>
    public static StructureDefinition BuildProfile(
        Profile profile, CompilerContext context, CompilerOptions? options = null)
    {
        var opts = options ?? new CompilerOptions();
        var sd = new StructureDefinition
        {
            Id = profile.Id?.Value,
            Url = ResolveUrl(profile.Id?.Value, opts),
            Name = profile.Name,
            Title = profile.Title?.Value,
            Description = profile.Description?.Value,
            Type = profile.Parent?.Value ?? "DomainResource",
            BaseDefinition = context.ResolveAlias(profile.Parent?.Value ?? string.Empty),
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

        // Ensure root element
        sd.Differential.Element.Add(new ElementDefinition(sd.Type) { Path = sd.Type });

        ApplySdRules(profile.Rules, sd, context, opts);
        return sd;
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
            Url = ResolveUrl(ext.Id, opts),
            Name = ext.Name,
            Title = ext.Title,
            Description = ext.Description,
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

        // Context
        if (ext.Contexts.Count > 0)
        {
            sd.Context = ext.Contexts
                .Select(c => new StructureDefinition.ContextComponent
                {
                    Type = StructureDefinition.ExtensionContextType.Element,
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
        var sd = new StructureDefinition
        {
            Id = logical.Id,
            Url = ResolveUrl(logical.Id, opts),
            Name = logical.Name,
            Title = logical.Title,
            Description = logical.Description,
            Type = logical.Name,
            BaseDefinition = context.ResolveAlias(logical.Parent ?? "http://hl7.org/fhir/StructureDefinition/Base"),
            Derivation = StructureDefinition.TypeDerivationRule.Specialization,
            Kind = StructureDefinition.StructureDefinitionKind.Logical,
            Differential = new StructureDefinition.DifferentialComponent
            {
                Element = new List<ElementDefinition>()
            }
        };

        sd.Differential.Element.Add(new ElementDefinition(sd.Type) { Path = sd.Type });

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
            Url = ResolveUrl(fshResource.Id, opts),
            Name = fshResource.Name,
            Title = fshResource.Title,
            Description = fshResource.Description,
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
            Url = ResolveUrl(vs.Id, opts),
            Name = vs.Name,
            Title = vs.Title,
            Description = vs.Description,
            Status = PublicationStatus.Active,
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
            Url = ResolveUrl(cs.Id, opts),
            Name = cs.Name,
            Title = cs.Title,
            Description = cs.Description,
            Status = PublicationStatus.Active,
            Content = CodeSystemContentMode.Complete,
            Concept = new List<FhirCodeSystem.ConceptDefinitionComponent>()
        };

        foreach (var rule in cs.Rules)
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

        return fcs;
    }

    // ─── SD rule processing ───────────────────────────────────────────────────

    private static void ApplySdRules(
        IEnumerable<FshRule> rules,
        StructureDefinition sd,
        CompilerContext context,
        CompilerOptions opts)
    {
        foreach (var rule in rules)
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
                    ApplyFixedValueRule(fixedValueRule, sd);
                    break;

                case ContainsRule containsRule:
                    ApplyContainsRule(containsRule, sd);
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
                        ApplySdRules(resolved, sd, context, opts);
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

    private static void ApplyFixedValueRule(FixedValueRule fixedValueRule, StructureDefinition sd)
    {
        if (string.IsNullOrEmpty(fixedValueRule.Path) || fixedValueRule.Value is null) return;
        var ed = GetOrCreateElement(fixedValueRule.Path, sd);
        var dt = FhirValueMapper.ToDataType(fixedValueRule.Value);
        if (dt != null)
        {
            // "exactly" modifier → fixed[x]; omitted → pattern[x]
            if (fixedValueRule.Exactly)
                ed.Fixed = dt;
            else
                ed.Pattern = dt;
        }
    }

    private static void ApplyContainsRule(ContainsRule containsRule, StructureDefinition sd)
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
            var sliceEd = GetOrCreateElement($"{containsRule.Path}:{item.Name}", sd);
            var parts = item.Cardinality.Split("..");
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out var min)) sliceEd.Min = min;
                sliceEd.Max = parts[1];
            }
            ApplyFlags(sliceEd, item.Flags);
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
            Value = FhirValueMapper.ToDataType(rule.Value)
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
            Value = FhirValueMapper.ToDataType(rule.Value)
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

        // Handle slice names: path:sliceName — keep as-is in ElementDefinition.Path is not right;
        // slicing uses sliceName in ElementDefinition.SliceName instead.
        if (path.Contains(':'))
        {
            var colonIndex = path.IndexOf(':');
            var basePath = path[..colonIndex];
            var sliceName = path[(colonIndex + 1)..];
            var sliceFullPath = string.IsNullOrEmpty(type) ? basePath : $"{type}.{basePath}";

            var sliceEd = sd.Differential.Element
                .FirstOrDefault(e => e.Path == sliceFullPath && e.SliceName == sliceName);

            if (sliceEd == null)
            {
                sliceEd = new ElementDefinition(sliceFullPath)
                {
                    Path = sliceFullPath,
                    SliceName = sliceName
                };
                sd.Differential.Element.Add(sliceEd);
            }
            return sliceEd;
        }

        var ed = sd.Differential.Element.FirstOrDefault(e => e.Path == fullPath && e.SliceName == null);
        if (ed == null)
        {
            ed = new ElementDefinition(fullPath) { Path = fullPath };
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

    private static string? ResolveUrl(string? idOrName, CompilerOptions opts)
    {
        if (string.IsNullOrEmpty(idOrName)) return null;
        if (idOrName.StartsWith("http://") || idOrName.StartsWith("https://")) return idOrName;
        if (string.IsNullOrEmpty(opts.CanonicalBase)) return idOrName;
        return $"{opts.CanonicalBase.TrimEnd('/')}/{idOrName}";
    }
}
