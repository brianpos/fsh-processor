# FSH Compiler — Unimplemented Features

> **Tracking document** — lists FSH features that are parsed by `fsh-processor` into the object
> model but not yet compiled to FHIR resources by `fsh-compiler`.
>
> Updated: 2026-03-16

---

## Entity-level gaps

### 1. `Instance` → FHIR resource (any type)

**Status:** Silently skipped.

`Instance` entities are fully parsed into the object model (`Instance` class with `InstanceOf`,
`Usage`, and `InstanceFixedValueRule`/`InstanceInsertRule`/`InstancePathRule` rules) but the
compiler emits no output for them.

**Why it's hard:** The FHIR type to instantiate is determined at runtime by `InstanceOf`, which
may be any resource type (Patient, Bundle, Observation, etc.) or a profile URL. Creating a typed
instance requires knowing the concrete CLR type, which depends on the version-specific assembly.
This is best implemented in the version-specific wrappers (`R4FshCompiler` etc.) using the
`ModelInfo.GetTypeForFhirType(string)` API.

**What's needed:**
- Resolve `InstanceOf` to a CLR `Type` via `ModelInfo.GetTypeForFhirType`.
- Construct an instance of that type.
- Walk `InstanceFixedValueRule` entries: use FHIRPath-like path navigation to set nested
  properties, including array indexing (e.g. `name[0].given[0] = "John"`).
- Handle `Usage` codes (`#example`, `#definition`, `#inline`) — affects whether the instance
  is emitted as a standalone resource or embedded.
- Handle `InstanceInsertRule` (RuleSet expansion into an instance — same mechanism as SD
  InsertRule but navigating resource properties).

---

### 2. `Invariant` → `StructureDefinition.snapshot/differential constraint`

**Status:** Parsed but never compiled.

`Invariant` entities capture `description`, `expression`, `xpath`, and `severity` at the entity
level. The `ObeysRule` handler in the SD compiler correctly creates a
`ConstraintComponent` (with `Key` and `Severity`) but does **not** populate `Human`, `Expression`,
or `XPath` from the matching `Invariant` entity.

**What's needed:**
- After all entities are compiled, resolve each `ObeysRule` reference (`inv.Key`) against the
  `Invariant` entities collected in `CompilerContext`.
- Patch the generated `ConstraintComponent` with `Human = invariant.Description`,
  `Expression = invariant.Expression`, `Xpath = invariant.XPath`.
- Map `severity` string (`"error"`, `"warning"`) to `ConstraintSeverity` enum.

---

### 3. `Mapping` → `StructureDefinition.mapping` / `ElementDefinition.mapping`

**Status:** Parsed but never compiled.

`Mapping` entities (with `Source`, `Target`, `Description`, `Title`, and `MappingMapRule` rules)
have no compiler handler. FHIR `StructureDefinition` carries mappings at two levels:
`StructureDefinition.mapping[]` (the identity declaration) and
`ElementDefinition.mapping[]` (per-element map entries).

**What's needed:**
- Build `StructureDefinition.MappingComponent` entries from `Mapping` entities that reference
  a given Profile/Extension.
- Translate each `MappingMapRule` (`* path -> target language? code?`) into
  `ElementDefinition.MappingComponent` entries on the appropriate element.

---

## Rule-level gaps within implemented entities

### 4. `FixedValueRule.Exactly` → `pattern[x]` vs `fixed[x]`

**Status:** `Exactly = false` (the default) should produce `pattern[x]`; `Exactly = true` should
produce `fixed[x]`. The compiler currently **always** sets `ed.Fixed`, ignoring the `Exactly`
flag.

**What's needed:**
- When `Exactly == true`: set `ed.Fixed = value` (current behaviour — correct).
- When `Exactly == false`: set `ed.Pattern = value` instead.

---

### 5. `InsertRule` expansion in `ValueSet` and `CodeSystem`

**Status:** `VsInsertRule` and `CsInsertRule` are silently ignored (comment in compiler).

