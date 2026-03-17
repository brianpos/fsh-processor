# FSH Specification Gaps — Working Document

> **Purpose** — comprehensive comparison of the FHIR Shorthand specification
> (`docs/language-reference.md`) against the current implementation in
> `fsh-processor` (parser/model) and `fsh-compiler` (FHIR resource builder).
> The SDC IG sushi-generated output (`fsh-compiler-tester-R4/TestData/sushi-generated/`)
> is used as the reference for expected output fidelity.
>
> Updated: 2026-03-17

---

## Status Legend

| Symbol | Meaning |
|--------|---------|
| ❌ | Open — not yet implemented |
| 🔄 | In progress |
| ✅ | Resolved |

---

## Cross-cutting Behavioural Differences (Parser vs. SUSHI)

Known parser-level design choices that differ from SUSHI. These are documented in
`docs/sushi-test-mapping.md` under "Cross-cutting behavioural differences". They affect
many `Assert.Inconclusive` tests in `fsh-tester/Sushi/`.

| ID | Status | Description | Affected tests |
|----|--------|-------------|----------------|
| X1 | ❌ | **No semantic validation** — SUSHI reports errors for duplicates, missing required fields, invalid codes, etc. `fsh-processor` is a pure syntax parser and emits no semantic errors. | 66 tests `Inconclusive` |
| X2 | ❌ | **Single-file only** — SUSHI cross-file alias sharing, multi-file duplicate detection, etc. are not supported. | All "…AcrossFiles" tests `Inconclusive` |
| X3 | ❌ | **Last-wins vs first-wins for duplicate metadata** — SUSHI uses first-wins; `fsh-processor` uses last-wins. | Several metadata tests `Inconclusive` |
| X4 | ❌ | **`CardRule` + `FlagRule` not split** — SUSHI splits `0..1 MS` into a `CardRule` and a separate `FlagRule`; `fsh-processor` keeps them combined. | Several card+flag tests `Inconclusive` |
| X5 | ❌ | **Multi-invariant `ObeysRule` not split** — SUSHI emits one `ObeysRule` per invariant; `fsh-processor` keeps all invariants in a single rule. | Several obeys tests `Inconclusive` |
| X6 | ❌ | **`ContainsRule` + `CardRule` not split** — SUSHI emits a `ContainsRule` then per-item `CardRule`s; `fsh-processor` combines them. | Several contains tests `Inconclusive` |

---

## Section: Alias

### Parser
No parser gaps beyond X1/X2/X3.

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-A1 | ❌ | `AliasOverrides` in `CompilerOptions` must be explicitly populated by callers; `sushi-config.yaml` canonical base and aliases are not auto-loaded. |

---

## Section: Profile

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-PR1 | ❌ | Profile `Id` is not defaulted to the kebab-case entity name when omitted. SUSHI applies this default. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-PR1 | ✅ | `StructureDefinition.Status` not set. ValueSet/CodeSystem set `PublicationStatus.Active`; Profile/Extension/Logical/Resource do not. |
| C-PR2 | ✅ | `StructureDefinition.Abstract` not set. Sushi always emits `"abstract": false`. |
| C-PR3 | ✅ | `StructureDefinition.Kind` not set for profiles. Should be `resource`, `complex-type`, or `primitive-type` depending on the parent. |
| C-PR4 | ❌ | `StructureDefinition.Type` defaults to `"DomainResource"` when no parent is given; should be the normalised parent type name. |
| C-PR5 | ❌ | `StructureDefinition.FhirVersion` requires `CompilerOptions.FhirVersion` to be set externally; not inferred from `sushi-config.yaml`. |

---

## Section: Extension

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-EX1 | ❌ | Extension `Parent` not defaulted to `"Extension"` when omitted. Inconclusive test: *"Parser does not yet default Extension.Parent to 'Extension'"*. |
| P-EX2 | ❌ | Extension `Id` not defaulted to the kebab-case entity name. (Same test.) |
| P-EX3 | ❌ | `Context` model has no `Type` property; cannot distinguish `fhirpath` (quoted), `element` (unquoted name/id/url), and `extension` (extension URL) context types. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-EX1 | ✅ | All contexts compiled as `ExtensionContextType.Element`. A quoted context string should map to `ExtensionContextType.FhirPath`; an extension-URL context should map to `ExtensionContextType.Extension`. |
| C-EX2 | ✅ | `StructureDefinition.Status` not set (same as C-PR1). |
| C-EX3 | ✅ | `StructureDefinition.Abstract` not set (same as C-PR2). |

