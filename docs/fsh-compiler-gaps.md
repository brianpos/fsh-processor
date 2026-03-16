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

### 10. `ContainsItem.NamedAlias` — `named` keyword in slicing

**Status:** `ContainsItem.NamedAlias` is captured by the parser (from `contains X named Y 0..1`)
but `ApplyContainsRule` only uses `item.Name` for the `SliceName`. The `NamedAlias` (the type
alias for the slice) is ignored.

**What's needed:**
- When `item.NamedAlias` is set, populate `ed.Type` on the slice element with the aliased type
  (after alias resolution).

---

### 12. `Ratio` value type in `FhirValueMapper`

**Status:** `Ratio` → `Hl7.Fhir.Model.Ratio` mapping is not implemented because
`Hl7.Fhir.Model.Ratio` is not available in the `Hl7.Fhir.Conformance` assembly used by the
base `fsh-compiler` project.

**What's needed:**
- Implement `Ratio` → `Hl7.Fhir.Model.Ratio` mapping in the version-specific wrappers
  (R4, R4B, R5) via a `FhirValueMapper` override or by supplying a custom value converter
  through `CompilerOptions`.
- `CodeableReference` (R5-only) has the same constraint — must be handled version-specifically.

---

## Cross-cutting / quality gaps

### 15. Compile error vs. warning granularity

**Status:** All exceptions during entity compilation are caught and reported as
`CompilerError` at the entity level (`EntityName`, `Message`). There is no concept of a
non-fatal warning (e.g. unresolved alias, unsupported rule silently skipped).

**What's needed:**
- A `CompilerWarning` type and corresponding `Warnings` collection on `CompileResult`.
- Emit warnings for silently-skipped rules (unknown rule types, unresolved RuleSet references,
  ignored `Exactly` flag, etc.) instead of discarding them.

---

## Completed items

The following items from the original gap list have been implemented:

| # | Feature | Completed |
|---|---|---|
| 2 | Invariant → ConstraintComponent (Human, Expression, XPath, Severity) | ✅ |
| 4 | `pattern[x]` vs `fixed[x]` (respect `FixedValueRule.Exactly`) | ✅ |
| 5 | InsertRule expansion in ValueSet and CodeSystem (non-parameterized) | ✅ |
| 6 | `AddCRElementRule` (contentReference elements) | ✅ |
| 7 | `LrCardRule` / `LrFlagRule` in Logical/Resource entities | ✅ |
| 8 | Per-concept caret values in CodeSystem (`CsCaretValueRule.Codes`) | ✅ |
| 11 | `OnlyRule` — parse `Reference(...)`, `Canonical(...)`, `CodeableReference(...)` | ✅ |
| 12 | `RegexValue` → `FhirString` in `FhirValueMapper` | ✅ |
| 13 | Invariant severity on ObeysRule (fixed by item 2) | ✅ |
| 14 | Multi-document `Compile(IEnumerable<FshDoc>)` overload | ✅ |
| 16 | `FHIRVersion` enum completeness via `EnumUtility.ParseLiteral` | ✅ |

---

## Remaining open items

| # | Feature | Entity / Rule | Priority |
|---|---|---|---|
| 1 | Instance compilation | `Instance` entity | High |
| 3 | Mapping → StructureDefinition.mapping | `Mapping` entity | Medium |
| 9 | Code-level caret/insert rules in ValueSet | `CodeCaretValueRule`, `CodeInsertRule` | Low |
| 10 | `named` alias in Contains items | `ContainsItem.NamedAlias` | Medium |
| 12 | Ratio / CodeableReference (version-specific DataTypes) | `FhirValueMapper` | Medium |
| 15 | Compiler warnings vs errors | `CompileResult` | Low |
