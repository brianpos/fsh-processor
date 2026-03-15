# FSH Compiler Implementation Plan

> **Status**: Implementation complete. This document describes the implemented architecture.
> See the `fsh-compiler*` projects for the actual code.

## Overview

This document describes the plan and implemented design of the FSH-to-FHIR compiler that converts
the parsed FSH object model (produced by `fsh-processor`) into concrete FHIR resources.

The design uses a shared project for all compilation logic and separate thin projects for each FHIR
version, minimizing code duplication.

---

## Solution Structure

```
fsh-compiler/                      ← Shared compiler library (version-neutral)
fsh-compiler-R4/                   ← R4-specific wrapper (sets FhirVersion = "4.0.1")
fsh-compiler-R4B/                  ← R4B-specific wrapper (sets FhirVersion = "4.3.0")
fsh-compiler-R5/                   ← R5-specific wrapper (sets FhirVersion = "5.0.0")
fsh-compiler-tester-R4/           ← Unit tests targeting R4
```

All projects target `net10.0` with `ImplicitUsings`, `Nullable`, and `LangVersion=latest`.

---

## 1. `fsh-compiler` — Shared Project

### Purpose

Contains all compilation logic: type mapping, rule processing, and entity building — all using
types from `Hl7.Fhir.Conformance` (FHIR R5-based conformance resource types that are shared
across all FHIR versions via transitive dependencies).

### NuGet Dependencies

| Package | Version | Reason |
|---|---|---|
| `Hl7.Fhir.Conformance` | 5.13.2 | `StructureDefinition`, `ValueSet`, `CodeSystem`, `ElementDefinition` etc. |
| (Project reference) | — | `fsh-processor` |

### Namespace

`fsh_compiler`

### Key Types

#### `FshCompiler` (static)

The main compilation entry point. Iterates the entities in a `FshDoc` and dispatches to
per-entity builders.

| Method | Responsibility |
|---|---|
| `Compile(FshDoc, CompilerOptions?)` | Compiles all entities → `CompileResult<List<Resource>>` |
| `BuildProfile(Profile, CompilerContext, CompilerOptions?)` | Profile → StructureDefinition |
| `BuildExtension(Extension, CompilerContext, CompilerOptions?)` | Extension → StructureDefinition |
| `BuildLogical(Logical, CompilerContext, CompilerOptions?)` | Logical → StructureDefinition |
| `BuildResource(Resource, CompilerContext, CompilerOptions?)` | Resource → StructureDefinition |
| `BuildValueSet(ValueSet, CompilerContext, CompilerOptions?)` | ValueSet → FHIR ValueSet |
| `BuildCodeSystem(CodeSystem, CompilerContext, CompilerOptions?)` | CodeSystem → FHIR CodeSystem |

Rule processing is handled by private helper methods for each `FshRule` subtype
(`ApplyCardRule`, `ApplyFlagRule`, `ApplyValueSetRule`, `ApplyFixedValueRule`, `ApplyContainsRule`,
`ApplyOnlyRule`, `ApplyObeysRule`, `ApplyCaretValueRule`, `ApplyAddElementRule`, `ApplyPathRule`).

#### `CompilerOptions`

Options bag: `CanonicalBase` (URL prefix), `FhirVersion` (version string), `AliasOverrides`.

#### `CompilerContext`

Aggregates state built from one or more `FshDoc`s:
- `Dictionary<string, string> Aliases` — from `Alias` entities
- `Dictionary<string, RuleSet> RuleSets` — from `RuleSet` entities
- `ResolveAlias(string)` — returns canonical URL or input unchanged

Built via `CompilerContext.Build(FshDoc)` or extended with `MergeFrom(FshDoc)`.

#### `CompileResult<T>`

Discriminated-union result type with `SuccessResult` (contains `T Value`) and
`FailureResult` (contains `IReadOnlyList<CompilerError> Errors`).

#### `CompilerError`

Per-entity error: `EntityName`, `Message`, `SourcePosition? Position`.

#### `FhirValueMapper` (static)

Converts `FshValue` subtypes to Firely `DataType`. Handles `StringValue`, `NumberValue`,
`BooleanValue`, `DateTimeValue`, `TimeValue`, `Code`, `Quantity`, `Reference`, `Canonical`.

#### `RuleSetResolver` (static)

Resolves `InsertRule` references:
1. Looks up the named `RuleSet` in the context.
2. For non-parameterized rule sets, returns `ruleSet.Rules` directly.
3. For parameterized rule sets, performs `{paramName}` substitution on `RuleSet.UnparsedContent`,
   then re-parses via a synthetic Profile wrapper using `FshParser.Parse`.

---

## 2. `fsh-compiler-R4` — R4-Specific Project

