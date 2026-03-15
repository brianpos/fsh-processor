# SUSHI FSHImporter Test Mapping

This document tracks the mapping between the upstream [SUSHI](https://github.com/FHIR/sushi) TypeScript
FSHImporter test suite and the ported C# MSTest equivalents in `fsh-tester/Sushi/`.

Its purpose is to make it easy to spot new or changed SUSHI tests and decide whether the corresponding
C# test needs to be added, updated, or left inconclusive due to a known behavioural difference.

---

## Quick-reference table

| SUSHI source file | C# test file | Tests | ✅ Pass | ⚠️ Inconclusive | ❌ Fail |
|---|---|---|---|---|---|
| `FSHImporter.Alias.test.ts` | `Sushi.AliasTests.cs` | 25 | 10 | 15 | 0 |
| `FSHImporter.CodeSystem.test.ts` | `Sushi.CodeSystemTests.cs` | 22 | 13 | 9 | 0 |
| `FSHImporter.Extension.test.ts` | `Sushi.ExtensionTests.cs` | 22 | 9 | 13 | 0 |
| `FSHImporter.Instance.test.ts` | `Sushi.InstanceTests.cs` | 21 | 11 | 10 | 0 |
| `FSHImporter.Invariant.test.ts` | `Sushi.InvariantTests.cs` | 14 | 7 | 7 | 0 |
| `FSHImporter.Logical.test.ts` | `Sushi.LogicalTests.cs` | 14 | 9 | 5 | 0 |
| `FSHImporter.Mapping.test.ts` | `Sushi.MappingTests.cs` | 17 | 12 | 5 | 0 |
| `FSHImporter.ParamRuleSet.test.ts` | `Sushi.ParamRuleSetTests.cs` | 7 | 4 | 3 | 0 |
| `FSHImporter.Profile.test.ts` | `Sushi.ProfileTests.cs` | 31 | 22 | 9 | 0 |
| `FSHImporter.Resource.test.ts` | `Sushi.ResourceTests.cs` | 14 | 8 | 6 | 0 |
| `FSHImporter.RuleSet.test.ts` | `Sushi.RuleSetTests.cs` | 12 | 9 | 3 | 0 |
| `FSHImporter.SDRules.test.ts` | `Sushi.SDRulesTests.cs` | 33 | 29 | 4 | 0 |
| `FSHImporter.ValueSet.test.ts` | `Sushi.ValueSetTests.cs` | 16 | 12 | 4 | 0 |
| `FSHImporter.Context.test.ts` | _(not yet ported)_ | — | — | — | — |
| **Total** | | **248** | **155** | **93** | **0** |

> Counts last updated against SUSHI `main` branch as of 2026-03-15.

---

## Cross-cutting behavioural differences

These differences apply across **all** test files.  Tests that exercise behaviours in this list are
marked `Inconclusive` rather than made to fail.

| # | Difference | Impact |
|---|---|---|
| 1 | **No semantic validation** — SUSHI reports errors/warnings via `loggerSpy` for duplicates, invalid codes, missing required fields, etc. fsh-processor is a pure syntax parser. | All "ShouldLogAnError…" tests are Inconclusive. |
| 2 | **Single-file only** — SUSHI's `importText()` accepts multiple `RawFSH` inputs and cross-file alias sharing, duplicate detection, etc. fsh-processor's `ParseDoc` parses one document. | All "…AcrossFiles" and multi-file alias tests are Inconclusive. |
| 3 | **No default Id** — SUSHI defaults `Id` to the entity name when the keyword is omitted. fsh-processor leaves `Id` null. | Checked with `Assert.IsNull` instead of asserting the name. |
| 4 | **Last-wins vs first-wins** — SUSHI first-wins for duplicate metadata attributes. fsh-processor last-wins. | "ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared" tests are Inconclusive. |
| 5 | **`CaretPath` retains `^` prefix** — fsh-processor stores `^short`, SUSHI strips it to `short`. Normalised by `SushiTestHelper.AssertCaretValueRule`. | Handled transparently; not Inconclusive. |
| 6 | **`Strength` retains `()` wrapper** — fsh-processor stores `(extensible)`, SUSHI strips it to `extensible`. Normalised by `SushiTestHelper.AssertBindingRule`. | Handled transparently; not Inconclusive. |
| 7 | **Boolean literals stored as `StringValue`** — fsh-processor does not produce a `BooleanValue` node for `true`/`false`. | `ShouldParseAssignedValueBooleanRule` tests are Inconclusive where `BooleanValue` is asserted. |
| 8 | **`CardRule` + `FlagRule` not split** — SUSHI splits a combined cardinality+flag rule (e.g. `0..1 MS`) into a `CardRule` and a separate `FlagRule`. fsh-processor stores them combined. | "ShouldParseCardRulesWithFlags" and similar tests are Inconclusive. |
| 9 | **Multi-invariant `ObeysRule` not split** — SUSHI emits one `ObeysRule` per invariant when a rule lists multiple. fsh-processor keeps them in a single rule. | "ShouldParseObeysRuleWithMultipleInvariants" tests are Inconclusive. |
| 10 | **`ContainsRule` + `CardRule` not split** — SUSHI emits a `ContainsRule` followed by one `CardRule` per item. fsh-processor combines them. | Related "ShouldParseContainsRule" tests are Inconclusive. |
| 11 | **Column positions are 0-based** — ANTLR produces 0-based column indices; SUSHI uses 1-based. | Column assertions use `position.Column` directly without +1 adjustment. |
| 12 | **`PathRule` retained in rule list** — SUSHI discards `PathRule` / `MappingPathRule` from the entity's rules collection. fsh-processor retains them. | `PathRule` assertions adapted accordingly. |

---

## Per-file details

### Alias — `FSHImporter.Alias.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Alias.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.AliasTests.cs`

Additional differences specific to this file:
- SUSHI **resolves** alias names inside rules (binding, only, reference, assignment rules). fsh-processor stores the alias name verbatim — the original alias token, not the expanded URL/system.

| SUSHI test | C# method | Status |
|---|---|---|
| should collect and return aliases in result | `ShouldCollectAndReturnAliasesInResult` | ✅ |
| should parse aliases that replicate the syntax of a code | `ShouldParseAliasesThatReplicateTheSyntaxOfACode` | ✅ |
| should report when the same alias is defined twice with different values in the same file | `ShouldReportWhenTheSameAliasIsDefinedTwiceWithDifferentValuesInTheSameFile` | ⚠️ semantic validation |
| should report when the same alias is defined twice with different values in different files | `ShouldReportWhenTheSameAliasIsDefinedTwiceWithDifferentValuesInDifferentFiles` | ⚠️ multi-file |
| should not report error when the same alias is defined multiple times with same values | `ShouldNotReportErrorWhenTheSameAliasIsDefinedMultipleTimesWithSameValues` | ✅ |
| should not resolve alias in binding rule when alias is defined before its use | `ShouldNotResolveAliasInBindingRuleWhenAliasIsDefinedBeforeItsUse` | ✅ |
| should not resolve alias in binding rule when alias is defined after its use | `ShouldNotResolveAliasInBindingRuleWhenAliasIsDefinedAfterItsUse` | ✅ |
| should not translate an alias when alias does not match | `ShouldNotTranslateAnAliasWhenAliasDoesNotMatch` | ✅ |
| should translate an alias from any input file | `ShouldTranslateAnAliasFromAnyInputFile` | ⚠️ multi-file |
| should log an error when an aliased code prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAliasedCodePrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an aliased value set rule prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAliasedValueSetRulePrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an aliased reference prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAliasedReferencePrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an assignment rule aliased reference prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAssignmentRuleAliasedReferencePrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an only rule aliased reference prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnOnlyRuleAliasedReferencePrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should not log an error when a contains rule aliased extension prefixed with $ does not resolve | `ShouldNotLogAnErrorWhenAContainsRuleAliasedExtensionPrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an aliased contains rule type prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAliasedContainsRuleTypePrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an aliased value set system prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAliasedValueSetSystemPrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should log an error when an aliased value set prefixed with $ does not resolve | `ShouldLogAnErrorWhenAnAliasedValueSetPrefixedWithDollarDoesNotResolve` | ⚠️ semantic validation |
| should not resolve alias in code with version | `ShouldNotResolveAliasInCodeWithVersion` | ✅ |
| should not resolve alias in code with empty version | `ShouldNotResolveAliasInCodeWithEmptyVersion` | ✅ |
| should log an error when alias contains reserved characters | `ShouldLogAnErrorWhenAliasContainsReservedCharacters` | ⚠️ semantic validation |
| should resolve an alias with all supported characters | `ShouldResolveAnAliasWithAllSupportedCharacters` | ✅ |
| should resolve but log warning when alias contains unsupported characters | `ShouldResolveButLogWarningWhenAliasContainsUnsupportedCharacters` | ⚠️ semantic validation |
| should resolve but log warning when alias contains unsupported characters and starts with $ | `ShouldResolveButLogWarningWhenAliasContainsUnsupportedCharactersAndStartsWithDollar` | ⚠️ semantic validation |
| should resolve but log warning when alias contains only a $ | `ShouldResolveButLogWarningWhenAliasContainsOnlyADollar` | ⚠️ semantic validation |

---

### CodeSystem — `FSHImporter.CodeSystem.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.CodeSystem.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.CodeSystemTests.cs`

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible CodeSystem | `ShouldParseTheSimplestPossibleCodeSystem` | ✅ |
| should parse CodeSystem with additional metadata | `ShouldParseCodeSystemWithAdditionalMetadata` | ✅ |
| should parse numeric CodeSystem name and id | `ShouldParseNumericCodeSystemNameAndId` | ✅ |
| should parse CodeSystem with multi-line description | `ShouldParseCodeSystemWithMultiLineDescription` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip CodeSystem with duplicate name | `ShouldLogAnErrorAndSkipCodeSystemWithDuplicateName` | ⚠️ semantic validation |
| should log an error and skip CodeSystem with duplicate name across files | `ShouldLogAnErrorAndSkipCodeSystemWithDuplicateNameAcrossFiles` | ⚠️ multi-file |
| should parse CodeSystem with one concept | `ShouldParseCodeSystemWithOneConcept` | ✅ |
| should parse CodeSystem with one concept with a display string | `ShouldParseCodeSystemWithOneConceptWithADisplayString` | ✅ |
| should parse CodeSystem with one concept with display and definition | `ShouldParseCodeSystemWithOneConceptWithDisplayAndDefinition` | ✅ |
| should parse concept with multi-line definition | `ShouldParseConceptWithMultiLineDefinition` | ✅ |
| should parse CodeSystem with more than one concept | `ShouldParseCodeSystemWithMoreThanOneConcept` | ✅ |
| should parse CodeSystem with hierarchical codes | `ShouldParseCodeSystemWithHierarchicalCodes` | ✅ |
| should log an error when concept includes a system declaration | `ShouldLogAnErrorWhenConceptIncludesASystemDeclaration` | ⚠️ semantic validation |
| should parse CodeSystem caret value rule with no codes | `ShouldParseCodeSystemCaretValueRuleWithNoCodes` | ✅ |
| should parse CodeSystem caret value rules with no codes alongside rules | `ShouldParseCodeSystemCaretValueRulesWithNoCodesAlongsideRules` | ✅ |
| should parse CodeSystem caret value rule on top-level concept | `ShouldParseCodeSystemCaretValueRuleOnTopLevelConcept` | ✅ |
| should parse CodeSystem caret value rule on nested concept | `ShouldParseCodeSystemCaretValueRuleOnNestedConcept` | ✅ |
| should keep raw value of code caret value rule for number or boolean | `ShouldKeepRawValueOfCodeCaretValueRuleForNumberOrBoolean` | ⚠️ BooleanValue not produced |
| should parse insert rule with single RuleSet | `ShouldParseInsertRuleWithSingleRuleSet` | ✅ |
| should parse insert rule with single RuleSet and code path | `ShouldParseInsertRuleWithSingleRuleSetAndCodePath` | ✅ |

---

### Extension — `FSHImporter.Extension.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Extension.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.ExtensionTests.cs`

Additional differences: SUSHI defaults `Parent` to `Extension` and `Id` to the entity name when omitted. fsh-processor stores null.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Extension | `ShouldParseTheSimplestPossibleExtension` | ✅ |
| should parse Extension with additional metadata properties | `ShouldParseExtensionWithAdditionalMetadataProperties` | ✅ |
| should parse numeric Extension name, parent, and id | `ShouldParseNumericExtensionNameParentAndId` | ✅ |
| should parse Extension with multiple contexts | `ShouldParseExtensionWithMultipleContexts` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering a duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringADuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip Extension with duplicate name | `ShouldLogAnErrorAndSkipExtensionWithDuplicateName` | ⚠️ semantic validation |
| should log an error and skip Extension with duplicate name across files | `ShouldLogAnErrorAndSkipExtensionWithDuplicateNameAcrossFiles` | ⚠️ multi-file |
| should log an error when deprecated Mixins keyword is used | `ShouldLogAnErrorWhenDeprecatedMixinsKeywordIsUsed` | ⚠️ semantic validation |
| should parse simple card rules | `ShouldParseSimpleCardRules` | ✅ |
| should parse card rules with flags | `ShouldParseCardRulesWithFlags` | ⚠️ card+flag not split |
| should parse single path, single value flag rules | `ShouldParseSinglePathSingleValueFlagRules` | ✅ |
| should parse value set rules with names and strength | `ShouldParseValueSetRulesWithNamesAndStrength` | ✅ |
| should parse assigned value boolean rule | `ShouldParseAssignedValueBooleanRule` | ⚠️ BooleanValue not produced |
| should parse assigned value boolean rule with exactly modifier | `ShouldParseAssignedValueBooleanRuleWithExactlyModifier` | ⚠️ BooleanValue not produced |
| should parse an only rule with one type | `ShouldParseAnOnlyRuleWithOneType` | ✅ |
| should parse contains rule with one item | `ShouldParseContainsRuleWithOneItem` | ⚠️ contains+card not split |
| should parse contains rule with reserved word code | `ShouldParseContainsRuleWithReservedWordCode` | ⚠️ contains+card not split |
| should parse contains rule with item declaring a type | `ShouldParseContainsRuleWithItemDeclaringAType` | ⚠️ contains+card not split |
| should parse a caret value rule with a path | `ShouldParseACaretValueRuleWithAPath` | ✅ |
| should parse an obeys rule with a path and multiple invariants | `ShouldParseAnObeysRuleWithAPathAndMultipleInvariants` | ⚠️ multi-invariant obeys not split |
| should parse an insert rule with a single RuleSet | `ShouldParseAnInsertRuleWithASingleRuleSet` | ✅ |

---

### Instance — `FSHImporter.Instance.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Instance.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.InstanceTests.cs`

Additional differences:
- SUSHI normalises `usage` codes (strips `#`, capitalises first letter). fsh-processor stores the raw token (e.g. `#example`).
- SUSHI drops instances with no `InstanceOf`; fsh-processor retains them with `InstanceOf = null`.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Instance | `ShouldParseTheSimplestPossibleInstance` | ✅ |
| should parse numeric instance name and numeric InstanceOf | `ShouldParseNumericInstanceNameAndNumericInstanceOf` | ✅ |
| should parse an Instance with an aliased type | `ShouldParseAnInstanceWithAnAliasedType` | ✅ |
| should not parse an Instance that has no type | `ShouldNotParseAnInstanceThatHasNoType` | ⚠️ semantic validation |
| should parse an Instance with a title | `ShouldParseAnInstanceWithATitle` | ✅ |
| should parse an Instance with a description | `ShouldParseAnInstanceWithADescription` | ✅ |
| should parse an Instance with a Usage | `ShouldParseAnInstanceWithAUsage` | ✅ |
| should log an error for invalid Usage and set default Usage to Example | `ShouldLogAnErrorForInvalidUsageAndSetDefaultUsageToExample` | ⚠️ semantic validation |
| should log a warning if a system is specified on Usage | `ShouldLogAWarningIfASystemIsSpecifiedOnUsage` | ⚠️ semantic validation |
| should log a warning if conformance or terminology resource does not have Usage | `ShouldLogAWarningIfConformanceOrTerminologyResourceDoesNotHaveUsage` | ⚠️ semantic validation |
| should log an error when the deprecated Mixins keyword is used | `ShouldLogAnErrorWhenTheDeprecatedMixinsKeywordIsUsed` | ⚠️ semantic validation |
| should parse an Instance with assigned value rules | `ShouldParseAnInstanceWithAssignedValueRules` | ✅ |
| should parse an Instance with assigned values that are an alias | `ShouldParseAnInstanceWithAssignedValuesThatAreAnAlias` | ✅ |
| should parse an Instance with assigned value Resource rules | `ShouldParseAnInstanceWithAssignedValueResourceRules` | ✅ |
| should parse a path rule | `ShouldParseAPathRule` | ✅ |
| should parse an insert rule with a single RuleSet | `ShouldParseAnInsertRuleWithASingleRuleSet` | ✅ |
| should parse an insert rule with an empty parameter value | `ShouldParseAnInsertRuleWithAnEmptyParameterValue` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encounter duplicate metadata attribute | `ShouldLogAnErrorWhenEncounterDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip instance with name used by another instance | `ShouldLogAnErrorAndSkipInstanceWithNameUsedByAnotherInstance` | ⚠️ semantic validation |
| should log an error and skip instance with name used by another instance in another file | `ShouldLogAnErrorAndSkipInstanceWithNameUsedByAnotherInstanceInAnotherFile` | ⚠️ multi-file |

---

### Invariant — `FSHImporter.Invariant.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Invariant.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.InvariantTests.cs`

Additional differences:
- SUSHI stores `Severity` as a `FshCode` (stripping `#`). fsh-processor stores the raw CODE token including `#`.
- SUSHI discards `InvariantPathRule` from the rules list; fsh-processor retains it.
- SUSHI composes child-rule paths from parent path rules (indentation-based). fsh-processor does not compose paths.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Invariant | `ShouldParseTheSimplestPossibleInvariant` | ✅ |
| should parse numeric Invariant name | `ShouldParseNumericInvariantName` | ✅ |
| should parse an Invariant with additional metadata | `ShouldParseAnInvariantWithAdditionalMetadata` | ✅ |
| should parse an Invariant with multiline expression | `ShouldParseAnInvariantWithMultilineExpression` | ⚠️ MULTILINE_STRING not supported |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip Invariant when encountering a duplicate name | `ShouldLogAnErrorAndSkipInvariantWhenEncounteringADuplicateName` | ⚠️ semantic validation |
| should log an error and skip Invariant when duplicate name in another file | `ShouldLogAnErrorAndSkipInvariantWhenDuplicateNameInAnotherFile` | ⚠️ multi-file |
| should parse an Invariant with assigned value rules | `ShouldParseAnInvariantWithAssignedValueRules` | ✅ |
| should parse an Invariant with assigned values that are an alias | `ShouldParseAnInvariantWithAssignedValuesThatAreAnAlias` | ✅ |
| should parse a path rule and include it in rules | `ShouldParseAPathRuleAndIncludeItInRules` | ✅ |
| should use a path rule to construct a full path | `ShouldUseAPathRuleToConstructAFullPath` | ✅ |
| should properly handle soft indices with path rules | `ShouldProperlyHandleSoftIndicesWithPathRules` | ✅ |
| should parse an insert rule | `ShouldParseAnInsertRule` | ✅ |

---

### Logical — `FSHImporter.Logical.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Logical.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.LogicalTests.cs`

Additional differences:
- `Logical` uses `string?` properties (not `Metadata?`) for `Parent`, `Id`, `Title`, `Description`.
- `Logical.Characteristics` stores raw characteristic codes as strings.
- `CaretValueRule` and `InsertRule` are not yet supported on `Logical` by the parser.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Logical Model | `ShouldParseTheSimplestPossibleLogicalModel` | ✅ |
| should parse Logical Model with all metadata fields | `ShouldParseLogicalModelWithAllMetadataFields` | ✅ |
| should parse numeric Logical Model name, parent, and id | `ShouldParseNumericLogicalModelNameParentAndId` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip Logical with duplicate name | `ShouldLogAnErrorAndSkipLogicalWithDuplicateName` | ⚠️ semantic validation |
| should parse Logical with single characteristic | `ShouldParseLogicalWithSingleCharacteristic` | ✅ |
| should parse Logical with multiple characteristics | `ShouldParseLogicalWithMultipleCharacteristics` | ✅ |
| should parse AddElementRule with type and description | `ShouldParseAddElementRuleWithTypeAndDescription` | ✅ |
| should parse AddElementRule with multiple types | `ShouldParseAddElementRuleWithMultipleTypes` | ✅ |
| should parse AddElementRule with short and definition | `ShouldParseAddElementRuleWithShortAndDefinition` | ✅ |
| should parse multiple AddElementRules | `ShouldParseMultipleAddElementRules` | ✅ |
| should parse insert rule on Logical | `ShouldParseInsertRuleOnLogical` | ⚠️ InsertRule not supported on Logical |
| should parse caret value rule on Logical | `ShouldParseCaretValueRuleOnLogical` | ⚠️ CaretValueRule not supported on Logical |

---

### Mapping — `FSHImporter.Mapping.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Mapping.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.MappingTests.cs`

Additional differences:
- SUSHI defaults `Mapping.id` to the mapping name when `Id` is omitted. fsh-processor leaves it null.
- SUSHI resolves aliases in `Mapping.source`. fsh-processor stores the raw name.
- `MappingMapRule.Language` stores the optional comment (second STRING); `MappingMapRule.Code` stores the language code (`CODE` token). SUSHI calls these `comment` and `language` respectively.
- SUSHI discards `MappingPathRule` from the rules list; fsh-processor retains it.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Mapping | `ShouldParseTheSimplestPossibleMapping` | ✅ |
| should parse a Mapping with additional metadata properties | `ShouldParseAMappingWithAdditionalMetadataProperties` | ✅ |
| should parse numeric Mapping name, id and source | `ShouldParseNumericMappingNameIdAndSource` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should not resolve alias for Mapping source | `ShouldNotResolveAliasForMappingSource` | ✅ |
| should log an error and skip Mapping when encountering duplicate name | `ShouldLogAnErrorAndSkipMappingWhenEncounteringDuplicateName` | ⚠️ semantic validation |
| should log an error and skip Mapping when duplicate name in another file | `ShouldLogAnErrorAndSkipMappingWhenDuplicateNameInAnotherFile` | ⚠️ multi-file |
| should parse a simple mapping rule | `ShouldParseASimpleMappingRule` | ✅ |
| should parse a mapping rule with no path | `ShouldParseAMappingRuleWithNoPath` | ✅ |
| should parse a mapping rule with a comment | `ShouldParseAMappingRuleWithAComment` | ✅ |
| should parse a mapping rule with a language | `ShouldParseAMappingRuleWithALanguage` | ✅ |
| should parse a mapping rule with comment and language | `ShouldParseAMappingRuleWithCommentAndLanguage` | ✅ |
| should log a warning when language has a system | `ShouldLogAWarningWhenLanguageHasASystem` | ⚠️ semantic validation |
| should parse a mapping rule with a multiline comment | `ShouldParseAMappingRuleWithAMultilineComment` | ⚠️ MULTILINE_STRING not supported |
| should parse a mapping rule with a multiline comment and language | `ShouldParseAMappingRuleWithAMultilineCommentAndLanguage` | ⚠️ MULTILINE_STRING not supported |
| should parse an insert rule with a single RuleSet | `ShouldParseAnInsertRuleWithASingleRuleSet` | ✅ |
| should parse a path rule and include it in Mapping rules | `ShouldParseAPathRuleAndIncludeItInMappingRules` | ✅ |

---

### ParamRuleSet — `FSHImporter.ParamRuleSet.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.ParamRuleSet.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.ParamRuleSetTests.cs`

Additional differences:
- SUSHI stores parameterised rule sets in a separate `paramRuleSets` map. fsh-processor stores them in the same document as regular `RuleSet` entries, with `IsParameterized = true`.
- SUSHI's `parameters` is a `string[]`. fsh-processor uses `List<RuleSetParameter>` with a `.Value` property.
- SUSHI's `contents` holds raw template text. fsh-processor stores this in `RuleSet.UnparsedContent`.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse a param RuleSet with a rule | `ShouldParseAParamRuleSetWithARule` | ✅ |
| should parse a param RuleSet with a numeric name | `ShouldParseAParamRuleSetWithANumericName` | ✅ |
| should parse a param RuleSet when there is no space between RuleSet name and parameter list | `ShouldParseAParamRuleSetWhenThereIsNoSpaceBetweenRulesetNameAndParameterList` | ✅ |
| should stop parsing param RuleSet contents when the next entity is defined | `ShouldStopParsingParamRuleSetContentsWhenTheNextEntityIsDefined` | ✅ |
| should log an error and skip the param RuleSet when encountered a param RuleSet with a name used by another param RuleSet | `ShouldLogAnErrorAndSkipTheParamRuleSetWhenEncounteredAParamRuleSetWithANameUsedByAnotherParamRuleSet` | ⚠️ semantic validation |
| should log an error and skip the param RuleSet when encountered a param RuleSet with a name used by another param RuleSet in another file | `ShouldLogAnErrorAndSkipTheParamRuleSetWhenEncounteredAnParamRuleSetWithANameUsedByAnotherParamRuleSetInAnotherFile` | ⚠️ multi-file |
| should log a warning when a param RuleSet has parameters that are not used in the contents | `ShouldLogAWarningWhenAParamRuleSetHasParametersThatAreNotUsedInTheContents` | ⚠️ semantic validation |

---

### Profile — `FSHImporter.Profile.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Profile.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.ProfileTests.cs`

Additional differences:
- Profile `Parent`, `Id`, `Title`, `Description` are `Metadata?` objects; use `.Value` to get the string.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Profile | `ShouldParseTheSimplestPossibleProfile` | ✅ |
| should parse Profile with all metadata fields | `ShouldParseProfileWithAllMetadataFields` | ✅ |
| should parse numeric Profile name and parent | `ShouldParseNumericProfileNameAndParent` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip Profile with duplicate name | `ShouldLogAnErrorAndSkipProfileWithDuplicateName` | ⚠️ semantic validation |
| should log an error and skip Profile with duplicate name across files | `ShouldLogAnErrorAndSkipProfileWithDuplicateNameAcrossFiles` | ⚠️ multi-file |
| should log an error when deprecated Mixins keyword is used | `ShouldLogAnErrorWhenDeprecatedMixinsKeywordIsUsed` | ⚠️ semantic validation |
| should parse multiple Profiles | `ShouldParseMultipleProfiles` | ✅ |
| should parse simple card rules | `ShouldParseSimpleCardRules` | ✅ |
| should parse card rules with flags | `ShouldParseCardRulesWithFlags` | ⚠️ card+flag not split |
| should parse single flag rule | `ShouldParseSingleFlagRule` | ✅ |
| should parse multiple flags on single path | `ShouldParseMultipleFlagsOnSinglePath` | ✅ |
| should parse value set binding rule | `ShouldParseValueSetBindingRule` | ✅ |
| should parse value set binding rule without strength | `ShouldParseValueSetBindingRuleWithoutStrength` | ✅ |
| should parse assigned value string rule | `ShouldParseAssignedValueStringRule` | ✅ |
| should parse assigned value string rule with exactly | `ShouldParseAssignedValueStringRuleWithExactly` | ✅ |
| should parse assigned value boolean rule | `ShouldParseAssignedValueBooleanRule` | ⚠️ BooleanValue not produced |
| should parse assigned value code rule | `ShouldParseAssignedValueCodeRule` | ✅ |
| should parse only rule with one type | `ShouldParseOnlyRuleWithOneType` | ✅ |
| should parse only rule with multiple types | `ShouldParseOnlyRuleWithMultipleTypes` | ✅ |
| should parse contains rule with one item | `ShouldParseContainsRuleWithOneItem` | ⚠️ contains+card not split |
| should parse caret value rule with a path | `ShouldParseCaretValueRuleWithAPath` | ✅ |
| should parse caret value rule without a path | `ShouldParseCaretValueRuleWithoutAPath` | ✅ |
| should parse obeys rule with a path | `ShouldParseObeysRuleWithAPath` | ✅ |
| should parse obeys rule without a path | `ShouldParseObeysRuleWithoutAPath` | ✅ |
| should parse obeys rule with multiple invariants | `ShouldParseObeysRuleWithMultipleInvariants` | ⚠️ multi-invariant obeys not split |
| should parse path rule | `ShouldParsePathRule` | ✅ |
| should parse insert rule | `ShouldParseInsertRule` | ✅ |
| should parse insert rule with path | `ShouldParseInsertRuleWithPath` | ✅ |
| should parse mix of rules in Profile | `ShouldParseMixOfRulesInProfile` | ✅ |

---

### Resource — `FSHImporter.Resource.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Resource.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.ResourceTests.cs`

Additional differences:
- `Resource` uses `string?` properties (not `Metadata?`) for `Parent`, `Id`, `Title`, `Description`.
- `CaretValueRule` and `InsertRule` support on `Resource` is limited.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible Resource | `ShouldParseTheSimplestPossibleResource` | ✅ |
| should parse Resource with all metadata fields | `ShouldParseResourceWithAllMetadataFields` | ✅ |
| should parse numeric Resource name, parent, and id | `ShouldParseNumericResourceNameParentAndId` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip Resource with duplicate name | `ShouldLogAnErrorAndSkipResourceWithDuplicateName` | ⚠️ semantic validation |
| should log an error and skip Resource with duplicate name across files | `ShouldLogAnErrorAndSkipResourceWithDuplicateNameAcrossFiles` | ⚠️ multi-file |
| should parse AddElementRule with type and description | `ShouldParseAddElementRuleWithTypeAndDescription` | ✅ |
| should parse AddElementRule with multiple types | `ShouldParseAddElementRuleWithMultipleTypes` | ✅ |
| should parse AddElementRule with short description and definition | `ShouldParseAddElementRuleWithShortDescriptionAndDefinition` | ✅ |
| should parse AddElementRule with flags | `ShouldParseAddElementRuleWithFlags` | ⚠️ card+flag not split |
| should parse card rule for existing element | `ShouldParseCardRuleForExistingElement` | ✅ |
| should parse caret value rule on Resource | `ShouldParseCaretValueRuleOnResource` | ⚠️ CaretValueRule not supported on Resource |
| should parse insert rule on Resource | `ShouldParseInsertRuleOnResource` | ⚠️ InsertRule not supported on Resource |

---

### RuleSet — `FSHImporter.RuleSet.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.RuleSet.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.RuleSetTests.cs`

Additional differences:
- `AddElementRule`: SUSHI sets both `short` and `definition` to the same STRING when only one is provided. fsh-processor sets only `ShortDescription`; `Definition` remains null.
- `Concept.Codes` retains the `#` prefix (e.g. `"ZOO#bear"` or `"#lion"`).
- SUSHI's `MappingRule` fields `comment` → C# `MappingMapRule.Language`; `language` → C# `MappingMapRule.Code`.
- Empty RuleSet causes a parse error (grammar requires at least one rule).

| SUSHI test | C# method | Status |
|---|---|---|
| should parse a RuleSet with a rule | `ShouldParseARuleSetWithARule` | ✅ |
| should parse a RuleSet with a numeric name | `ShouldParseARuleSetWithANumericName` | ✅ |
| should parse a RuleSet with multiple rules | `ShouldParseARuleSetWithMultipleRules` | ✅ |
| should parse a RuleSet with an insert rule | `ShouldParseARuleSetWithAnInsertRule` | ✅ |
| should parse a RuleSet with an AddElementRule | `ShouldParseARuleSetWithAnAddElementRule` | ✅ |
| should parse a RuleSet with a content reference AddElementRule | `ShouldParseARuleSetWithAContentReferenceAddElementRule` | ✅ |
| should parse a RuleSet with a mapping rule | `ShouldParseARuleSetWithAMappingRule` | ✅ |
| should parse a RuleSet with rules, value set components, concept rules, and caret value rules | `ShouldParseARuleSetWithRulesValueSetComponentsConceptRulesAndCaretValueRules` | ✅ |
| should log an error when parsing a RuleSet with no rules | `ShouldLogAnErrorWhenParsingARuleSetWithNoRules` | ⚠️ empty RuleSet → parse error |
| should log an error and skip the RuleSet when encountered a RuleSet with a name used by another RuleSet | `ShouldLogAnErrorAndSkipTheRuleSetWhenEncounteredARuleSetWithANameUsedByAnotherRuleSet` | ⚠️ semantic validation |
| should log an error and skip the RuleSet when encountered a RuleSet with a name used by another RuleSet in another file | `ShouldLogAnErrorAndSkipTheRuleSetWhenEncounteredAnRuleSetWithANameUsedByAnotherRuleSetInAnotherFile` | ⚠️ multi-file |
| should not log an error when concept rule has one code with a system, no definition, and no hierarchy | `ShouldNotLogAnErrorWhenConceptRuleHasOneCodeWithASystemNoDefinitionAndNoHierarchy` | ✅ |

---

### SDRules — `FSHImporter.SDRules.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.SDRules.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.SDRulesTests.cs`

Structure Definition rules tested: `CardRule`, `FlagRule`, `OnlyRule`, `ValueSetRule`, `FixedValueRule`/`AssignmentRule`, `ContainsRule`, `ObeysRule`, `CaretValueRule`, `InsertRule`, `PathRule`.

| SUSHI test | C# method | Status |
|---|---|---|
| should parse simple card rule | `ShouldParseSimpleCardRule` | ✅ |
| should parse card rule with zero max | `ShouldParseCardRuleWithZeroMax` | ✅ |
| should parse card rule with unlimited max | `ShouldParseCardRuleWithUnlimitedMax` | ✅ |
| should parse card rule with combined flags | `ShouldParseCardRuleWithCombinedFlags` | ⚠️ card+flag not split |
| should parse multiple card rules | `ShouldParseMultipleCardRules` | ✅ |
| should parse single path flag rule | `ShouldParseSinglePathFlagRule` | ✅ |
| should parse multiple flags on single path | `ShouldParseMultipleFlagsOnSinglePath` | ✅ |
| should parse must support flag | `ShouldParseMustSupportFlag` | ✅ |
| should parse narrative flag | `ShouldParseNarrativeFlag` | ✅ |
| should parse multiple flag paths | `ShouldParseMultipleFlagPaths` | ✅ |
| should parse value set rule with strength | `ShouldParseValueSetRuleWithStrength` | ✅ |
| should parse value set rule with all strengths | `ShouldParseValueSetRuleWithAllStrengths` | ✅ |
| should parse value set rule without strength | `ShouldParseValueSetRuleWithoutStrength` | ✅ |
| should parse assigned value string rule | `ShouldParseAssignedValueStringRule` | ✅ |
| should parse assigned value string rule with exactly | `ShouldParseAssignedValueStringRuleWithExactly` | ✅ |
| should parse assigned value code rule | `ShouldParseAssignedValueCodeRule` | ✅ |
| should parse assigned value number rule | `ShouldParseAssignedValueNumberRule` | ✅ |
| should parse assigned value boolean rule | `ShouldParseAssignedValueBooleanRule` | ⚠️ BooleanValue not produced |
| should parse assigned value reference rule | `ShouldParseAssignedValueReferenceRule` | ✅ |
| should parse only rule with single type | `ShouldParseOnlyRuleWithSingleType` | ✅ |
| should parse only rule with multiple types | `ShouldParseOnlyRuleWithMultipleTypes` | ✅ |
| should parse contains rule with one item | `ShouldParseContainsRuleWithOneItem` | ⚠️ contains+card not split |
| should parse caret value rule with path | `ShouldParseCaretValueRuleWithPath` | ✅ |
| should parse caret value rule without path | `ShouldParseCaretValueRuleWithoutPath` | ✅ |
| should parse caret value rule with integer value | `ShouldParseCaretValueRuleWithIntegerValue` | ✅ |
| should parse obeys rule with path | `ShouldParseObeysRuleWithPath` | ✅ |
| should parse obeys rule without path | `ShouldParseObeysRuleWithoutPath` | ✅ |
| should parse obeys rule with multiple invariants | `ShouldParseObeysRuleWithMultipleInvariants` | ⚠️ multi-invariant obeys not split |
| should parse path rule | `ShouldParsePathRule` | ✅ |
| should parse insert rule with single RuleSet | `ShouldParseInsertRuleWithSingleRuleSet` | ✅ |
| should parse insert rule with path | `ShouldParseInsertRuleWithPath` | ✅ |
| should parse insert rule with parameterized RuleSet | `ShouldParseInsertRuleWithParameterizedRuleSet` | ✅ |
| should parse mixed SD rules in Profile | `ShouldParseMixedSDRulesInProfile` | ✅ |

---

### ValueSet — `FSHImporter.ValueSet.test.ts`

**SUSHI source:** https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.ValueSet.test.ts  
**C# file:** `fsh-tester/Sushi/Sushi.ValueSetTests.cs`

Additional differences:
- `ConceptCode.Value` in `VsComponentRule` retains the `#` prefix (e.g. `#lion`).

| SUSHI test | C# method | Status |
|---|---|---|
| should parse the simplest possible ValueSet | `ShouldParseTheSimplestPossibleValueSet` | ✅ |
| should parse ValueSet with all metadata fields | `ShouldParseValueSetWithAllMetadataFields` | ✅ |
| should parse numeric ValueSet name and id | `ShouldParseNumericValueSetNameAndId` | ✅ |
| should only apply each metadata attribute the first time it is declared | `ShouldOnlyApplyEachMetadataAttributeTheFirstTimeItIsDeclared` | ⚠️ last-wins vs first-wins |
| should log an error when encountering duplicate metadata attribute | `ShouldLogAnErrorWhenEncounteringDuplicateMetadataAttribute` | ⚠️ semantic validation |
| should log an error and skip ValueSet with duplicate name | `ShouldLogAnErrorAndSkipValueSetWithDuplicateName` | ⚠️ semantic validation |
| should log an error and skip ValueSet with duplicate name across files | `ShouldLogAnErrorAndSkipValueSetWithDuplicateNameAcrossFiles` | ⚠️ multi-file |
| should parse ValueSet with include all from system | `ShouldParseValueSetWithIncludeAllFromSystem` | ✅ |
| should parse ValueSet with exclude all from system | `ShouldParseValueSetWithExcludeAllFromSystem` | ✅ |
| should parse ValueSet with include from ValueSet | `ShouldParseValueSetWithIncludeFromValueSet` | ✅ |
| should parse ValueSet with include concept from system | `ShouldParseValueSetWithIncludeConceptFromSystem` | ✅ |
| should parse ValueSet with multiple includes and excludes | `ShouldParseValueSetWithMultipleIncludesAndExcludes` | ✅ |
| should parse ValueSet with filter | `ShouldParseValueSetWithFilter` | ✅ |
| should parse ValueSet caret value rule | `ShouldParseValueSetCaretValueRule` | ✅ |
| should parse insert rule on ValueSet | `ShouldParseInsertRuleOnValueSet` | ✅ |
| should parse multiple ValueSets | `ShouldParseMultipleValueSets` | ✅ |

---

## Not yet ported

| SUSHI source file | Notes |
|---|---|
| `FSHImporter.Context.test.ts` | Tests for FSH context-path (`[]`) syntax. No C# equivalent exists yet. |

---

## How to update this document when SUSHI changes

1. **Find the changed SUSHI test file.**  Each C# file has a comment at the top with the exact SUSHI
   source path, e.g.:
   ```
   // Ported from SUSHI: test/import/FSHImporter.Alias.test.ts
   // Source: https://github.com/FHIR/sushi/blob/main/test/import/FSHImporter.Alias.test.ts
   ```

2. **Compare the SUSHI test against the C# file.**  Look for:
   - New `it(…)` blocks → add a matching `[TestMethod]` in the C# file (or mark Inconclusive if it
     tests a behaviour in the cross-cutting differences table above).
   - Changed assertions → update the C# assertion accordingly, noting any behavioural difference.
   - Removed tests → remove or comment out the C# method and update this document.

3. **Update the mapping table above** — tick the new tests and adjust the pass/inconclusive counts.

4. **Re-run the test suite** to confirm no regressions:
   ```
   dotnet test fsh-tester/fsh-tester.csproj
   ```

5. **Update the "Counts last updated" line** in the quick-reference table.