---

## Section: Logical

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-LG1 | ✅ | `CaretValueRule` on Logical entities not supported by the visitor. Inconclusive test: *"Parser does not yet support CaretValueRule on Logical entities"*. |
| P-LG2 | ✅ | `InsertRule` on Logical entities not supported by the visitor. Inconclusive test: *"Parser does not yet support InsertRule on Logical entities"*. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-LG1 | ✅ | `Logical.Characteristics` is parsed but not compiled. Per spec, characteristics SHALL be emitted as the `structuredefinition-type-characteristics` extension (`http://hl7.org/fhir/tools/StructureDefinition/type-characteristics`). |
| C-LG2 | ✅ | `StructureDefinition.Status` not set (same as C-PR1). |
| C-LG3 | ✅ | `StructureDefinition.Abstract` not set (same as C-PR2). |

---

## Section: Resource

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-RS1 | ✅ | `CaretValueRule` on Resource entities not supported. Inconclusive test: *"Parser does not yet support CaretValueRule on Resource entities"*. |
| P-RS2 | ✅ | `InsertRule` on Resource entities not supported. Inconclusive test: *"Parser does not yet support InsertRule on Resource entities"*. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-RS1 | ✅ | `StructureDefinition.Status` not set (same as C-PR1). |
| C-RS2 | ✅ | `StructureDefinition.Abstract` not set (same as C-PR2). |

---

## Section: Instance

### Parser
No parser-layer gaps identified.

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-IN1 | ✅ | Instance entity `Name` (defaulted to kebab-case `Id`) not applied to `FhirResource.Id`. Sushi sets `resource.id` from the entity name. |
| C-IN2 | ✅ | `InstanceOf` value not written to `resource.Meta.Profile` for conformance instances. Sushi sets `meta.profile` to the full canonical URL of the profile. |
| C-IN3 | ❌ | Instance `Title` and `Description` metadata keywords unused at compile time. |
| C-IN4 | ✅ | Instance `Usage` (`#example`, `#definition`, `#inline`) ignored. Sushi uses this to control standalone resource emission. |
| C-IN5 | ❌ | Soft-index expansion (`[+]`/`[=]`) not implemented for instance assignment paths. Large instances (Bundle, CapabilityStatement) rely on soft indexing. |
| C-IN6 | ❌ | Indented rule path composition not implemented for instance rules. `Indent` is stored but never used to expand relative paths. |

---

## Section: Invariant

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-IV1 | ❌ | Multiline expressions (`MULTILINE_STRING`) not supported in `Invariant.Expression`. Grammar uses `KW_EXPRESSION STRING` only. Inconclusive: *"multiline Expression not supported by the parser grammar"*. |

### Compiler
No additional compiler gaps beyond the parser.

---

## Section: ValueSet

### Parser
No parser gaps beyond X1/X2/X3.

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-VS1 | ✅ | `ValueSet.Experimental` not set. Sushi emits `"experimental": false` by default. |
| C-VS2 | ❌ | `ValueSet.Id` not defaulted to the kebab-case entity name when omitted. |
| C-VS3 | ✅ | `ValueSet.Url` generated as `{canonicalBase}/{id}` — missing the `/ValueSet/` path segment. Sushi produces `{canonicalBase}/ValueSet/{id}`. |

---

## Section: CodeSystem

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-CS1 | ❌ | `CodeSystem.Id` not defaulted to entity name. Inconclusive: *"Parser does not yet default CodeSystem.Id to the entity name"*. |
| P-CS2 | ❌ | Leading newline not trimmed for triple-quoted multiline strings that start on a new line. Inconclusive: *"Parser does not yet trim leading newline from triple-quoted multiline strings that start on a new line"*. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-CS1 | ✅ | `CodeSystem.Count` not computed. Sushi counts total concepts and sets `count`. |
| C-CS2 | ✅ | `CodeSystem.Experimental` not set. |
| C-CS3 | ✅ | `CodeSystem.Url` generated without the `/CodeSystem/` segment. Sushi produces `{canonicalBase}/CodeSystem/{id}`. |