### NuGet Dependencies

| Package | Version |
|---|---|
| `Hl7.Fhir.R4` | 5.13.2 |

Note: `Hl7.Fhir.R4` transitively depends on `Hl7.Fhir.Conformance`, so no explicit Conformance
reference is needed.

### Project References

- `fsh-compiler`

### Key Types

#### `R4FshCompiler` (static)

```csharp
public static CompileResult<List<Resource>> Compile(FshDoc doc, CompilerOptions? options = null)
```

Sets `options.FhirVersion = "4.0.1"` and delegates to `FshCompiler.Compile`.

---

## 3. `fsh-compiler-R4B` — R4B-Specific Project

Structure mirrors `fsh-compiler-R4`, referencing `Hl7.Fhir.R4B` 5.13.2 (+ explicit
`Hl7.Fhir.Conformance` 5.13.2 since R4B does not transitively depend on it).

`R4BFshCompiler.FhirVersion = "4.3.0"`.

---

## 4. `fsh-compiler-R5` — R5-Specific Project

Structure mirrors `fsh-compiler-R4`, referencing `Hl7.Fhir.R5` 5.13.2.

`R5FshCompiler.FhirVersion = "5.0.0"`.

---

## 5. `fsh-compiler-tester-R4` — Unit Test Project

### NuGet Dependencies

| Package | Version |
|---|---|
| `MSTest` | 4.0.1 |
| `Hl7.Fhir.R4` | 5.13.2 |

### Project References

- `fsh-compiler-R4`
- `fsh-processor`

### Test Classes

| Class | Coverage |
|---|---|
| `R4ProfileCompilerTests` | 26 tests: cardinality, flags, valueset binding, fixed values, only, obeys, caret-value, contains/slicing, path rules, multi-profile |
| `R4ExtensionCompilerTests` | 4 tests: metadata, parent, context, cardinality rules |
| `R4ValueSetCompilerTests` | 4 tests: metadata, include, exclude, caret-value override |
| `R4CodeSystemCompilerTests` | 4 tests: metadata, concepts, caret-value, caseSensitive |

### `CompilerTestHelper`

Shared utility class providing `CompileDoc`, `GetStructureDefinition`, `GetValueSet`,
`GetCodeSystem`, `GetElement`, `GetSliceElement`, and `LeftAlign` helpers.

---

## Refactoring Notes

### `ConvertToProfile.cs` Removal

`fsh-processor/Engine/ConvertToProfile.cs` was deleted. Its logic has been folded into
`FshCompiler.BuildProfile` in `fsh-compiler`. The `fsh-tester/FshValidator.cs` was updated to use
`FshCompiler.BuildProfile(profile, compilerContext)`.

### Alias Resolution

The existing `Dictionary<string, string>` aliasDict pattern is preserved via `CompilerContext`.

---

## Solution File

All new projects are registered in `fsh-processor.slnx` under a `/Compiler/` solution folder.

---

## Dependency Diagram

```
fsh-processor  (parsing/serialization — version-neutral)
       │
       ▼
fsh-compiler   (shared compilation logic — Hl7.Fhir.Conformance 5.13.2)
  ┌────┴───────────────┬──────────────────┐
  ▼                    ▼                  ▼
fsh-compiler-R4    fsh-compiler-R4B   fsh-compiler-R5
(Hl7.Fhir.R4)      (Hl7.Fhir.R4B +   (Hl7.Fhir.R5)
  │                 Hl7.Fhir.Conformance)
  ▼
fsh-compiler-tester-R4
```


## Overview

This document describes the plan to implement a FHIR Shorthand (FSH) compiler that converts the parsed FSH object model (produced by `fsh-processor`) into concrete FHIR resources.

The design uses a shared project for all version-neutral compilation logic and separate thin projects for each FHIR version, referencing the appropriate Firely SDK package.

---

## Solution Structure

```
fsh-compiler/                    ← Shared compiler library (version-neutral)
fsh-compiler-R4/                 ← R4-specific project
fsh-compiler-R4B/                ← R4B-specific project
fsh-compiler-R5/                 ← R5-specific project
fsh-compiler-tester-R4/         ← Unit tests targeting R4
```

All projects target `net10.0` with `ImplicitUsings`, `Nullable`, and `LangVersion=latest`.

---

## 1. `fsh-compiler` — Shared Project

### Purpose

Contains all version-neutral compilation logic: interfaces, abstract base classes, rule-processing helpers, and anything that doesn't reference a specific `Hl7.Fhir.Rn` package.

### NuGet Dependencies

| Package | Version | Reason |
|---|---|---|
| `Hl7.Fhir.Conformance` | 5.12.1 | Version-neutral conformance types (e.g. `ElementDefinition`) |
| (Project reference) | — | `fsh-processor` |