**What's needed:**
- Expand `VsInsertRule` by resolving the referenced `RuleSet`, applying parameter substitution
  (same mechanism as `RuleSetResolver.Resolve`), and re-processing the resulting `VsRule` list
  against the `FhirValueSet` under construction.
- Same for `CsInsertRule` against `FhirCodeSystem`.

---

### 6. `AddCRElementRule` (content reference element)

**Status:** Parsed into `AddCRElementRule` (with `ContentReference`, `Cardinality`, `Flags`,
`ShortDescription`, `Definition`) but not handled in `ApplySdRules` — the rule is silently
dropped (falls through the switch with no case).

**What's needed:**
- Add a case for `AddCRElementRule` in `ApplySdRules`.
- Set `ed.ContentReference`, cardinality, flags, and descriptions, analogously to `ApplyAddElementRule`.

---

### 7. `LrCardRule` and `LrFlagRule` in Logical/Resource entities

**Status:** The `ApplySdRules` switch has cases for `CardRule` and `FlagRule` (the SD versions),
but `Logical` and `Resource` entities may produce `LrCardRule` and `LrFlagRule` instead (the
LR-specific variants parsed from their grammar contexts). These are silently dropped.

**What's needed:**
- Add cases for `LrCardRule` and `LrFlagRule` in `ApplySdRules`, mapping to the same logic as
  `ApplyCardRule` / `ApplyFlagRule`.

---

### 8. `CsCaretValueRule.Codes` — per-concept caret values

**Status:** `CsCaretValueRule` has a `Codes` list for rules of the form
`* #myCode ^property = value`, which targets a specific concept rather than the CodeSystem
itself. The compiler currently calls `FhirCaretValueWriter.TrySet(fcs, ...)` regardless of
whether `Codes` is populated, setting the property on the root CodeSystem object.

**What's needed:**
- When `rule.Codes` is non-empty, locate the matching `ConceptDefinitionComponent` in
  `fcs.Concept` and set the property there instead.

---

### 9. `CodeCaretValueRule` and `CodeInsertRule` in `ValueSet`