---

## Section: RuleSet / ParamRuleSet

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-RST1 | ❌ | Parameterized insert with empty parameter slots and multi-word unquoted parameters may not parse identically to SUSHI. Inconclusive: *"parameterized insert with empty param slots and multi-word unquoted params may not parse identically to SUSHI"*. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-RL1 | ✅ | Insert rules with a **path context** (`* <element> insert RuleSet`) do not apply the path prefix to resolved rules. When `InsertRule.Path` is non-empty, every resolved rule's path should have `{insertRule.Path}.` prepended. Affects all entity types with path-context insert rules. |

---

## Section: Mapping

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-MP1 | ❌ | Multiline comments (`MULTILINE_STRING`) not supported in `mappingRule` comments. Inconclusive: two mapping tests. |

### Compiler
No additional compiler gaps beyond the parser.

---

## Section: FSH Paths — Indented Rules & Soft Indexing

These are cross-cutting features affecting all entity/rule types.

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-FP1 | ❌ | **Indented rule path composition** — `Indent` whitespace is preserved but paths are NOT composed from indentation. SUSHI expands `* name\n  * family 1..1` into `* name.family 1..1` at import time. `fsh-processor` stores them as separate rules with relative paths. Inconclusive: *"path composition via indented path rules not implemented in parser"*. |
| P-FP2 | ❌ | **Soft-index expansion** (`[+]`/`[=]`) — indices are stored as-is in path strings; not resolved to numeric indices. Inconclusive: *"soft-index expansion not implemented in parser"*. |
| P-FP3 | ❌ | **Context-path `[]` syntax** — FSHImporter.Context tests from SUSHI entirely unported. Tests cover `entry[ResourceA]` type-disambiguation syntax in paths. The grammar captures these in `SEQUENCE` tokens but the visitor does not model them distinctly. |

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-FP1 | ❌ | Indented rules are not path-composed in the compiler. Since the parser stores relative paths, the compiler receives rules with incomplete paths and cannot expand them without the indent context. |
| C-FP2 | ❌ | Soft-index expansion not implemented. Paths like `item[+].linkId` are passed to `SetInstancePath` / `GetOrCreateElement` as-is rather than being resolved to `item[0].linkId`. |

---

## Section: FSH Rules — Assignment Rules

### Parser
| ID | Status | Description |
|----|--------|-------------|
| P-AR1 | ❌ | **`BooleanValue` not emitted for `true`/`false` in `fixedValueRule`**. The `name()` branch in `VisitValue` wins before the `@bool()` branch, so `true`/`false` become `NameValue` instead of `BooleanValue`. Affects at least 4 tests (Extension, SDRules, CodeSystem, ValueSet). |

---

## Section: ElementDefinition IDs

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-EI1 | ✅ | **`ElementDefinition.Id` not generated**. FHIR requires a logically-derived `id` on every differential element (e.g., `Extension.extension:name.value[x]`). `GetOrCreateElement` creates `ElementDefinition` objects without `id`. Required for correct snapshot generation and IG Publisher validation. |

---

## Section: Canonical URL Construction

### Compiler
| ID | Status | Description |
|----|--------|-------------|
| C-CU1 | ❌ | `ResolveUrl(idOrName, opts)` generates `{canonicalBase}/{idOrName}` for all resource types. Sushi uses resource-type-specific segments: `{base}/StructureDefinition/{id}`, `{base}/ValueSet/{id}`, `{base}/CodeSystem/{id}`. All generated URLs are wrong when `CanonicalBase` is supplied. |

---

## Section: Output Testing (SDC IG Sushi Comparison)

`SdcIgCompilerTests.cs` has six tests; each has a `TODO` comment noting sushi comparison is unimplemented.

| ID | Status | Test | Description |
|----|--------|------|-------------|
| T1 | ❌ | `ShouldCompileAllSdcIgFilesToFhirResources` | Compile error count is informational only. Should become `Assert.AreEqual(0, compileErrors.Count)` once all gaps are resolved. |
| T2 | ❌ | `ShouldSerializeCompiledResourcesToValidFhirJson` | Sushi JSON comparison noted as a TODO in the test body. |
| T3 | ❌ | `ShouldGenerateSnapshotsForStructureDefinitions` | Snapshot may differ when `specification.zip` absent. |
| T4 | ❌ | `ShouldProduceExpectedResourceTypeCounts` | Sushi resource counts should be confirmed and used as assertions. |
| T5 | ❌ | `ShouldValidateResourceMetadata` | `Id`/`Url` population assertion is informational pending C-IN1 fix. |
| T6 | ❌ | `ShouldWriteCompiledResourcesToDiskForManualComparison` | Writes JSON to disk but no automated diff against `TestData/sushi-generated/`. |