### Namespace

`fsh_compiler`

### Key Types to Create

#### `IFhirCompiler<TBundle>`

Interface that all version-specific compilers implement. The single type parameter `TBundle` is the version-specific bundle type returned after compilation.

```
IFhirCompiler<TBundle>
  CompileResult<TBundle> Compile(FshDoc doc, CompilerOptions? options)
```

#### `CompilerOptions`

Options bag (canonical base URL, version string, alias dictionary override, etc.).

#### `CompilerContext`

Aggregates state built from one or more `FshDoc`s before emitting resources:

- `Dictionary<string, string> Aliases` — collected from all `Alias` entities
- `Dictionary<string, RuleSet> RuleSets` — for `InsertRule` resolution
- Lookup helpers for cross-entity references

#### `FhirCompilerBase<TStructureDefinition, TValueSet, TCodeSystem, TBundle>` (abstract)

Abstract generic base class implementing most rule-processing logic using generic type parameters for the concrete FHIR model types. Concrete version projects override only what differs between versions.

Methods (all `protected virtual` to allow per-version overrides):

| Method | Responsibility |
|---|---|
| `BuildStructureDefinition(Profile, CompilerContext)` | Convert a `Profile` entity → StructureDefinition |
| `BuildExtensionSD(Extension, CompilerContext)` | Convert an `Extension` entity → StructureDefinition |
| `BuildLogicalSD(Logical, CompilerContext)` | Convert a `Logical` entity → StructureDefinition |
| `BuildResourceSD(Resource, CompilerContext)` | Convert a `Resource` entity → StructureDefinition |
| `BuildValueSet(ValueSet, CompilerContext)` | Convert a `ValueSet` entity → ValueSet resource |
| `BuildCodeSystem(CodeSystem, CompilerContext)` | Convert a `CodeSystem` entity → CodeSystem resource |
| `ApplyRules(FshEntity, StructureDefinition, CompilerContext)` | Dispatch each `FshRule` to a typed handler |

#### Rule handlers (one per rule type)

Each handler is a `protected virtual` method so individual FHIR versions can override specific behaviours where the model differs.

| Rule | Handler signature |
|---|---|
| `CardRule` | `ApplyCardRule(CardRule, ElementDefinition)` |
| `FlagRule` | `ApplyFlagRule(FlagRule, ElementDefinition, ...)` |
| `ValueSetRule` | `ApplyValueSetRule(ValueSetRule, ElementDefinition)` |
| `FixedValueRule` | `ApplyFixedValueRule(FixedValueRule, ElementDefinition)` |
| `ContainsRule` | `ApplyContainsRule(ContainsRule, StructureDefinition)` |
| `OnlyRule` | `ApplyOnlyRule(OnlyRule, ElementDefinition)` |
| `ObeysRule` | `ApplyObeysRule(ObeysRule, ElementDefinition)` |
| `CaretValueRule` | `ApplyCaretValueRule(CaretValueRule, ElementDefinition / StructureDefinition)` |
| `InsertRule` | `ResolveAndApplyInsertRule(InsertRule, FshEntity, CompilerContext)` |
| `AddElementRule` | `ApplyAddElementRule(AddElementRule, StructureDefinition)` |

#### `FhirValueMapper` (static helper)

Converts `FshValue` subtypes to the appropriate Firely `DataType`. Called from fixed-value and caret-value rule handlers. Concrete version projects register additional mappings for types that differ across versions (e.g. `url`, `canonical`, `integer64`).

#### `RuleSetResolver` (static helper)

Resolves `InsertRule` references:

1. Look up the named `RuleSet` in the context.
2. Perform parameter substitution on unparsed rule text.
3. Re-parse via a synthetic Profile wrapper using `FshParser.Parse`.
4. Return the resolved `List<FshRule>` for splicing.

#### `CompileResult<TBundle>`

Discriminated-union-style result type:

```
CompileResult<TBundle>
  Success  { TBundle Bundle }
  Failure  { List<CompilerError> Errors }
```

#### `CompilerError`

```
CompilerError
  string EntityName
  string Message
  SourcePosition? Position
```

---

## 2. `fsh-compiler-R4` — R4-Specific Project

### NuGet Dependencies

| Package | Version |
|---|---|
| `Hl7.Fhir.R4` | latest stable |

### Project References

- `fsh-compiler`
- `fsh-processor`

### Namespace

`fsh_compiler_r4`

### Key Types

#### `R4FhirCompiler`

Inherits `FhirCompilerBase<R4.Model.StructureDefinition, R4.Model.ValueSet, R4.Model.CodeSystem, R4.Model.Bundle>`.

