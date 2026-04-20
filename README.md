# fsh-processor

A .NET solution for parsing, serializing, and compiling [FHIR Shorthand (FSH)](https://build.fhir.org/ig/HL7/fhir-shorthand/) files, targeting FHIR R4, R4B, and R5.

## Packages

### [Hl7.FhirShorthand.Serialization](fsh-processor/README.md)

[![NuGet](https://img.shields.io/nuget/v/Hl7.FhirShorthand.Serialization)](https://www.nuget.org/packages/Hl7.FhirShorthand.Serialization)

Parses FSH text into a strongly-typed `FshDoc` object model and serializes it back to FSH with full comment and whitespace round-trip fidelity. No FHIR version dependency — suitable as a standalone FSH parser.

```bash
dotnet add package Hl7.FhirShorthand.Serialization
```

→ [Full documentation](fsh-processor/README.md)

---

### [Hl7.FhirShorthand.Compiler](fsh-compiler/README.md)

[![NuGet](https://img.shields.io/nuget/v/Hl7.FhirShorthand.Compiler)](https://www.nuget.org/packages/Hl7.FhirShorthand.Compiler)

Compiles a parsed `FshDoc` into FHIR resources (`StructureDefinition`, `ValueSet`, `CodeSystem`, `Instance`, etc.). 

Can use the base project providing your own ModelInspector, or use with a version-specific adapter:

| FHIR version | Package | NuGet |
|---|---|---|
| R4 (4.0.1) | `Hl7.FhirShorthand.Compiler.R4` | [![NuGet](https://img.shields.io/nuget/v/Hl7.FhirShorthand.Compiler.R4)](https://www.nuget.org/packages/Hl7.FhirShorthand.Compiler.R4) |
| R4B (4.3.0) | `Hl7.FhirShorthand.Compiler.R4B` | [![NuGet](https://img.shields.io/nuget/v/Hl7.FhirShorthand.Compiler.R4B)](https://www.nuget.org/packages/Hl7.FhirShorthand.Compiler.R4B) |
| R5 (5.0.0) | `Hl7.FhirShorthand.Compiler.R5` | [![NuGet](https://img.shields.io/nuget/v/Hl7.FhirShorthand.Compiler.R5)](https://www.nuget.org/packages/Hl7.FhirShorthand.Compiler.R5) |

```bash
dotnet add package Hl7.FhirShorthand.Compiler.R4   # or .R4B / .R5
```

→ [Full documentation](fsh-compiler/README.md)

---

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

## Solution Structure

```
fsh-processor/              # Hl7.FhirShorthand.Serialization — FSH parser and object model
fsh-compiler/               # Hl7.FhirShorthand.Compiler — version-agnostic compiler core
fsh-compiler-R4/            # Hl7.FhirShorthand.Compiler.R4 — FHIR R4 adapter
fsh-compiler-R4B/           # Hl7.FhirShorthand.Compiler.R4B — FHIR R4B adapter
fsh-compiler-R5/            # Hl7.FhirShorthand.Compiler.R5 — FHIR R5 adapter
fsh-tester/                 # MSTest project — parser and serializer tests
fsh-compiler-tester-R4/     # MSTest project — R4 compiler tests
```

## License

BSD 3-Clause — see [LICENSE.txt](LICENSE.txt) for details.

## Acknowledgements

* [FHIR Shorthand specification](https://build.fhir.org/ig/HL7/fhir-shorthand/)
* [Firely .NET SDK](https://github.com/FirelyTeam/firely-net-sdk)
* [ANTLR4](https://www.antlr.org/)