---

## Priority Order for Implementation

### High Priority (required for basic parity with sushi)
1. **C-CU1** — Canonical URL path segments (`/StructureDefinition/`, `/ValueSet/`, `/CodeSystem/`)
2. **C-PR1 / C-EX2 / C-LG2 / C-RS1** — `StructureDefinition.Status` set to `active`
3. **C-PR2 / C-EX3 / C-LG3 / C-RS2** — `StructureDefinition.Abstract` set to `false`
4. **C-IN1** — Instance `Id` populated from entity name
5. **C-IN2** — Instance `meta.profile` set from `InstanceOf`
6. **C-EI1** — `ElementDefinition.Id` generation
7. **P-AR1** — `BooleanValue` for `true`/`false` in `fixedValueRule`
8. **P-LG1 / P-LG2 / P-RS1 / P-RS2** — `CaretValueRule` + `InsertRule` on Logical/Resource entities

### Medium Priority
9. **C-RL1** — Path-context insert rules prepend the path prefix
10. **C-EX1** — Extension context type (`fhirpath` vs `element` vs `extension`)
11. **C-LG1** — Logical `Characteristics` → `type-characteristics` extension
12. **C-VS3 / C-CS3** — URL path segments for ValueSet and CodeSystem
13. **C-IN4** — Instance `Usage` flag respects `#inline` skip
14. **P-FP1** — Indented rule path composition
15. **P-FP2** — Soft-index expansion

### Lower Priority
16. **C-IN5 / C-FP2** — Soft-index expansion in instance paths
17. **P-CS2** — Multiline string leading-newline trim
18. **C-CS1** — `CodeSystem.Count`
19. **C-VS1 / C-CS2** — `Experimental` flag
20. **T1–T6** — Sushi output comparison tests
21. **X1–X6** — Cross-cutting behavioural differences (semantic validation, multi-file, etc.)

---

## Completed Items

| ID | Description | Completed |
|----|-------------|-----------|
| C-CU1 | Canonical URL path segments (`/StructureDefinition/`, `/ValueSet/`, `/CodeSystem/`) | ✅ |
| C-PR1/C-EX2/C-LG2/C-RS1 | `StructureDefinition.Status = active` on all SD builders | ✅ |
| C-PR2/C-EX3/C-LG3/C-RS2 | `StructureDefinition.Abstract = false` on all SD builders | ✅ |
| C-VS1/C-CS2 | `Experimental = false` on ValueSet and CodeSystem | ✅ |
| C-VS3/C-CS3 | `/ValueSet/` and `/CodeSystem/` URL path segments | ✅ (part of C-CU1) |
| C-CS1 | `CodeSystem.Count` computed from total concepts | ✅ |
| C-EX1 | Extension context type: quoted→`Fhirpath`, unquoted→`Element` | ✅ |
| C-LG1 | Logical `Characteristics` → `type-characteristics` extension | ✅ |
| C-IN1 | Instance `Id` populated from entity `Name` | ✅ |
| C-IN2 | Instance `meta.profile` set from `InstanceOf` canonical URL | ✅ |
| C-IN4 | `#inline` instances not emitted as standalone resources | ✅ |
| C-RL1 | Path-context insert rules prepend path prefix to resolved rules | ✅ |
| C-EI1 | `ElementDefinition.Id` generated for slice elements | ✅ (non-root only) |
| P-LG1 | `CaretValueRule` on Logical entities (grammar already supported; tests fixed) | ✅ |
| P-LG2 | `InsertRule` on Logical entities (grammar already supported; tests fixed) | ✅ |
| P-RS1 | `CaretValueRule` on Resource entities (grammar already supported; tests fixed) | ✅ |
| P-RS2 | `InsertRule` on Resource entities (grammar already supported; tests fixed) | ✅ |
