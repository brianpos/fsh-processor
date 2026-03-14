using fsh_processor.Models;
using Hl7.Fhir.Model;
using FhirCode = Hl7.Fhir.Model.Code;
using FhirExtension = Hl7.Fhir.Model.Extension;

namespace fsh_processor.Engine
{
    public class ConvertToProfile
    {
        public static StructureDefinition Convert(Profile profile, Dictionary<string, string> aliasDict)
        {
            var sd = new StructureDefinition
            {
                Id = profile.Id?.Value,
                Url = profile.Id?.Value,
                Name = profile.Name,
                Title = profile.Title?.Value,
                Description = profile.Description?.Value,
                Type = profile.Parent?.Value ?? "DomainResource",
                BaseDefinition = profile.Parent?.Value,
                Derivation = StructureDefinition.TypeDerivationRule.Constraint,
                Differential = new StructureDefinition.DifferentialComponent
                {
                    Element = new List<ElementDefinition>()
                }
            };

            // Ensure root element exists for caret rules on '.'
            sd.Differential.Element.Add(new ElementDefinition(profile.Parent?.Value ?? sd.Type)
            {
                Path = profile.Parent?.Value ?? sd.Type
            });

            ElementDefinition GetOrCreateElement(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("Path is required", nameof(path));

                // FSH paths do not include the resource name, but ElementDefinition.path must.
                var fullPath = string.IsNullOrEmpty(sd.Type) ? path : $"{sd.Type}.{path}";
                var ed = sd.Differential.Element.FirstOrDefault(e => e.Path == fullPath);
                if (ed == null)
                {
                    ed = new ElementDefinition(fullPath) { Path = fullPath };
                    sd.Differential.Element.Add(ed);
                }
                return ed;
            }

            // Helper to set flags: MS, SU, N, TU, D
            void ApplyFlags(ElementDefinition ed, IEnumerable<string> flags)
            {
                foreach (var f in flags)
                {
                    switch (f)
                    {
                        case "MS":
                            ed.MustSupport = true;
                            break;
                        case "SU":
                            ed.IsSummary = true;
                            break;
                        case "N":
                            // ed.IsModifier = false; // normative? No direct mapping; best-effort noop
                            break;
                        case "TU":
                            // Trial Use flag doesn't have direct ElementDefinition mapping; noop
                            break;
                        case "D":
                            // Draft flag - no direct ED mapping; noop
                            break;
                        case "?!":
                            ed.IsModifier = true;
                            break;
                        default:
                            break;
                    }
                }
            }

            foreach (var rule in profile.Rules)
            {
                // Process each rule and modify the StructureDefinition accordingly
                // This is a placeholder for actual rule processing logic
                if (rule != null)
                {
                    switch (rule)
                    {
                        // cardRule
                        case CardRule cardRule:
                            if (!string.IsNullOrEmpty(cardRule.Path))
                            {
                                var ed = GetOrCreateElement(cardRule.Path);
                                // Cardinality like "0..1", "1..*"
                                var parts = cardRule.Cardinality.Split("..");
                                if (parts.Length == 2)
                                {
                                    if (int.TryParse(parts[0], out var min)) ed.Min = min;
                                    ed.Max = parts[1];
                                }
                                ApplyFlags(ed, cardRule.Flags);
                            }
                            break;

                        // flagRule
                        case FlagRule flagRule:
                            if (!string.IsNullOrEmpty(flagRule.Path))
                            {
                                var ed = GetOrCreateElement(flagRule.Path);
                                ApplyFlags(ed, flagRule.Flags);
                            }
                            if (flagRule.AdditionalPaths != null)
                            {
                                foreach (var ap in flagRule.AdditionalPaths)
                                {
                                    var ed2 = GetOrCreateElement(ap);
                                    ApplyFlags(ed2, flagRule.Flags);
                                }
                            }
                            break;

                        // valueSetRule
                        case ValueSetRule valueSetRule:
                            if (!string.IsNullOrEmpty(valueSetRule.Path) && !string.IsNullOrEmpty(valueSetRule.ValueSetName))
                            {
                                var ed = GetOrCreateElement(valueSetRule.Path);
                                ed.Binding = new ElementDefinition.ElementDefinitionBindingComponent
                                {
                                    Strength = valueSetRule.Strength switch
                                    {
                                        "(example)" => BindingStrength.Example,
                                        "(preferred)" => BindingStrength.Preferred,
                                        "(extensible)" => BindingStrength.Extensible,
                                        "(required)" => BindingStrength.Required,
                                        _ => BindingStrength.Preferred
                                    },
                                    ValueSet = valueSetRule.ValueSetName
                                };
                            }
                            break;

                        // fixedValueRule
                        case FixedValueRule fixedValueRule:
                            if (!string.IsNullOrEmpty(fixedValueRule.Path) && fixedValueRule.Value != null)
                            {
                                var ed = GetOrCreateElement(fixedValueRule.Path);
                                // Map basic value types to FHIR ElementDefinition.fixed[x]
                                DataType? dt = null;
                                switch (fixedValueRule.Value)
                                {
                                    case StringValue sv:
                                        dt = new FhirString(sv.Value);
                                        break;
                                    case NumberValue nv:
                                        dt = new FhirDecimal(nv.Value);
                                        break;
                                    case BooleanValue bv:
                                        dt = new FhirBoolean(bv.Value);
                                        break;
                                    case fsh_processor.Models.Code c:
                                        dt = new FhirCode(c.Value);
                                        break;
                                    default:
                                        break;
                                }
                                if (dt != null)
                                {
                                    ed.Fixed = dt;
                                    if (fixedValueRule.Exactly)
                                    {
                                        // no direct exactly flag; using pattern would be weaker. Leave as fixed.
                                    }
                                }
                            }
                            break;

                        // containsRule
                        case ContainsRule containsRule:
                            // Slicing declaration minimal mapping
                            if (!string.IsNullOrEmpty(containsRule.Path) && containsRule.Items?.Count > 0)
                            {
                                var ed = GetOrCreateElement(containsRule.Path);
                                if (ed.Slicing == null)
                                {
                                    ed.Slicing = new ElementDefinition.SlicingComponent
                                    {
                                        Rules = ElementDefinition.SlicingRules.Open,
                                        Ordered = false,
                                        Discriminator = new List<ElementDefinition.DiscriminatorComponent>()
                                    };
                                }
                                foreach (var item in containsRule.Items)
                                {
                                    var sliceEd = GetOrCreateElement($"{containsRule.Path}:{item.Name}");
                                    // Cardinality for slice
                                    var parts = item.Cardinality.Split("..");
                                    if (parts.Length == 2)
                                    {
                                        if (int.TryParse(parts[0], out var min)) sliceEd.Min = min;
                                        sliceEd.Max = parts[1];
                                    }
                                    ApplyFlags(sliceEd, item.Flags);
                                }
                            }
                            break;

                        // onlyRule
                        case OnlyRule onlyRule:
                            if (!string.IsNullOrEmpty(onlyRule.Path) && onlyRule.TargetTypes?.Count > 0)
                            {
                                var ed = GetOrCreateElement(onlyRule.Path);
                                ed.Type = onlyRule.TargetTypes.Select(tt => new ElementDefinition.TypeRefComponent
                                {
                                    Code = tt
                                }).ToList();
                            }
                            break;

                        // obeysRule
                        case ObeysRule obeysRule:
                            if (obeysRule.InvariantNames != null && obeysRule.InvariantNames.Count > 0)
                            {
                                if (!string.IsNullOrEmpty(obeysRule.Path))
                                {
                                    var ed = GetOrCreateElement(obeysRule.Path);
                                    if (ed.Constraint == null) ed.Constraint = new List<ElementDefinition.ConstraintComponent>();
                                    foreach (var inv in obeysRule.InvariantNames)
                                    {
                                        ed.Constraint.Add(new ElementDefinition.ConstraintComponent
                                        {
                                            Key = inv,
                                            Severity = ConstraintSeverity.Warning
                                        });
                                    }
                                }
                                else
                                {
                                    // Profile-level invariants would be added to root ED
                                    var root = sd.Differential.Element.First();
                                    if (root.Constraint == null) root.Constraint = new List<ElementDefinition.ConstraintComponent>();
                                    foreach (var inv in obeysRule.InvariantNames)
                                    {
                                        root.Constraint.Add(new ElementDefinition.ConstraintComponent
                                        {
                                            Key = inv,
                                            Severity = ConstraintSeverity.Warning
                                        });
                                    }
                                }
                            }
                            break;

                        // caretValueRule
                        case CaretValueRule caretValueRule:
                            if (!string.IsNullOrEmpty(caretValueRule.CaretPath) && caretValueRule.Value != null)
                            {
                                // If path is '.', apply to root ElementDefinition, else to targeted ED metadata via extension
                                ElementDefinition targetEd;
                                if (string.IsNullOrEmpty(caretValueRule.Path) || caretValueRule.Path == ".")
                                {
                                    targetEd = sd.Differential.Element.First();
                                }
                                else
                                {
                                    targetEd = GetOrCreateElement(caretValueRule.Path);
                                }

                                // Map a few common caret paths
                                switch (caretValueRule.CaretPath)
                                {
                                    case "^short":
                                        if (caretValueRule.Value is StringValue sv1) targetEd.Short = sv1.Value;
                                        break;
                                    case "^definition":
                                        if (caretValueRule.Value is StringValue sv2) targetEd.Definition = sv2.Value;
                                        break;
                                    case "^comment":
                                        if (caretValueRule.Value is StringValue sv3) targetEd.Comment = sv3.Value;
                                        break;
                                    case "^requirements":
                                        if (caretValueRule.Value is StringValue sv4) targetEd.Requirements = sv4.Value;
                                        break;
                                    default:
                                        // For unmapped caret paths, store in extension to preserve data
                                        if (targetEd.Extension == null) targetEd.Extension = new List<FhirExtension>();
                                        targetEd.Extension.Add(new FhirExtension
                                        {
                                            Url = caretValueRule.CaretPath.TrimStart('^'),
                                            Value = caretValueRule.Value is StringValue svx ? new FhirString(svx.Value) : null
                                        });
                                        break;
                                }
                            }
                            break;

                        // insertRule
                        case InsertRule insertRule:
                            // RuleSet inserts affect authoring-time; no direct StructureDefinition change here.
                            break;

                        // pathRule
                        case PathRule pathRule:
                            if (!string.IsNullOrEmpty(pathRule.Path))
                            {
                                GetOrCreateElement(pathRule.Path);
                            }
                            break;
                    }
                }
            }

            return sd;
        }
    }
}
