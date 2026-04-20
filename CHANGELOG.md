# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The two core packages ship together at the same version:
`Hl7.FhirShorthand.Serialization`, `Hl7.FhirShorthand.Compiler`.

The version-specific compiler adapters (`Hl7.FhirShorthand.Compiler.R4`,
`Hl7.FhirShorthand.Compiler.R4B`, `Hl7.FhirShorthand.Compiler.R5`) will
be published in a future release.


## [Unreleased]

### Added
### Changed
### Fixed
### Removed


### Added
- Initial release under the `Hl7.FhirShorthand.*` namespace family.
- `Hl7.FhirShorthand.Serialization`: full FSH parser and serializer built on ANTLR4.
  Supports all FSH entity types (Profile, Extension, Logical, Resource, Instance,
  Invariant, ValueSet, CodeSystem, RuleSet, Alias, Mapping) and rule types
  (Cardinality, Flag, Type, Assignment, Binding, Contains, Obeys, Caret-value,
  Insert, AddElement).
- `Hl7.FhirShorthand.Compiler`: version-agnostic compiler core with alias resolution
  and parameterized RuleSet expansion.
- `Hl7.FhirShorthand.Compiler.R4`: FHIR R4 (4.0.1) compiler adapter.
- `Hl7.FhirShorthand.Compiler.R4B`: FHIR R4B (4.3.0) compiler adapter.
- `Hl7.FhirShorthand.Compiler.R5`: FHIR R5 (5.0.0) compiler adapter.
- Source symbols published as `.snupkg` to NuGet.org for all packages.

[Unreleased]: https://github.com/brianpos/fsh-processor/compare/v1.0.0-alpha.1...HEAD
[1.0.0-alpha.1]: https://github.com/brianpos/fsh-processor/releases/tag/v1.0.0-alpha.1
