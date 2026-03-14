# fsh-processor

A .NET library for parsing, validating, and serializing [FHIR Shorthand (FSH)](https://build.fhir.org/ig/HL7/fhir-shorthand/) files. Built on an ANTLR4 grammar, it produces a strongly-typed object model that can be inspected, transformed, and round-tripped back to FSH text.

## Features

* **Parse** FSH text into a structured `FshDoc` object model via `FshParser.Parse()`
* **Serialize** a `FshDoc` back to valid FSH text via `FshSerializer.Serialize()` (round-trip capable, preserving comments and whitespace)
* *(very partially)* **Convert** parsed FSH definitions to FHIR R5 resources (e.g. `Profile` → `StructureDefinition`) via the `ConvertToProfile` engine
* **Full entity support** — Alias, Profile, Extension, Logical, Resource, Instance, Invariant, ValueSet, CodeSystem, RuleSet (including parameterized), and Mapping
* **Rich rule model** — Cardinality, Flag, Type, Assignment, Binding, Contains, Obeys, Caret-value, Insert, AddElement, and more
* **Source-position tracking** — every node carries line/column information for diagnostics
* **Hidden-token preservation** — comments and blank lines are captured so serialized output faithfully reproduces the original formatting

## Getting Started

### Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Basic Usage

```csharp
using fsh_processor;
using fsh_processor.Models;

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

## Project Structure

```
fsh-processor/          # Main library
├── antlr/              # ANTLR4-generated lexer, parser, and visitor/listener base classes
├── Engine/             # Conversion logic (FSH → FHIR resources)
├── Models/             # Strongly-typed object model (FshDoc, FshEntity, rules, etc.)
├── Visitors/           # ANTLR visitor that builds the object model
├── FshParser.cs        # Public entry point for parsing FSH text
└── FshSerializer.cs    # Public entry point for serializing FshDoc to FSH text

fsh-tester/             # MSTest test project
├── ParserTests.cs      # Core parser tests
├── RoundTripTests.cs   # Round-trip (parse → serialize → parse) tests
└── ...                 # Additional test suites
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [Antlr4.Runtime.Standard](https://www.nuget.org/packages/Antlr4.Runtime.Standard) | ANTLR4 runtime for the FSH grammar |
| [Hl7.Fhir.R5](https://www.nuget.org/packages/Hl7.Fhir.R5) | Firely .NET SDK — FHIR R5 resource models |
| [Hl7.Fhir.Conformance](https://www.nuget.org/packages/Hl7.Fhir.Conformance) | Firely .NET SDK — conformance resource support |

## License

This project is licensed under the **BSD 3-Clause License** — see [LICENSE.txt](LICENSE.txt) for details.

## Acknowledgements

* [FHIR Shorthand specification](https://build.fhir.org/ig/HL7/fhir-shorthand/)
* [Firely .NET SDK](https://github.com/FirelyTeam/firely-net-sdk)
* [ANTLR4](https://www.antlr.org/)