Overrides (only where R4 model diverges from R5):

- Any `CaretValueRule` mappings that differ (e.g. no `integer64`)
- `FhirValueMapper` registration for R4-specific types
- `BuildBundle` to produce an R4 `Bundle`

#### `R4CompilerFactory` (static)

Convenience entry point:

```csharp
public static CompileResult<R4.Model.Bundle> Compile(FshDoc doc, CompilerOptions? options = null)
```

---

## 3. `fsh-compiler-R4B` — R4B-Specific Project

Structure mirrors `fsh-compiler-R4`, referencing `Hl7.Fhir.R4B`.

Key difference from R4: R4B introduced `Citation` and `EvidenceVariable` resources; override resource-type validation accordingly.

---

## 4. `fsh-compiler-R5` — R5-Specific Project

Structure mirrors `fsh-compiler-R4`, referencing `Hl7.Fhir.R5`.

The existing `fsh-processor/Engine/ConvertToProfile.cs` is the starting point. It will be refactored into `R5FhirCompiler` (and the shared base class), then deleted from `fsh-processor` — compilation is not the processor's responsibility.

---

## 5. `fsh-compiler-tester-R4` — Unit Test Project

### NuGet Dependencies

| Package | Version |
|---|---|
| `MSTest` | 4.0.1 |
| `Hl7.Fhir.R4` | (same as compiler project) |

### Project References

- `fsh-compiler-R4`
- `fsh-processor`

### Namespace

`fsh_compiler_tester_r4`

### Test Data

`.fsh` test files stored in `fsh-compiler-tester-R4/TestData/` and set to `CopyToOutputDirectory="PreserveNewest"` as per existing convention.

### Test Categories

| Class | Coverage |
|---|---|
| `R4ProfileCompilerTests` | `Profile` → `StructureDefinition` round-trip |
| `R4ExtensionCompilerTests` | `Extension` → `StructureDefinition` |
| `R4ValueSetCompilerTests` | `ValueSet` → `ValueSet` |
| `R4CodeSystemCompilerTests` | `CodeSystem` → `CodeSystem` |
| `R4InsertRuleTests` | `InsertRule` / RuleSet resolution |
| `R4CaretValueRuleTests` | `CaretValueRule` mapping completeness |

Each test class follows the existing `SushiTestHelper` pattern (parse FSH → compile → assert FHIR resource properties).

---

## Refactoring Notes

### Moving `ConvertToProfile.cs`

`fsh-processor/Engine/ConvertToProfile.cs` currently lives in the parser library and references `Hl7.Fhir.R5`. It should be:

1. Refactored into the shared `FhirCompilerBase` (rule-processing logic) and `R5FhirCompiler` (concrete output).
2. Deleted from `fsh-processor/Engine/` — the processor library remains version-neutral.
3. `fsh-processor.csproj` can then drop the `Hl7.Fhir.Conformance` package reference (it is only needed by the compiler layer).

### Alias Resolution

The existing approach of passing a `Dictionary<string, string>` into the engine is preserved; `CompilerContext` simply wraps it.

---

## Solution File Updates

Add the four new projects to `fsh-processor.slnx` (or rename the solution to `fsh.slnx` to reflect the expanded scope):

```xml
<Project Path="fsh-compiler/fsh-compiler.csproj" />
<Project Path="fsh-compiler-R4/fsh-compiler-R4.csproj" />
<Project Path="fsh-compiler-R4B/fsh-compiler-R4B.csproj" />
<Project Path="fsh-compiler-R5/fsh-compiler-R5.csproj" />
<Project Path="fsh-compiler-tester-R4/fsh-compiler-tester-R4.csproj" />
```

---

## Dependency Diagram

```
fsh-processor  (parsing/serialization — version-neutral)
       │
       ▼
fsh-compiler   (shared compilation logic — version-neutral)
  ┌────┴───────────┬──────────────┐
  ▼                ▼              ▼
fsh-compiler-R4  fsh-compiler-R4B  fsh-compiler-R5
  │
  ▼
fsh-compiler-tester-R4
```

---

## Implementation Order

1. Create `fsh-compiler` project with interfaces, base classes, and helpers (no FHIR-version-specific code).
2. Create `fsh-compiler-R5` first (closest to the existing `ConvertToProfile.cs`) to validate the design.
3. Create `fsh-compiler-R4` and `fsh-compiler-R4B` by subclassing the shared base and registering any version-specific overrides.
4. Create `fsh-compiler-tester-R4` and port a representative subset of existing `fsh-tester/Sushi/` tests, updated to assert on compiled FHIR resources rather than just the parsed model.
5. Delete `fsh-processor/Engine/ConvertToProfile.cs` and clean up the processor's project file.