**Status:** Parsed as `CodeCaretValueRule` / `CodeInsertRule` (rules targeting specific codes
within a ValueSet's expansion) but there are no switch cases for them in the VS rule loop —
they are silently dropped.

**What's needed:**
- `CodeCaretValueRule`: use `FhirCaretValueWriter.TrySet` on the matching
  `ConceptSetComponent.Concept[n]` entry.
- `CodeInsertRule`: expand the referenced RuleSet and apply to the code-level context.

---

### 10. `ContainsItem.NamedAlias` — `named` keyword in slicing

**Status:** `ContainsItem.NamedAlias` is captured by the parser (from `contains X named Y 0..1`)
but `ApplyContainsRule` only uses `item.Name` for the `SliceName`. The `NamedAlias` (the type
alias for the slice) is ignored.

**What's needed:**
- When `item.NamedAlias` is set, populate `ed.Type` on the slice element with the aliased type
  (after alias resolution).

---

### 11. `OnlyRule` — profile/canonical type references

**Status:** `ApplyOnlyRule` sets `ed.Type[].Code` directly from `onlyRule.TargetTypes` strings.
This works for primitive type names (`string`, `integer`) but not for:
- Canonical profile references (`Reference(http://hl7.org/fhir/StructureDefinition/Patient)`)
- Versioned canonicals (`Canonical(MyValueSet|1.0)`)
- Multiple profiles on a single type code (e.g. `Reference(Patient or Practitioner)`)

**What's needed:**
- Parse each target type string to distinguish bare type codes, `Reference(...)`,
  `CodeableReference(...)`, and `Canonical(...)`.
- Populate `TypeRefComponent.Code`, `TypeRefComponent.Profile[]`,
  and `TypeRefComponent.TargetProfile[]` correctly.
- Resolve aliases within the type expressions.

---

### 12. `FhirValueMapper` — unhandled value types

**Status:** `FhirValueMapper.ToDataType` returns `null` for:
- `Ratio` → `Hl7.Fhir.Model.Ratio`
- `CodeableReference` → `Hl7.Fhir.Model.CodeableReference` (R5-only)
- `RegexValue` → no direct FHIR DataType; used in pattern constraints

**What's needed:**
- Map `Ratio` to `Hl7.Fhir.Model.Ratio` (numerator/denominator with optional unit).
- Map `CodeableReference` (R5) conditionally — could be handled in a version-specific override.
- `RegexValue` is only valid for `pattern[x]` on string-typed elements; document as
  out-of-scope or handle as a `FhirString` with the regex pattern string.

---

### 13. `ObeysRule` — severity from linked `Invariant`

**Status:** The `ConstraintComponent` is always created with `Severity = ConstraintSeverity.Error`
regardless of the invariant's actual `Severity` field.

**What's needed:** See item 2 above — populating from the linked `Invariant` entity would fix
this as a by-product.

---

## Cross-cutting / quality gaps

### 14. Multi-document `CompilerContext` merging

**Status:** `CompilerContext.Build(FshDoc)` builds a context from one document.
`CompilerContext.MergeFrom(FshDoc)` exists but there is no public API on `FshCompiler` that
accepts multiple documents and merges them before compiling. Real IG projects span many `.fsh`
files that share aliases, rule sets, and invariants.

**What's needed:**
- A `FshCompiler.Compile(IEnumerable<FshDoc>, CompilerOptions?)` overload that builds a merged
  context, then compiles all entities from all documents within that shared context.

---

### 15. Compile error vs. warning granularity

**Status:** All exceptions during entity compilation are caught and reported as
`CompilerError` at the entity level (`EntityName`, `Message`). There is no concept of a
non-fatal warning (e.g. unresolved alias, unsupported rule silently skipped).

**What's needed:**
- A `CompilerWarning` type and corresponding `Warnings` collection on `CompileResult`.
- Emit warnings for silently-skipped rules (unknown rule types, unresolved RuleSet references,
  ignored `Exactly` flag, etc.) instead of discarding them.

---

### 16. `FHIRVersion` enum mapping completeness

**Status:** `BuildProfile` maps only `"4.0.1"`, `"4.3.0"`, and `"5.0.0"`. Other version strings
(e.g. `"4.0"`, `"3.0.2"`, `"1.4.0"`) produce `null`, leaving `sd.FhirVersion` unset.

**What's needed:**
- Extend the version switch or use `EnumUtility.ParseLiteral` against the `FHIRVersion` enum
  (which itself carries `[EnumLiteral]` attributes for all version strings).

---

## Summary table

| # | Feature | Entity / Rule | Priority |
|---|---|---|---|
| 1 | Instance compilation | `Instance` entity | High |
| 2 | Invariant → ConstraintComponent population | `Invariant` entity + `ObeysRule` | High |
| 3 | Mapping → StructureDefinition.mapping | `Mapping` entity | Medium |
| 4 | `pattern[x]` vs `fixed[x]` (Exactly flag) | `FixedValueRule` | High |
| 5 | InsertRule in ValueSet / CodeSystem | `VsInsertRule`, `CsInsertRule` | Medium |
| 6 | Content-reference element (`AddCRElementRule`) | `AddCRElementRule` | Medium |
| 7 | LR card/flag rules in Logical/Resource | `LrCardRule`, `LrFlagRule` | Medium |
| 8 | Per-concept caret values in CodeSystem | `CsCaretValueRule.Codes` | Low |
| 9 | Code-level caret/insert rules in ValueSet | `CodeCaretValueRule`, `CodeInsertRule` | Low |
| 10 | `named` alias in Contains items | `ContainsItem.NamedAlias` | Medium |
| 11 | Profile/canonical type refs in Only rule | `OnlyRule` | High |
| 12 | Ratio / CodeableReference / Regex values | `FhirValueMapper` | Medium |
| 13 | Invariant severity on ObeysRule | `ObeysRule` + `Invariant` | Low |
| 14 | Multi-document compilation | `FshCompiler.Compile` | High |
| 15 | Compiler warnings | `CompileResult` | Low |
| 16 | FHIRVersion enum completeness | `BuildProfile` | Low |
