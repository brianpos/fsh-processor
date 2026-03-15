# Copilot Instructions for fsh-processor

## Project Overview

This is a .NET library for parsing, validating, and serializing [FHIR Shorthand (FSH)](https://build.fhir.org/ig/HL7/fhir-shorthand/) files. It uses an ANTLR4 grammar to produce a strongly-typed object model that supports round-trip serialization (preserving comments and whitespace).

**License:** BSD 3-Clause

## Solution Structure

- **fsh-processor** — Core library: ANTLR grammar/generated code, parser, serializer, object model, and conversion engine.
  - `antlr/` — ANTLR4-generated lexer/parser/visitor/listener (do **not** hand-edit these files).
  - `Models/` — Strongly-typed FSH object model (`FshDoc`, `FshEntity` subclasses, `FshRule` subclasses, `FshNode` base).
  - `Visitors/` — `FshModelVisitor` builds the object model from the ANTLR parse tree.
  - `Engine/` — Conversion logic (e.g. `ConvertToProfile` converts `Profile` → FHIR `StructureDefinition`).
  - `FshParser.cs` — Public entry point: `FshParser.Parse(string) → ParseResult`.
  - `FshSerializer.cs` — Public entry point: `FshSerializer.Serialize(FshDoc) → string`.
- **fsh-tester** — MSTest-based test project exercising parsing, serialization, round-tripping, and validation.

## Tech Stack & Versions

- **.NET 10** (`net10.0` TFM)
- **C# latest** (currently 14.0) — use modern C# features (file-scoped namespaces, primary constructors, collection expressions, raw string literals, pattern matching, etc.) where appropriate.
- **ANTLR4** (`Antlr4.Runtime.Standard 4.13.1`) for grammar-based parsing.
- **Firely .NET SDK** (`Hl7.Fhir.Conformance` / `Hl7.Fhir.R5` 5.12.1) for FHIR resource types.
- **MSTest v4** (`MSTest 4.0.1`) for unit tests.
- Implicit usings and nullable reference types are enabled in both projects.

## Coding Conventions

### General Style
- File-scoped namespaces (one per file).
- XML doc comments (`<summary>`) on all public types and members.
- Use `string.Empty` for default string properties, not `""`.
- Use `required` modifier on properties that must be set at construction (e.g. `FshRule.Indent`).
- Prefer `List<T>` for mutable collections in the model.
- Use expression-bodied members where the body is a single expression.

### Naming
- Root namespace: `fsh_processor` (library), `fsh_tester` (tests).
- PascalCase for types, methods, and properties.
- `_camelCase` for private fields.
- Prefix interfaces with `I`.

### Model Architecture
- All FSH syntax tree nodes inherit from `FshNode`, which provides:
  - `SourcePosition? Position` — line/column tracking.
  - `LeadingHiddenTokens` / `TrailingHiddenTokens` — comment/whitespace preservation for round-tripping.
  - `IAnnotated` / `IAnnotatable` support (from Firely SDK) for attaching arbitrary metadata.
- `FshEntity` (abstract, extends `FshNode`) is the base for top-level definitions (Profile, Extension, Instance, ValueSet, CodeSystem, RuleSet, Alias, etc.).
- `FshRule` (abstract, extends `FshNode`) is the base for rules; has `Path` and `Indent` properties.
- `ParseResult` is a discriminated-union-style type with `Success` (contains `FshDoc`) and `Failure` (contains `List<ParseError>`) cases.

### Parser / Serializer
- `FshParser.Parse()` is the sole parsing entry point — it wires up the ANTLR lexer, parser, error listener, and `FshModelVisitor`.
- `FshSerializer.Serialize()` reconstructs FSH text from the object model, faithfully reproducing hidden tokens for exact round-trip fidelity.
- When adding new entity or rule types, update **both** the visitor (`FshModelVisitor`) and the serializer (`FshSerializer`).

### Tests
- Test project uses `[TestClass]` / `[TestMethod]` attributes (MSTest).
- `Microsoft.VisualStudio.TestTools.UnitTesting` is imported via a global using in the `.csproj`.
- Test data (`.fsh` files) lives under `fsh-tester/TestData/` and is copied to output via `<Content ... CopyToOutputDirectory="PreserveNewest" />`.
- Round-trip tests: parse → serialize → re-parse → assert structural equivalence.
- Use `Assert.IsInstanceOfType<T>(result)` for type assertions.
- Write `Console.WriteLine` output for diagnostic/debugging info in tests.

### ANTLR Files
- **Never** manually edit files under `fsh-processor/antlr/`. They are generated from the FSH grammar.
- If the grammar needs changes, regenerate the C# sources using the ANTLR4 tool.

## Key Patterns

- **Annotation pattern**: Use `entity.AddAnnotation(obj)` / `entity.Annotation<T>()` to attach metadata (e.g. `FileInfo` for source file tracking). This comes from the Firely SDK's `IAnnotatable` interface.
- **RuleSet substitution**: `InsertRule` references are resolved by looking up named `RuleSet` entries, performing parameter substitution on unparsed content, re-parsing via a synthetic Profile wrapper, and splicing the resulting rules into the host entity.
- **Alias resolution**: Aliases are collected across all parsed `FshDoc`s into a `Dictionary<string, string>` and passed to conversion engines.

## Do Not

- Do not modify ANTLR-generated files in `fsh-processor/antlr/`.
- Do not add new NuGet packages unless absolutely necessary.
- Do not break round-trip fidelity — hidden token handling must be preserved.
- Do not introduce `async` in the parser/serializer pipeline (it is intentionally synchronous).
