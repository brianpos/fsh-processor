# FSH Compiler — Unimplemented Features

> **Tracking document** — lists FSH features that are parsed by `fsh-processor` into the object
> model but not yet compiled to FHIR resources by `fsh-compiler`.
>
> Updated: 2026-03-16

---

## Remaining open items

*All tracked compiler gaps have been resolved.*

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
| 12 | `Ratio` → version-specific `Hl7.Fhir.Model.Ratio` via `ModelInspector` in `FhirValueMapper` | ✅ |
| 13 | Invariant severity on ObeysRule (fixed by item 2) | ✅ |
| 14 | Multi-document `Compile(IEnumerable<FshDoc>)` overload | ✅ |
| 15 | Compiler warnings (`CompilerWarning` type, warnings emitted for skipped rules) | ✅ |
| 16 | `FHIRVersion` enum completeness via `EnumUtility.ParseLiteral` | ✅ |

