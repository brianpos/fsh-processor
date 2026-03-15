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

