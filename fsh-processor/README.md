# Hl7.FhirShorthand.Serialization

A .NET library for **parsing** and **serializing** [FHIR Shorthand (FSH)](https://build.fhir.org/ig/HL7/fhir-shorthand/) text. Built on an ANTLR4 grammar, it produces a strongly-typed object model that can be inspected, transformed, and round-tripped back to FSH text with full comment and whitespace fidelity.

> This package is part of the [fsh-processor](https://github.com/brianpos/fsh-processor) project.
> For compiling FSH to FHIR resources, see [Hl7.FhirShorthand.Compiler](https://www.nuget.org/packages/Hl7.FhirShorthand.Compiler).

## Installation

```bash
dotnet add package Hl7.FhirShorthand.Serialization
```

## Features

* **Parse** FSH text into a structured `FshDoc` object model via `FshParser.Parse()`
* **Serialize** a `FshDoc` back to valid FSH text via `FshSerializer.Serialize()` (round-trip capable, preserving comments and whitespace)
* **Full entity support** — Alias, Profile, Extension, Logical, Resource, Instance, Invariant, ValueSet, CodeSystem, RuleSet (including parameterized), and Mapping
* **Rich rule model** — Cardinality, Flag, Type, Assignment, Binding, Contains, Obeys, Caret-value, Insert, AddElement, and more
* **Source-position tracking** — every node carries line/column information for diagnostics
* **Hidden-token preservation** — comments and blank lines are captured so serialized output faithfully reproduces the original formatting

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `Hl7.FhirShorthand.Serialization` | `FshParser`, `FshSerializer`, `ParseResult` |
| `Hl7.FhirShorthand.Serialization.Models` | `FshDoc`, `FshEntity` subclasses, `FshRule` subclasses, `FshNode` |

## Basic Usage

```csharp
using Hl7.FhirShorthand.Serialization;
using Hl7.FhirShorthand.Serialization.Models;

string fshText = """
    Profile: MyPatientProfile
    Parent: Patient
    Title: "My Patient Profile"
    Description: "A custom patient profile"
    * name 1..* MS
    * birthDate 1..1
    """;

ParseResult result = FshParser.Parse(fshText);

if (result is ParseResult.Success success)
{
    FshDoc doc = success.Document;

    // Inspect parsed entities
    foreach (var entity in doc.Entities)
    {
        Console.WriteLine($"{entity.GetType().Name}: {entity.Name}");
    }

    // Round-trip back to FSH text
    string output = FshSerializer.Serialize(doc);
}
else if (result is ParseResult.Failure failure)
{
    foreach (var error in failure.Errors)
    {
        Console.WriteLine($"{error.Severity} {error.Location}: {error.Message}");
    }
}
```

## Object Model

> Full grammar-to-class mapping and hidden-token conventions are documented in [Models/README.md](Models/README.md).

All FSH syntax tree nodes inherit from `FshNode`, which provides:

* `SourcePosition? Position` — line/column tracking for diagnostics
* `LeadingHiddenTokens` / `TrailingHiddenTokens` — comment and whitespace preservation
* `IAnnotated` / `IAnnotatable` support (from the Firely SDK) for attaching arbitrary metadata

Top-level FSH definitions (Profile, Extension, Instance, etc.) all inherit from the abstract `FshEntity` base class. Rules inherit from the abstract `FshRule` base class.

`ParseResult` is a discriminated-union-style type with two cases:

* `ParseResult.Success` — contains the parsed `FshDoc`
* `ParseResult.Failure` — contains a `List<ParseError>` with severity, location, and message

## Project Structure

```
fsh-processor/
├── antlr/           # ANTLR4-generated lexer, parser, visitor/listener base classes (do not hand-edit)
├── Models/          # Strongly-typed object model
├── Visitors/        # FshModelVisitor — builds the object model from the ANTLR parse tree
├── FshParser.cs     # Public entry point: FshParser.Parse(string) → ParseResult
└── FshSerializer.cs # Public entry point: FshSerializer.Serialize(FshDoc) → string
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [Antlr4.Runtime.Standard](https://www.nuget.org/packages/Antlr4.Runtime.Standard) | ANTLR4 runtime for the FSH grammar |
| [Hl7.Fhir.Conformance](https://www.nuget.org/packages/Hl7.Fhir.Conformance) | Firely SDK annotation infrastructure (`IAnnotated`, `IAnnotatable`, `AnnotationList`) used by `FshNode` — no FHIR resource types are used from this package |

## License

BSD 3-Clause — see [LICENSE.txt](https://github.com/brianpos/fsh-processor/blob/master/LICENSE.txt) for details.
