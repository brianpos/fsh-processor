# Hl7.FhirShorthand.Compiler

A .NET library for **compiling** [FHIR Shorthand (FSH)](https://build.fhir.org/ig/HL7/fhir-shorthand/) definitions into FHIR resources. It consumes the parsed `FshDoc` object model produced by `Hl7.FhirShorthand.Serialization` and emits FHIR `StructureDefinition`, `ValueSet`, `CodeSystem`, `Instance`, and other resource types via the Firely .NET SDK.

> This package is part of the [fsh-processor](https://github.com/brianpos/fsh-processor) project.
> For parsing FSH text into the object model, see [Hl7.FhirShorthand.Serialization](https://www.nuget.org/packages/Hl7.FhirShorthand.Serialization).

## Installation

Install this base package together with the version-specific adapter for your target FHIR release:

| FHIR version | Package |
|---|---|
| R4 (4.0.1) | `dotnet add package Hl7.FhirShorthand.Compiler.R4` |
| R4B (4.3.0) | `dotnet add package Hl7.FhirShorthand.Compiler.R4B` |
| R5 (5.0.0) | `dotnet add package Hl7.FhirShorthand.Compiler.R5` |

The base package (`Hl7.FhirShorthand.Compiler`) is pulled in automatically as a dependency.

## Features

* **Compile** parsed FSH definitions to FHIR resources targeting R4, R4B, or R5
* **Full entity support** — Profile, Extension, Logical, Resource, Instance, Invariant, ValueSet, CodeSystem, Mapping
* **Alias resolution** — aliases are collected across all parsed documents and applied during compilation
* **RuleSet expansion** — parameterized RuleSet references are resolved and spliced inline before compilation
* **Compiler errors and warnings** — structured `CompilerError` / `CompilerWarning` results with source locations

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `Hl7.FhirShorthand.Compiler` | `FshCompiler`, `CompilerOptions`, `CompileResult`, `CompilerError`, `CompilerWarning`, `AliasResolver`, `RuleSetResolver` |
| `Hl7.FhirShorthand.Compiler_r4` | `R4FshCompiler` (version-specific entry point) |
| `Hl7.FhirShorthand.Compiler_r4b` | `R4BFshCompiler` (version-specific entry point) |
| `Hl7.FhirShorthand.Compiler_r5` | `R5FshCompiler` (version-specific entry point) |

## Basic Usage (R4 example)

```csharp
using Hl7.FhirShorthand.Serialization;
using Hl7.FhirShorthand.Serialization.Models;
using Hl7.FhirShorthand.Compiler_r4;

// 1. Parse FSH text
ParseResult parseResult = FshParser.Parse(fshText);
if (parseResult is not ParseResult.Success success)
    return;

FshDoc doc = success.Document;

// 2. Compile to R4 FHIR resources
CompileResult result = R4FshCompiler.Compile(doc);

foreach (var resource in result.Resources)
    Console.WriteLine($"{resource.TypeName}/{resource.Id}");

foreach (var error in result.Errors)
    Console.WriteLine($"ERROR {error.Location}: {error.Message}");
```

## Project Structure

```
fsh-compiler/            # Version-agnostic compiler core
│                        # Namespace: Hl7.FhirShorthand.Compiler
├── FshCompiler.cs       # Main compiler orchestration
├── AliasResolver.cs     # Resolves FSH aliases across documents
├── RuleSetResolver.cs   # Expands parameterized RuleSet references
├── CompilerContext.cs   # Compilation state and options
└── ...                  # CompileResult, CompilerError, CompilerWarning, etc.

fsh-compiler-R4/         # FHIR R4 adapter — Namespace: Hl7.FhirShorthand.Compiler_r4
└── R4FshCompiler.cs

fsh-compiler-R4B/        # FHIR R4B adapter — Namespace: Hl7.FhirShorthand.Compiler_r4b
└── R4BFshCompiler.cs

fsh-compiler-R5/         # FHIR R5 adapter — Namespace: Hl7.FhirShorthand.Compiler_r5
└── R5FshCompiler.cs
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [Hl7.FhirShorthand.Serialization](https://www.nuget.org/packages/Hl7.FhirShorthand.Serialization) | FSH parser and object model |
| [Hl7.Fhir.Conformance](https://www.nuget.org/packages/Hl7.Fhir.Conformance) | Firely .NET SDK — version-agnostic model introspection |
| [Hl7.Fhir.R4](https://www.nuget.org/packages/Hl7.Fhir.R4) *(R4 adapter only)* | FHIR R4 resource models |
| [Hl7.Fhir.R4B](https://www.nuget.org/packages/Hl7.Fhir.R4B) *(R4B adapter only)* | FHIR R4B resource models |
| [Hl7.Fhir.R5](https://www.nuget.org/packages/Hl7.Fhir.R5) *(R5 adapter only)* | FHIR R5 resource models |

## License

BSD 3-Clause — see [LICENSE.txt](https://github.com/brianpos/fsh-processor/blob/master/LICENSE.txt) for details.
