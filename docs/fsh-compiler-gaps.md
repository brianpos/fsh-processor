# FSH Compiler — Unimplemented Features

> **Tracking document** — lists FSH features that are parsed by `fsh-processor` into the object
> model but not yet compiled to FHIR resources by `fsh-compiler`.
>
> Updated: 2026-03-16

---

## Remaining open items

| # | Feature | Entity / Rule | Priority |
|---|---|---|---|
| 12 | Ratio / CodeableReference (version-specific DataTypes) | `FhirValueMapper` | Medium |

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

## Completed items

The following items from the original gap list have been implemented:

| # | Feature | Completed |
|---|---|---|
| 1 | Instance compilation | ✅ |
| 2 | Invariant → ConstraintComponent (Human, Expression, XPath, Severity) | ✅ |
| 3 | Mapping → StructureDefinition.mapping / ElementDefinition.mapping | ✅ |
| 4 | `pattern[x]` vs `fixed[x]` (respect `FixedValueRule.Exactly`) | ✅ |
| 5 | InsertRule expansion in ValueSet and CodeSystem (non-parameterized) | ✅ |
| 6 | `AddCRElementRule` (contentReference elements) | ✅ |
| 7 | `LrCardRule` / `LrFlagRule` in Logical/Resource entities | ✅ |
| 8 | Per-concept caret values in CodeSystem (`CsCaretValueRule.Codes`) | ✅ |
| 9 | Code-level caret/insert rules in ValueSet (`CodeCaretValueRule`, `CodeInsertRule`) | ✅ |
| 10 | `ContainsItem.NamedAlias` — `named` keyword in slicing | ✅ |
| 11 | `OnlyRule` — parse `Reference(...)`, `Canonical(...)`, `CodeableReference(...)` | ✅ |
| 12 | `RegexValue` → `FhirString` in `FhirValueMapper` | ✅ |
| 13 | Invariant severity on ObeysRule (fixed by item 2) | ✅ |
| 14 | Multi-document `Compile(IEnumerable<FshDoc>)` overload | ✅ |
| 15 | Compiler warnings (`CompilerWarning` type, warnings emitted for skipped rules) | ✅ |
| 16 | `FHIRVersion` enum completeness via `EnumUtility.ParseLiteral` | ✅ |

