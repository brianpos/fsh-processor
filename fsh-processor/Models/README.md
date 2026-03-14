# FSH Object Model

Object model for FHIR Shorthand (FSH) corresponding to the `FSH.g4` grammar.
All classes have XML doc comments — see the source files for property-level details.

## Design Rationale

Why specialized classes instead of a generic model?

- **Type-safe rule assignment** — each entity type (Profile, Extension, Instance, …) accepts only the rule types that are valid for it, catching misuse at compile time rather than runtime.
- **Hidden token preservation** — `FshNode.LeadingHiddenTokens` / `TrailingHiddenTokens` capture comments and whitespace from the ANTLR hidden channel so files can be round-tripped character-for-character. A `null` list means "use default formatting" — the serializer treats this as opt-in.
- **Annotation support** — `FshNode` implements `IAnnotated`/`IAnnotatable` (from the Firely SDK) so downstream passes can attach semantic metadata without modifying the model.
- **`FshRule.Indent`** — every rule carries the whitespace before its `*` character so serialization can reproduce the original indentation.

## Key Entry Points

| What | Where |
|---|---|
| Parse FSH text | `FshParser` → returns `ParseResult` (Success / Failure) |
| ANTLR tree → object model | `FshModelVisitor` (visitor over `FSHParser` contexts) |
| Object model → FSH text | `FshSerializer.Serialize(FshDoc)` |
| Clear formatting for default output | `node.ClearHiddenTokens()` extension method |

## Hidden Token Conventions

- `LeadingHiddenTokens` — block comments, line comments, and whitespace **before** an element.
- `TrailingHiddenTokens` — same-line trailing comments/whitespace **after** an element.
- `null` on either list = serializer uses default formatting rules.
- Newlines after trailing tokens become leading tokens for the **next** element.
- Extension methods on `FshNode`: `GetAllComments()`, `HasComments()`, `ClearHiddenTokens()`, `CopyHiddenTokensFrom()`, `GetLeadingText()`, `GetTrailingText()`.
- Extension methods on `List<HiddenToken>`: `GetComments()`, `GetWhitespace()`, `GetCombinedText()`, `HasComments()`, `HasWhitespace()`.

## Grammar → Object Model Mapping

These tables document which grammar rules map to which classes — something not obvious from the code alone.

### Entity Declarations
| Grammar Rule | Object Class |
|---|---|
| `aliasDecl` | `Alias` |
| `profileDecl` | `Profile` |
| `extensionDecl` | `Extension` |
| `logicalDecl` | `Logical` |
| `resourceDecl` | `Resource` |
| `instanceDecl` | `Instance` |
| `invariantDecl` | `Invariant` |
| `valueSetDecl` | `ValueSet` |
| `codeSystemDecl` | `CodeSystem` |
| `ruleSetDecl` | `RuleSet` |
| `mappingDecl` | `Mapping` |

### Rules
| Grammar Rule | Object Class(es) | Applicable Entities |
|---|---|---|
| `cardRule` | `CardRule` / `LrCardRule` | Profile, Extension, Logical, Resource |
| `flagRule` | `FlagRule` / `LrFlagRule` | Profile, Extension, Logical, Resource |
| `valueSetRule` | `ValueSetRule` | Profile, Extension |
| `fixedValueRule` | `FixedValueRule` / `InstanceFixedValueRule` / `InvariantFixedValueRule` | Most entities |
| `containsRule` | `ContainsRule` | Profile, Extension |
| `onlyRule` | `OnlyRule` | Profile, Extension |
| `obeysRule` | `ObeysRule` | Profile, Extension |
| `caretValueRule` | `CaretValueRule` / `VsCaretValueRule` / `CsCaretValueRule` / `CodeCaretValueRule` | All entities |
| `insertRule` | `InsertRule` / `InstanceInsertRule` / variant per entity | All entities |
| `pathRule` | `PathRule` / `InstancePathRule` / `MappingPathRule` | Profile, Extension, Instance, Mapping |
| `addElementRule` | `AddElementRule` | Logical, Resource |
| `addCRElementRule` | `AddCRElementRule` | Logical, Resource |
| `vsComponentRule` | `VsComponentRule` | ValueSet |
| `conceptRule` | `Concept` | CodeSystem |
| `mappingRule` | `MappingMapRule` | Mapping |

### Values
| Grammar Rule | Object Class |
|---|---|
| `STRING` / `MULTILINE_STRING` | `StringValue` |
| `NUMBER` | `NumberValue` |
| `DATETIME` | `DateTimeValue` |
| `TIME` | `TimeValue` |
| `BOOLEAN` | `BooleanValue` |
| `CODE` | `Code` |
| `quantity` | `Quantity` |
| `ratio` | `Ratio` / `RatioPart` |
| `reference` | `Reference` |
| `canonical` | `Canonical` |
| `codeableReference` | `CodeableReference` |
| `IDENTIFIER` / `NAME` | `NameValue` |
| `REGEX` | `RegexValue` |

