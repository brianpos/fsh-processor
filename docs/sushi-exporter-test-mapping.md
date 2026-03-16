# SUSHI Exporter Test Mapping

This document catalogues every test in the SUSHI `test/export/` suite.
Its purpose is to track which tests have been ported to the C# `fsh-compiler` layer,
and to guide decisions about which tests to tackle next.

> **Note (2026-03-16):** The Firely SDK package loader and Firely SDK snapshot generator
> are available for use in `fsh-compiler`. This resolves two of the five previously-identified
> portability blockers (FHIR package loading and snapshot generation).
> The remaining blockers are: programmatic object-model input vs FSH text, log-spy error
> assertions, and the SUSHI `Package` abstraction having no equivalent.

---

## Contents

- [CodeSystemExporter.test.ts](#codesystemexportertestts) — 59 tests
- [FHIRExporter.test.ts](#fhirexportertestts) — 25 tests
- [InstanceExporter.test.ts](#instanceexportertestts) — 370 tests
- [MappingExporter.test.ts](#mappingexportertestts) — 21 tests
- [Package.test.ts](#packagetestts) — 55 tests
- [StructureDefinition.ExtensionExporter.test.ts](#structuredefinitionextensionexportertestts) — 44 tests
- [StructureDefinition.LogicalExporter.test.ts](#structuredefinitionlogicalexportertestts) — 42 tests
- [StructureDefinition.ProfileExporter.test.ts](#structuredefinitionprofileexportertestts) — 24 tests
- [StructureDefinition.ResourceExporter.test.ts](#structuredefinitionresourceexportertestts) — 28 tests
- [StructureDefinitionExporter.test.ts](#structuredefinitionexportertestts) — 376 tests
- [ValueSetExporter.test.ts](#valuesetexportertestts) — 94 tests
- **Total: 1138 tests**

---

## [`CodeSystemExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/CodeSystemExporter.test.ts)

**59 tests**

### `CodeSystemExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single code system | Export a single code system | |
| should add source info for the exported code system to the package | Add source info for the exported code system to the package | |
| should export a code system with additional metadata | Export a code system with additional metadata | |
| should export a code system with status and version in FSHOnly mode | Export a code system with status and version in FSHOnly mode | |
| should export each code system once, even if export is called more than once | Export each code system once, even if export is called more than once | |
| should export a code system with a concept with only a code | Export a code system with a concept with only a code | |
| should export a code system with a concept with a code, display, and definition | Export a code system with a concept with a code, display, and definition | |
| should export a code system with hierarchical codes | Export a code system with hierarchical codes | |
| should log an error when encountering a duplicate code | Log an error when encountering a duplicate code | |
| should not log an error when encountering a duplicate code if the new code has no display or definition | Not log an error when encountering a duplicate code if the new code has no display or definition | |
| should log an error when encountering a code with an incorrectly defined hierarchy | Log an error when encountering a code with an incorrectly defined hierarchy | |
| should warn when title and/or description is an empty string | Warn when title and/or description is an empty string | |
| should log a message when the code system has an invalid id | Log a message when the code system has an invalid id | |
| should not log a message when the code system overrides an invalid id with a Caret Rule | Not log a message when the code system overrides an invalid id with a Caret Rule | |
| should log a message when the code system overrides an invalid id with an invalid Caret Rule | Log a message when the code system overrides an invalid id with an invalid Caret Rule | |
| should log a message when the code system overrides a valid id with an invalid Caret Rule | Log a message when the code system overrides a valid id with an invalid Caret Rule | |
| should log a message when the code system has an invalid name | Log a message when the code system has an invalid name | |
| should not log a message when the code system overrides an invalid name with a Caret Rule | Not log a message when the code system overrides an invalid name with a Caret Rule | |
| should log a message when the code system overrides an invalid name with an invalid Caret Rule | Log a message when the code system overrides an invalid name with an invalid Caret Rule | |
| should log a message when the code system overrides a valid name with an invalid Caret Rule | Log a message when the code system overrides a valid name with an invalid Caret Rule | |
| should sanitize the id and log a message when a valid name is used to make an invalid id | Sanitize the id and log a message when a valid name is used to make an invalid id | |
| should sanitize the id and log a message when a long valid name is used to make an invalid id | Sanitize the id and log a message when a long valid name is used to make an invalid id | |
| should log an error when multiple code systems have the same id | Log an error when multiple code systems have the same id | |
| should apply a CaretValueRule | Apply a CaretValueRule | |
| should apply a CaretValueRule on a top-level concept | Apply a CaretValueRule on a top-level concept | |
| should apply a CaretValueRule on a concept within a hierarchy | Apply a CaretValueRule on a concept within a hierarchy | |
| should apply a CaretValueRule on a concept that assigns an Instance | Apply a CaretValueRule on a concept that assigns an Instance | |
| should apply a CaretValueRule on a concept that assigns an Instance with a numeric id | Apply a CaretValueRule on a concept that assigns an Instance with a numeric id | |
| should apply a CaretValueRule on a concept that assigns an Instance with an id that resembles a boolean | Apply a CaretValueRule on a concept that assigns an Instance with an id that resembles a boolean | |
| should apply CaretValueRules that create a contained resource | Apply CaretValueRules that create a contained resource | |
| should apply CaretValueRules that modify a contained resource | Apply CaretValueRules that modify a contained resource | |
| should log a warning when applying a CaretValueRule that assigns an example Instance | Log a warning when applying a CaretValueRule that assigns an example Instance | |
| should log a warning when applying a CaretValueRule that assigns an example Instance with a numeric id | Log a warning when applying a CaretValueRule that assigns an example Instance with a numeric id | |
| should replace references when applying a CaretValueRule | Replace references when applying a CaretValueRule | |
| should resolve soft indexing when applying top level Caret Value rules | Resolve soft indexing when applying top level Caret Value rules | |
| should resolve soft indexing when applying CaretValue rules with paths | Resolve soft indexing when applying CaretValue rules with paths | |
| should export a code system with extensions | Export a code system with extensions | |
| should output an error when a choice element has values assigned to more than one choice type | Output an error when a choice element has values assigned to more than one choice type | |
| should not override count when ^count is provided by user | Not override count when ^count is provided by user | |
| should warn when ^count does not match number of concepts in #complete CodeSystem | Warn when ^count does not match number of concepts in #complete CodeSystem | |
| should warn when ^count is set and concepts is null in #complete CodeSystem | Warn when ^count is set and concepts is null in #complete CodeSystem | |
| should not set count when ^content is not #complete | Not set count when ^content is not #complete | |
| should log a message when applying an invalid ConceptRule | Log a message when applying an invalid ConceptRule | |
| should log a message when applying invalid CaretValueRule | Log a message when applying invalid CaretValueRule | |
| should log a message when applying an invalid CaretValueRule | Log a message when applying an invalid CaretValueRule | |
| should log a message when a CaretValueRule assigns an Instance, but the Instance is not found | Log a message when a CaretValueRule assigns an Instance, but the Instance is not found | |
| should log a message when a CaretValueRule assigns a value that is numeric and refers to an Instance, but both types are wrong | Log a message when a CaretValueRule assigns a value that is numeric and refers to an Instance, but both types are wrong | |
| should log a message when a CaretValueRule assigns a value that is boolean and refers to an Instance, but both types are wrong | Log a message when a CaretValueRule assigns a value that is boolean and refers to an Instance, but both types are wrong | |

#### `#insertRules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply rules from an insert rule | Apply rules from an insert rule | |
| should resolve soft indexing when inserting an insert rule | Resolve soft indexing when inserting an insert rule | |
| should insert a rule set at a code path | Insert a rule set at a code path | |
| should update count when applying concepts from an insert rule | Update count when applying concepts from an insert rule | |
| should log an error and not apply rules from an invalid insert rule | Log an error and not apply rules from an invalid insert rule | |
| should maintain concept order when adding concepts from an insert rule | Maintain concept order when adding concepts from an insert rule | |
| should add nested concepts from an insert rule | Add nested concepts from an insert rule | |
| should add nested concepts whose hierarchy is created by an insert rule | Add nested concepts whose hierarchy is created by an insert rule | |
| should not add concepts from an insert rule that are duplicates of existing concepts | Not add concepts from an insert rule that are duplicates of existing concepts | |
| should not add concepts from an insert rule that are duplicates of concepts added by a previous insert rule | Not add concepts from an insert rule that are duplicates of concepts added by a previous insert rule | |

---

## [`FHIRExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/FHIRExporter.test.ts)

**25 tests**

### `FHIRExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |

#### `#containedResources`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should allow a profile to contain a defined FHIR resource | Allow a profile to contain a defined FHIR resource | |
| should allow a profile to contain a FSH resource | Allow a profile to contain a FSH resource | |
| should allow a profile to contain a FSH resource with a numeric id | Allow a profile to contain a FSH resource with a numeric id | |
| should allow a profile to contain a FSH resource with an id that resembles a boolean | Allow a profile to contain a FSH resource with an id that resembles a boolean | |
| should allow a profile to contain multiple FSH resources | Allow a profile to contain multiple FSH resources | |
| should allow a profile to contain a resource and to apply caret rules within the contained resource | Allow a profile to contain a resource and to apply caret rules within the contained resource | |
| should log an error when a deferred rule assigns something of the wrong type | Log an error when a deferred rule assigns something of the wrong type | |
| should not get confused when there are contained resources of different types | Not get confused when there are contained resources of different types | |
| should allow a profile to contain a profiled resource and to apply a caret rule within the contained resource | Allow a profile to contain a profiled resource and to apply a caret rule within the contained resource | |
| should allow a profile to bind an element to a contained ValueSet using a relative reference | Allow a profile to bind an element to a contained ValueSet using a relative reference | |
| should allow a profile to bind an element to a contained inline instance of ValueSet using a relative reference | Allow a profile to bind an element to a contained inline instance of ValueSet using a relative reference | |
| should allow a profile to bind an element to a contained inline instance of ValueSet with name set by a rule, using a relative reference | Allow a profile to bind an element to a contained inline instance of ValueSet with name set by a rule, using a relative reference | |
| should allow a profile to bind an element to a contained inline instance of ValueSet with url set by a rule, using a relative reference | Allow a profile to bind an element to a contained inline instance of ValueSet with url set by a rule, using a relative reference | |
| should allow a profile to bind an element to a contained definitional instance of ValueSet using a relative reference | Allow a profile to bind an element to a contained definitional instance of ValueSet using a relative reference | |
| should allow a profile to bind an element by name to a contained definitional instance of ValueSet with a name set by a rule using a relative reference | Allow a profile to bind an element by name to a contained definitional instance of ValueSet with a name set by a rule using a relative reference | |
| should allow a profile to bind an element to a contained ValueSet using a relative reference when the rule includes a version | Allow a profile to bind an element to a contained ValueSet using a relative reference when the rule includes a version | |
| should log an error when attempting to bind an element to an inline ValueSet instance that is not contained in the profile | Log an error when attempting to bind an element to an inline ValueSet instance that is not contained in the profile | |
| should log an error when a profile tries to contain an instance that is not a resource | Log an error when a profile tries to contain an instance that is not a resource | |
| should log an error when a profile tries to contain a resource that does not exist | Log an error when a profile tries to contain a resource that does not exist | |
| should let a profile assign an Inline instance that is not a resource | Let a profile assign an Inline instance that is not a resource | |
| should let a profile assign and modify an Inline instance that is not a resource | Let a profile assign and modify an Inline instance that is not a resource | |
| should export a value set that includes a component from a contained FSH code system and add the valueset-system extension | Export a value set that includes a component from a contained FSH code system and add the valueset-system extension | |
| should log a message when trying to assign a value that is numeric and refers to an Instance, but both types are wrong | Log a message when trying to assign a value that is numeric and refers to an Instance, but both types are wrong | |
| should log a message and not change the URL when trying to assign an instance to a URL and the instance is not the correct type | Log a message and not change the URL when trying to assign an instance to a URL and the instance is not the correct type | |

---

## [`InstanceExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/InstanceExporter.test.ts)

**370 tests**

### `InstanceExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single instance | Export a single instance | |
| should add source info for the exported instance to the package | Add source info for the exported instance to the package | |
| should export multiple instances | Export multiple instances | |
| should still export instance if one fails | Still export instance if one fails | |
| should log a message with source information when the parent is not found | Log a message with source information when the parent is not found | |
| should log a message with source information when the instanceOf is an abstract specialization | Log a message with source information when the instanceOf is an abstract specialization | |
| should log a message with source information when the instanceOf is a profile whose nearest specialization is abstract | Log a message with source information when the instanceOf is a profile whose nearest specialization is abstract | |
| should warn when title and/or description is an empty string | Warn when title and/or description is an empty string | |
| should export instances with InstanceOf FSHy profile | Export instances with InstanceOf FSHy profile | |
| should assign values on an instance | Assign values on an instance | |

#### `#exportInstance`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set resourceType to the base resource type we are making an instance of | Set resourceType to the base resource type we are making an instance of | |
| should set resourceType to the base resource type for the profile we are making an instance of | Set resourceType to the base resource type for the profile we are making an instance of | |
| should set meta.profile to the defining profile URL we are making an instance of | Set meta.profile to the defining profile URL we are making an instance of | |
| should not set meta.profile when we are making an instance of a base resource | Not set meta.profile when we are making an instance of a base resource | |
| should set meta.profile with the InstanceOf profile before checking for required elements | Set meta.profile with the InstanceOf profile before checking for required elements | |
| should only set meta.profile with one profile when profile is set on the InstanceOf profile | Only set meta.profile with one profile when profile is set on the InstanceOf profile | |
| should add the InstanceOf profile as the first meta.profile if it is not added by any rules | Add the InstanceOf profile as the first meta.profile if it is not added by any rules | |
| should set meta.profile without the unversioned InstanceOf profile if a versioned InstanceOf profile is present | Set meta.profile without the unversioned InstanceOf profile if a versioned InstanceOf profile is present | |
| should keep the unversioned InstanceOf in meta.profile if it is also added by a rule on the profile | Keep the unversioned InstanceOf in meta.profile if it is also added by a rule on the profile | |
| should keep the unversioned InstanceOf in meta.profile if it is also added by a rule on the instance | Keep the unversioned InstanceOf in meta.profile if it is also added by a rule on the instance | |
| should set meta.profile on all instances when setMetaProfile is always | Set meta.profile on all instances when setMetaProfile is always | |
| should set meta.profile on all instances when setMetaProfile is not set | Set meta.profile on all instances when setMetaProfile is not set | |
| should set meta.profile on no instances when setMetaProfile is never | Set meta.profile on no instances when setMetaProfile is never | |
| should set meta.profile on inline instances when setMetaProfile is inline-only | Set meta.profile on inline instances when setMetaProfile is inline-only | |
| should set meta.profile on non-inline instances when setMetaProfile is standalone-only | Set meta.profile on non-inline instances when setMetaProfile is standalone-only | |
| should automatically set the URL property on definition instances | Automatically set the URL property on definition instances | |
| should not automatically set the URL property on definition instances if the URL is set explicitly | Not automatically set the URL property on definition instances if the URL is set explicitly | |
| should not automatically set the URL property on definition instances if the profile does not support URL setting | Not automatically set the URL property on definition instances if the profile does not support URL setting | |
| should set an extension on meta.profile when no rules set values on meta.profile | Set an extension on meta.profile when no rules set values on meta.profile | |
| should set an extension on meta.profile when a rule sets the InstanceOf url on meta.profile | Set an extension on meta.profile when a rule sets the InstanceOf url on meta.profile | |
| should set an extension on meta.profile when a rule sets a non-InstanceOf url on meta.profile | Set an extension on meta.profile when a rule sets a non-InstanceOf url on meta.profile | |
| should set a non-InstanceOf url and an extension on meta.profile at the same non-zero index | Set a non-InstanceOf url and an extension on meta.profile at the same non-zero index | |
| should set InstanceOf and non-InstanceOf urls in meta.profile alongside extensions | Set InstanceOf and non-InstanceOf urls in meta.profile alongside extensions | |
| should keep meta.profile and child elements of meta.profile aligned when removing duplicates from meta.profile | Keep meta.profile and child elements of meta.profile aligned when removing duplicates from meta.profile | |
| should set id to instance name by default | Set id to instance name by default | |
| should overwrite id if it is set by a rule | Overwrite id if it is set by a rule | |
| should log a message when the instance has an invalid id | Log a message when the instance has an invalid id | |
| should sanitize the id and log a message when a valid name is used to make an invalid id | Sanitize the id and log a message when a valid name is used to make an invalid id | |
| should log a message when a long valid name is used to make an invalid id | Log a message when a long valid name is used to make an invalid id | |
| should log an error when multiple instances of the same type have the same id | Log an error when multiple instances of the same type have the same id | |
| should not log an error when multiple instances of different types have the same id | Not log an error when multiple instances of different types have the same id | |
| should not log an error when multiple inline instances of the same type have the same id | Not log an error when multiple inline instances of the same type have the same id | |
| should not log an error when an inline instance and a non-inline instance of the same type have the same id | Not log an error when an inline instance and a non-inline instance of the same type have the same id | |
| should set id on all instances when setId is always | Set id on all instances when setId is always | |
| should set id on all instances when setId is not set | Set id on all instances when setId is not set | |
| should set id on only non-inline instances when setId is standalone-only | Set id on only non-inline instances when setId is standalone-only | |
| should assign top level elements that are assigned by pattern[x] on the Structure Definition | Assign top level elements that are assigned by pattern[x] on the Structure Definition | |
| should assign top level elements that are assigned by fixed[x] on the Structure Definition | Assign top level elements that are assigned by fixed[x] on the Structure Definition | |
| should assign boolean false values that are assigned on the Structure Definition | Assign boolean false values that are assigned on the Structure Definition | |
| should assign numeric 0 values that are assigned on the Structure Definition | Assign numeric 0 values that are assigned on the Structure Definition | |
| should assign top level codes that are assigned on the Structure Definition | Assign top level codes that are assigned on the Structure Definition | |
| should not assign optional elements that are assigned on the Structure Definition | Not assign optional elements that are assigned on the Structure Definition | |
| should assign top level elements to an array even if constrained on the Structure Definition | Assign top level elements to an array even if constrained on the Structure Definition | |
| should assign top level elements that are assigned by a pattern on the Structure Definition | Assign top level elements that are assigned by a pattern on the Structure Definition | |
| should assign a value onto an element that are assigned by a pattern on the Structure Definition | Assign a value onto an element that are assigned by a pattern on the Structure Definition | |
| should assign a value onto slice elements that are assigned by a pattern on the Structure Definition | Assign a value onto slice elements that are assigned by a pattern on the Structure Definition | |
| should assign top level choice elements that are assigned on the Structure Definition | Assign top level choice elements that are assigned on the Structure Definition | |
| should not assign fixed values from value[x] children when a specific choice has not been chosen | Not assign fixed values from value[x] children when a specific choice has not been chosen | |
| should assign fixed values from value[x] children using the correct specific choice property name | Assign fixed values from value[x] children using the correct specific choice property name | |
| should assign fixed values from value[x] children using the correct specific choice property name (primitive edition) | Assign fixed values from value[x] children using the correct specific choice property name (primitive edition) | |
| should assign fixed value[x] correctly and log no errors when multiple choice slices are assigned | Assign fixed value[x] correctly and log no errors when multiple choice slices are assigned | |
| should assign fixed value[x] correctly even in weird situations (SUSHI #760) | Assign fixed value[x] correctly even in weird situations (SUSHI #760) | |
| should assign value[x] to the correct path when the rule on the instance refers to value[x], and value[x] is constrained to one type | Assign value[x] to the correct path when the rule on the instance refers to value[x], and value[x] is constrained to one type | |
| should log an error and not assign to a descendant of a choice element when that choice element has more than one type | Log an error and not assign to a descendant of a choice element when that choice element has more than one type | |
| should assign an element to a value the same as the assigned value on the Structure Definition | Assign an element to a value the same as the assigned value on the Structure Definition | |
| should assign an element to a value the same as the assigned pattern on the Structure Definition | Assign an element to a value the same as the assigned pattern on the Structure Definition | |
| should assign an element to a value that is a superset of the assigned pattern on the Structure Definition | Assign an element to a value that is a superset of the assigned pattern on the Structure Definition | |
| should not assign an element to a value different than the assigned value on the Structure Definition | Not assign an element to a value different than the assigned value on the Structure Definition | |
| should not assign an element to a value different than the pattern value on the Structure Definition | Not assign an element to a value different than the pattern value on the Structure Definition | |
| should assign an element to a value different than the pattern value on the Structure Definition on an array | Assign an element to a value different than the pattern value on the Structure Definition on an array | |
| should assign a nested element that has parents defined in the instance and is assigned on the Structure Definition | Assign a nested element that has parents defined in the instance and is assigned on the Structure Definition | |
| should assign a nested element that has parents and children defined in the instance and is assigned on the Structure Definition | Assign a nested element that has parents and children defined in the instance and is assigned on the Structure Definition | |
| should not assign a nested element that does not have parents defined in the instance | Not assign a nested element that does not have parents defined in the instance | |
| should assign a nested element that has parents defined in the instance and assigned on the SD to an array even if constrained | Assign a nested element that has parents defined in the instance and assigned on the SD to an array even if constrained | |
| should assign a deeply nested element that is assigned on the Structure Definition and has 1..1 parents | Assign a deeply nested element that is assigned on the Structure Definition and has 1..1 parents | |
| should not get confused by matching path parts when assigning deeply nested elements | Not get confused by matching path parts when assigning deeply nested elements | |
| should assign a deeply nested element that is assigned on the Structure Definition and has array parents with min > 1 | Assign a deeply nested element that is assigned on the Structure Definition and has array parents with min > 1 | |
| should assign a deeply nested element that is assigned on the Structure Definition and has slice array parents with min > 1 | Assign a deeply nested element that is assigned on the Structure Definition and has slice array parents with min > 1 | |
| should create additional elements when assigning primitive implied properties from named slices | Create additional elements when assigning primitive implied properties from named slices | |
| should not create additional elements when assigning implied properties from named slices | Not create additional elements when assigning implied properties from named slices | |
| should create additional elements when assigning implied properties if the value on the named slice and on an ancestor element are different | Create additional elements when assigning implied properties if the value on the named slice and on an ancestor element are different | |
| should not create additional elements when assigning implied properties on descdendants of named slices | Not create additional elements when assigning implied properties on descdendants of named slices | |
| should not assign a deeply nested element that is assigned on the Structure Definition but does not have 1..1 parents | Not assign a deeply nested element that is assigned on the Structure Definition but does not have 1..1 parents | |
| should log a warning when assigning a value to an element nested within an element with multiple profiles | Log a warning when assigning a value to an element nested within an element with multiple profiles | |
| should assign a nested element that is assigned by pattern[x] from a parent on the SD | Assign a nested element that is assigned by pattern[x] from a parent on the SD | |
| should assign multiple nested elements that are assigned by pattern[x] from a parent on the SD | Assign multiple nested elements that are assigned by pattern[x] from a parent on the SD | |
| should assign a nested element that is assigned by array pattern[x] from a parent on the SD | Assign a nested element that is assigned by array pattern[x] from a parent on the SD | |
| should assign multiple nested elements that are assigned by array pattern[x] from a parent on the SD | Assign multiple nested elements that are assigned by array pattern[x] from a parent on the SD | |
| should assign elements with soft indexing used within a path | Assign elements with soft indexing used within a path | |
| should only create optional slices that are defined even if sibling in array has more slices than other siblings | Only create optional slices that are defined even if sibling in array has more slices than other siblings | |
| should do the above but with a required slice from the profile | Do the above but with a required slice from the profile | |
| should output no warnings when assigning a value[x] choice type on an extension element | Output no warnings when assigning a value[x] choice type on an extension element | |
| should output an error when a choice element has values assigned to more than one choice type | Output an error when a choice element has values assigned to more than one choice type | |
| should output an error when a choice element has values assigned to more than one choice type, some of which are a complex type | Output an error when a choice element has values assigned to more than one choice type, some of which are a complex type | |
| should not output an error when a multiple-cardinality choice element has different types at different indices | Not output an error when a multiple-cardinality choice element has different types at different indices | |
| should output an error when a choice element within another element has values assigned to more than one choice type | Output an error when a choice element within another element has values assigned to more than one choice type | |
| should output an error when a choice element that is a descendant of a primitive has values assigned to more than one type | Output an error when a choice element that is a descendant of a primitive has values assigned to more than one type | |
| should assign cardinality 1..n elements that are assigned by array pattern[x] from a parent on the SD | Assign cardinality 1..n elements that are assigned by array pattern[x] from a parent on the SD | |
| should assign children of primitive values on an instance | Assign children of primitive values on an instance | |
| should assign primitive values and their children on an instance | Assign primitive values and their children on an instance | |
| should assign children of primitive value arrays on an instance | Assign children of primitive value arrays on an instance | |
| should assign extensions and values on out-of-order elements on a primitive array | Assign extensions and values on out-of-order elements on a primitive array | |
| should assign children of primitive value arrays on an instance with out of order rules | Assign children of primitive value arrays on an instance with out of order rules | |
| should assign children of sliced primitive arrays on an instance | Assign children of sliced primitive arrays on an instance | |
| should assign a reference while resolving the Instance of a resource being referred to | Assign a reference while resolving the Instance of a resource being referred to | |
| should assign a reference while resolving the Instance of a profile being referred to | Assign a reference while resolving the Instance of a profile being referred to | |
| should assign a reference while resolving the profile being referred to | Assign a reference while resolving the profile being referred to | |
| should assign a reference while resolving the non-FSH profile being referred to | Assign a reference while resolving the non-FSH profile being referred to | |
| should log warning when reference values do not resolve and is not an absolute or relative URL | Log warning when reference values do not resolve and is not an absolute or relative URL | |
| should not log warning when reference values do not resolve and is a UUID or OID | Not log warning when reference values do not resolve and is a UUID or OID | |
| should not log warning when reference values do not resolve and is a relative URL with correct number of parts | Not log warning when reference values do not resolve and is a relative URL with correct number of parts | |
| should not log warning when reference values do not resolve and is a relative URL but has more than two parts | Not log warning when reference values do not resolve and is a relative URL but has more than two parts | |
| should not log warning when reference values are an absolute URL | Not log warning when reference values are an absolute URL | |
| should assign a reference leaving the full profile URL when it is specified | Assign a reference leaving the full profile URL when it is specified | |
| should assign a reference while resolving the Extension being referred to | Assign a reference while resolving the Extension being referred to | |
| should assign a reference while resolving the non-FSH extension being referred to | Assign a reference while resolving the non-FSH extension being referred to | |
| should assign a reference leaving the full extension URL when it is specified | Assign a reference leaving the full extension URL when it is specified | |
| should assign a reference while resolving the Logical being referred to | Assign a reference while resolving the Logical being referred to | |
| should assign a reference while resolving the non-FSH logical being referred to | Assign a reference while resolving the non-FSH logical being referred to | |
| should assign a reference leaving the full logical URL when it is specified | Assign a reference leaving the full logical URL when it is specified | |
| should assign a reference while resolving the FSH Resource being referred to | Assign a reference while resolving the FSH Resource being referred to | |
| should assign a reference while resolving the non-FSH resource being referred to | Assign a reference while resolving the non-FSH resource being referred to | |
| should assign a reference leaving the full resource URL when it is specified | Assign a reference leaving the full resource URL when it is specified | |
| should assign a reference while resolving the CodeSystem being referred to | Assign a reference while resolving the CodeSystem being referred to | |
| should assign a reference while resolving the non-FSH CodeSystem being referred to | Assign a reference while resolving the non-FSH CodeSystem being referred to | |
| should assign a reference leaving the full CodeSystem URL when it is specified | Assign a reference leaving the full CodeSystem URL when it is specified | |
| should assign a reference while resolving the ValueSet being referred to | Assign a reference while resolving the ValueSet being referred to | |
| should assign a reference while resolving the non-FSH ValueSet being referred to | Assign a reference while resolving the non-FSH ValueSet being referred to | |
| should assign a reference leaving the full ValueSet URL when it is specified | Assign a reference leaving the full ValueSet URL when it is specified | |
| should assign a reference to a contained instance using a fragment reference | Assign a reference to a contained instance using a fragment reference | |
| should assign a reference to a contained Profile using a fragment reference | Assign a reference to a contained Profile using a fragment reference | |
| should assign a reference to a contained non-FSH profile using a fragment reference | Assign a reference to a contained non-FSH profile using a fragment reference | |
| should assign a full URL reference to a contained non-FSH profile using a fragment reference | Assign a full URL reference to a contained non-FSH profile using a fragment reference | |
| should assign a reference to a contained Extension using a fragment reference | Assign a reference to a contained Extension using a fragment reference | |
| should assign a reference to a contained non-FSH extension using a fragment reference | Assign a reference to a contained non-FSH extension using a fragment reference | |
| should assign a full URL reference to a contained non-FSH extension using a fragment reference | Assign a full URL reference to a contained non-FSH extension using a fragment reference | |
| should assign a reference to a contained Logical using a fragment reference | Assign a reference to a contained Logical using a fragment reference | |
| should assign a reference to a contained non-FSH logical using a fragment reference | Assign a reference to a contained non-FSH logical using a fragment reference | |
| should assign a full URL reference to a contained non-FSH logical using a fragment reference | Assign a full URL reference to a contained non-FSH logical using a fragment reference | |
| should assign a reference to a contained Resource using a fragment reference | Assign a reference to a contained Resource using a fragment reference | |
| should assign a reference to a contained non-FSH resource using a fragment reference | Assign a reference to a contained non-FSH resource using a fragment reference | |
| should assign a full URL reference to a contained non-FSH resource using a fragment reference | Assign a full URL reference to a contained non-FSH resource using a fragment reference | |
| should assign a reference to a contained CodeSystem using a fragment reference | Assign a reference to a contained CodeSystem using a fragment reference | |
| should assign a reference to a contained non-FSH CodeSystem using a fragment reference | Assign a reference to a contained non-FSH CodeSystem using a fragment reference | |
| should assign a full URL reference to a contained non-FSH CodeSystem using a fragment reference | Assign a full URL reference to a contained non-FSH CodeSystem using a fragment reference | |
| should assign a reference to a contained ValueSet using a fragment reference | Assign a reference to a contained ValueSet using a fragment reference | |
| should assign a reference to a contained non-FSH ValueSet using a fragment reference | Assign a reference to a contained non-FSH ValueSet using a fragment reference | |
| should assign a full URL reference to a contained non-FSH ValueSet using a fragment reference | Assign a full URL reference to a contained non-FSH ValueSet using a fragment reference | |
| should not convert non-reference values to contained fragment references | Not convert non-reference values to contained fragment references | |
| should assign a reference without replacing if the referred Instance does not exist | Assign a reference without replacing if the referred Instance does not exist | |
| should assign a reference to a type based on a profile | Assign a reference to a type based on a profile | |
| should assign a reference when the type has no targetProfile | Assign a reference when the type has no targetProfile | |
| should log a warning and ignore the version when assigning a reference that contains a version | Log a warning and ignore the version when assigning a reference that contains a version | |
| should log an error when an invalid reference is assigned | Log an error when an invalid reference is assigned | |
| should log an error when assigning an invalid reference to a type based on a profile | Log an error when assigning an invalid reference to a type based on a profile | |
| should assign a reference to a child type of the referenced type | Assign a reference to a child type of the referenced type | |
| should log an error if an instance of a parent type is assigned | Log an error if an instance of a parent type is assigned | |
| should apply an Assignment rule with a valid Canonical entity defined in FSH | Apply an Assignment rule with a valid Canonical entity defined in FSH | |
| should apply an Assignment rule with Canonical of a FHIR entity | Apply an Assignment rule with Canonical of a FHIR entity | |
| should apply an Assignment rule with Canonical of a Questionnaire instance | Apply an Assignment rule with Canonical of a Questionnaire instance | |
| should apply an Assignment rule with Canonical of an inline instance | Apply an Assignment rule with Canonical of an inline instance | |
| should apply an Assignment rule with Canonical of an instance that has its url assigned by a RuleSet | Apply an Assignment rule with Canonical of an instance that has its url assigned by a RuleSet | |
| should not apply an Assignment rule with an invalid Canonical entity and log an error | Not apply an Assignment rule with an invalid Canonical entity and log an error | |
| should assign a Canonical that is one of the valid types | Assign a Canonical that is one of the valid types | |
| should assign a Canonical that is one of the valid types (without checking the version) when the type is versioned | Assign a Canonical that is one of the valid types (without checking the version) when the type is versioned | |
| should assign a Canonical that is a child of the valid types | Assign a Canonical that is a child of the valid types | |
| should assign the right matching Canonical when the Canonical lookup matches multiple types | Assign the right matching Canonical when the Canonical lookup matches multiple types | |
| should assign a Canonical as a #id fragment when referring to a contained resource created as a ValueSet entity | Assign a Canonical as a #id fragment when referring to a contained resource created as a ValueSet entity | |
| should assign a Canonical as a #id fragment when referring to a contained resource created directly on the instance | Assign a Canonical as a #id fragment when referring to a contained resource created directly on the instance | |
| should assign a Canonical as a #id fragment when referring to a contained resource that was added by slice name, slice name with index, and double digit indices | Assign a Canonical as a #id fragment when referring to a contained resource that was added by slice name, slice name with index, and double digit indices | |
| should assign a Canonical as a full url (not #id) when referring to a resource that is not directly on the contained array | Assign a Canonical as a full url (not #id) when referring to a resource that is not directly on the contained array | |
| should log an error when an invalid canonical is assigned | Log an error when an invalid canonical is assigned | |
| should log an error when an already exported invalid canonical is assigned | Log an error when an already exported invalid canonical is assigned | |
| should assign a code to a top level element while replacing the local code system name with its url | Assign a code to a top level element while replacing the local code system name with its url | |
| should assign a code with a version to a top level element while replacing the local code system name with its url and use the specified version | Assign a code with a version to a top level element while replacing the local code system name with its url and use the specified version | |
| should assign a code with a version to a top level element while replacing the code system name with its url when the correct version is found | Assign a code with a version to a top level element while replacing the code system name with its url when the correct version is found | |
| should assign a code with a version while replacing the code system name with its url regardless of the specified version | Assign a code with a version while replacing the code system name with its url regardless of the specified version | |
| should assign a code to a top level element if the code system was defined as an instance of usage definition | Assign a code to a top level element if the code system was defined as an instance of usage definition | |
| should not assign a code to a top level element if the system references an instance that is not a CodeSystem | Not assign a code to a top level element if the system references an instance that is not a CodeSystem | |
| should not assign a code to a top level element if the code system was defined as an instance of a non-definition usage | Not assign a code to a top level element if the code system was defined as an instance of a non-definition usage | |
| should assign a code to a nested element while replacing the local code system name with its url | Assign a code to a nested element while replacing the local code system name with its url | |
| should assign a code from a CodeSystem in the fisher by id | Assign a code from a CodeSystem in the fisher by id | |
| should assign a code from a CodeSystem in the fisher by name | Assign a code from a CodeSystem in the fisher by name | |
| should assign a code from a CodeSystem in the fisher by url | Assign a code from a CodeSystem in the fisher by url | |
| should assign a Quantity with value 0 (and not drop the 0) | Assign a Quantity with value 0 (and not drop the 0) | |
| should assign a Quantity to a Quantity specialization | Assign a Quantity to a Quantity specialization | |
| should assign a single sliced element to a value | Assign a single sliced element to a value | |
| should assign a single primitive sliced element to a value | Assign a single primitive sliced element to a value | |
| should assign sliced elements in an array that are assigned in order | Assign sliced elements in an array that are assigned in order | |
| should assign a sliced primitive array | Assign a sliced primitive array | |
| should assign a sliced element in an array that is assigned by multiple rules | Assign a sliced element in an array that is assigned by multiple rules | |
| should assign sliced elements in an array that are assigned out of order | Assign sliced elements in an array that are assigned out of order | |
| should assign sliced elements in an array and fill empty values | Assign sliced elements in an array and fill empty values | |
| should assign mixed sliced elements in an array out of order | Assign mixed sliced elements in an array out of order | |
| should assign mixed sliced elements in a deeper array element out of order | Assign mixed sliced elements in a deeper array element out of order | |
| should keep slices in usage order after the first used slice, followed by all required slices, when slices have non-required parents | Keep slices in usage order after the first used slice, followed by all required slices, when slices have non-required parents | |
| should provide a different warning when an author creates an item matching a slice without using the sliceName in the path when manual slice mode is OFF | Provide a different warning when an author creates an item matching a slice without using the sliceName in the path when manual slice mode is OFF | |
| should provide a different warning when an author creates an item exactly matching a slice without using the sliceName in the path when manual slice mode is OFF | Provide a different warning when an author creates an item exactly matching a slice without using the sliceName in the path when manual slice mode is OFF | |
| should assign a sliced extension element that is referred to by name | Assign a sliced extension element that is referred to by name | |
| should assign a nested sliced extension element that is referred to by name | Assign a nested sliced extension element that is referred to by name | |
| should assign a sliced extension element that is referred to by url | Assign a sliced extension element that is referred to by url | |
| should assign a sliced extension element that is referred to by aliased url | Assign a sliced extension element that is referred to by aliased url | |
| should assign an extension that is defined but not present on the SD | Assign an extension that is defined but not present on the SD | |
| should not assign an extension that is not defined and not present on the SD | Not assign an extension that is not defined and not present on the SD | |
| should log an error when a modifier extension is assigned to an extension path | Log an error when a modifier extension is assigned to an extension path | |
| should log an error when a non-modifier extension is assigned to a modifierExtension path | Log an error when a non-modifier extension is assigned to a modifierExtension path | |
| should log an error when a modifier extension is used on an extension element as part of a longer path | Log an error when a modifier extension is used on an extension element as part of a longer path | |
| should log an error when a modifier extension is used on an extension element in the middle of a path | Log an error when a modifier extension is used on an extension element in the middle of a path | |
| should log an error when a non-modifier extension is used on a modifierExtension element as part of a longer path | Log an error when a non-modifier extension is used on a modifierExtension element as part of a longer path | |
| should assign a child of a contentReference element | Assign a child of a contentReference element | |
| should assign a child of a contentReference element in a logical model | Assign a child of a contentReference element in a logical model | |
| should log an error when a required element is not present | Log an error when a required element is not present | |
| should log multiple errors when multiple required elements are not present | Log multiple errors when multiple required elements are not present | |
| should log an error when an element required by an incomplete assigned parent is not present | Log an error when an element required by an incomplete assigned parent is not present | |
| should log an error for a parent only when a required parent is not present | Log an error for a parent only when a required parent is not present | |
| should log an error when an array does not have all required elements | Log an error when an array does not have all required elements | |
| should log an error multiple times for an element missing required elements in an array | Log an error multiple times for an element missing required elements in an array | |
| should log an error when an [x] element is not present | Log an error when an [x] element is not present | |
| should not log an error when an [x] element is present | Not log an error when an [x] element is present | |
| should log an error when a required sliced element is not present | Log an error when a required sliced element is not present | |
| should not log an error when a required sliced element could be satisfied by elements without a sliceName | Not log an error when a required sliced element could be satisfied by elements without a sliceName | |
| should log an error when a required element inherited from a resource is not present | Log an error when a required element inherited from a resource is not present | |
| should log an error when a required element inherited on a profile is not present | Log an error when a required element inherited on a profile is not present | |
| should not log an error when a required choice element has an extension on a complex type choice | Not log an error when a required choice element has an extension on a complex type choice | |
| should not log an error when a required choice element has an extension on a primitive type choice | Not log an error when a required choice element has an extension on a primitive type choice | |
| should log an error when a required primitive child element is not present | Log an error when a required primitive child element is not present | |
| should not log an error when a required primitive child element is present | Not log an error when a required primitive child element is present | |
| should log an error when a required primitive child array is not large enough | Log an error when a required primitive child array is not large enough | |
| should not log an error when a required primitive child array is large enough | Not log an error when a required primitive child array is large enough | |
| should not log an error when a required primitive value element is present on the parent primitive | Not log an error when a required primitive value element is present on the parent primitive | |
| should not log an error when a required primitive value element is present on the parent array primitive | Not log an error when a required primitive value element is present on the parent array primitive | |
| should log an error when a required primitive value element is not present on the parent primitive | Log an error when a required primitive value element is not present on the parent primitive | |
| should log an error when a required primitive value element is missing on the first element of a parent array primitive | Log an error when a required primitive value element is missing on the first element of a parent array primitive | |
| should log an error when a required primitive value element is missing on the parent sliced array primitive | Log an error when a required primitive value element is missing on the parent sliced array primitive | |
| should not log an error when a connected element fulfills the cardinality constraint | Not log an error when a connected element fulfills the cardinality constraint | |
| should properly validate slices with child elements of differing cardinalities | Properly validate slices with child elements of differing cardinalities | |
| should log a warning when a pre-loaded element in a sliced array is accessed with a numeric index | Log a warning when a pre-loaded element in a sliced array is accessed with a numeric index | |
| should log a warning when the child of a pre-loaded element in a sliced array is accessed with a numeric index | Log a warning when the child of a pre-loaded element in a sliced array is accessed with a numeric index | |
| should log a warning when any element in a closed sliced array is accessed with a numeric index | Log a warning when any element in a closed sliced array is accessed with a numeric index | |
| should log a warning when a choice element has its cardinality satisfied, but an ancestor of the choice element is a named slice that is referenced numerically | Log a warning when a choice element has its cardinality satisfied, but an ancestor of the choice element is a named slice that is referenced numerically | |
| should not log a warning when a choice element with one type has its cardinality satisfied by a rule that includes the name of an ancestor slice | Not log a warning when a choice element with one type has its cardinality satisfied by a rule that includes the name of an ancestor slice | |
| should not log an error when a reslice element fulfills a cardinality constraint | Not log an error when a reslice element fulfills a cardinality constraint | |
| should create the correct number of required elements on a resliced element | Create the correct number of required elements on a resliced element | |
| should create the correct number of required elements on a resliced element when required slices are greater than required reslices | Create the correct number of required elements on a resliced element when required slices are greater than required reslices | |
| should create the correct number of required elements on a resliced element when required elements are greater than required slices and reslices | Create the correct number of required elements on a resliced element when required elements are greater than required slices and reslices | |
| should not assign a value which violates a closed child slicing | Not assign a value which violates a closed child slicing | |
| should assign a value which does not violate all elements of a closed child slicing | Assign a value which does not violate all elements of a closed child slicing | |
| should assign a value which violates an open child slicing | Assign a value which violates an open child slicing | |
| should overwrite optional slice values when a numeric index refers to a slice before the end of a path | Overwrite optional slice values when a numeric index refers to a slice before the end of a path | |
| should only export an instance once | Only export an instance once | |
| should only add optional children of list elements and the implied elements of those children to entries in the list that assign values on those children | Only add optional children of list elements and the implied elements of those children to entries in the list that assign values on those children | |
| should set optional extensions on array elements with 1..* card as assigned without implying additional optional extensions | Set optional extensions on array elements with 1..* card as assigned without implying additional optional extensions | |
| should handle extensions on non-zero element of primitive arrays | Handle extensions on non-zero element of primitive arrays | |
| should keep additional values assigned directly on a sibling path before assigning a value with Reference() | Keep additional values assigned directly on a sibling path before assigning a value with Reference() | |
| should keep additional values assigned directly on a sibling but prefer later values when assigning a value with Reference() | Keep additional values assigned directly on a sibling but prefer later values when assigning a value with Reference() | |
| should not allow path rules to be used to define a specific order of items in an array in classic slicing mode | Not allow path rules to be used to define a specific order of items in an array in classic slicing mode | |
| should add assigned values of optional elements when a path rule is used | Add assigned values of optional elements when a path rule is used | |
| should add assigned values of required children of optional element when a path rule is used | Add assigned values of required children of optional element when a path rule is used | |
| should not overwrite fixed values when a path rule is used later | Not overwrite fixed values when a path rule is used later | |

#### `InstanceExporter > #exportInstance > Issue #1559 Bug Fix`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should throw Error when requested version is not in scope | Throw Error when requested version is not in scope | |
| should set meta.profile (non-existent meta) to the defining profile canonical URL with profile name and canonical version | Set meta.profile (non-existent meta) to the defining profile canonical URL with profile name and canonical version | |
| should set meta.profile (non-existent meta) to the defining profile canonical URL with version | Set meta.profile (non-existent meta) to the defining profile canonical URL with version | |
| should set meta.profile (only meta.id) to the defining profile URL with canonical version | Set meta.profile (only meta.id) to the defining profile URL with canonical version | |
| should set meta.profile (single meta.profile) to the defining profile URL with canonical version | Set meta.profile (single meta.profile) to the defining profile URL with canonical version | |
| should set meta.profile (multiple meta.profile) to the defining profile URL with canonical version | Set meta.profile (multiple meta.profile) to the defining profile URL with canonical version | |
| should set meta.profile (non-existent meta) to the proper profile canonical URL with version for which there are two different versions of the profile in scope | Set meta.profile (non-existent meta) to the proper profile canonical URL with version for which there are two different versions of the profile in scope | |

#### `InstanceExporter > #exportInstance > strict slice name usage`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should assign elements with soft indexing and named slices used in combination when enforcing strict slice name usage | Assign elements with soft indexing and named slices used in combination when enforcing strict slice name usage | |
| should assign elements with implied values on required slices when enforcing strict slice name usage | Assign elements with implied values on required slices when enforcing strict slice name usage | |
| should create the correct number of required slices when enforcing strict slice name usage | Create the correct number of required slices when enforcing strict slice name usage | |
| should create the correct number of required elements without slice names when enforcing strict slice name usage | Create the correct number of required elements without slice names when enforcing strict slice name usage | |
| should create required slices when rules use out-of-order indices when enforcing strict slice name usage | Create required slices when rules use out-of-order indices when enforcing strict slice name usage | |
| should assign mixed sliced elements in an array when enforcing strict slice name usage | Assign mixed sliced elements in an array when enforcing strict slice name usage | |
| should output no warnings when assigning a value[x] choice type on an extension element when enforcing strict slice name usage | Output no warnings when assigning a value[x] choice type on an extension element when enforcing strict slice name usage | |
| should warn when an author creates an item loosely matching a slice without using the sliceName in the path | Warn when an author creates an item loosely matching a slice without using the sliceName in the path | |
| should truncate long values when it warns an author about an item loosely matching a slice without using the sliceName in the path | Truncate long values when it warns an author about an item loosely matching a slice without using the sliceName in the path | |
| should warn when an author creates an item loosely matching a slice (with extra sub-array values) without using the sliceName in the path | Warn when an author creates an item loosely matching a slice (with extra sub-array values) without using the sliceName in the path | |
| should warn when an author creates an item loosely matching a slice (with sub-array items in different order) without using the sliceName in the path | Warn when an author creates an item loosely matching a slice (with sub-array items in different order) without using the sliceName in the path | |
| should warn when an author creates an item loosely matching a slice (on non-array properties) without using the sliceName in the path | Warn when an author creates an item loosely matching a slice (on non-array properties) without using the sliceName in the path | |
| should warn when an author creates an item exactly matching a slice without using the sliceName in the path | Warn when an author creates an item exactly matching a slice without using the sliceName in the path | |
| should warn when an author creates an item exactly matching a slice (on non-array properties) without using the sliceName in the path | Warn when an author creates an item exactly matching a slice (on non-array properties) without using the sliceName in the path | |
| should warn when an author creates an item exactly matching a slice (and not matching others) without using the sliceName in the path | Warn when an author creates an item exactly matching a slice (and not matching others) without using the sliceName in the path | |
| should warn when an author creates an item exactly matching a slice and superset matching another slice without using the sliceName in the path | Warn when an author creates an item exactly matching a slice and superset matching another slice without using the sliceName in the path | |
| should NOT warn when an author creates an item partially matching a slice without using the sliceName in the path | NOT warn when an author creates an item partially matching a slice without using the sliceName in the path | |
| should NOT warn when an author creates an item matching a slice but missing an array item without using the sliceName in the path | NOT warn when an author creates an item matching a slice but missing an array item without using the sliceName in the path | |
| should NOT warn when an author creates an item superset matching a slice and correctly uses the sliceName in the path | NOT warn when an author creates an item superset matching a slice and correctly uses the sliceName in the path | |
| should NOT warn when an author creates an item exactly matching a slice and correctly uses the sliceName in the path | NOT warn when an author creates an item exactly matching a slice and correctly uses the sliceName in the path | |
| should allow path rules to be used to define a specific order of items in an array in manual slicing mode | Allow path rules to be used to define a specific order of items in an array in manual slicing mode | |
| should not add null values with path rules | Not add null values with path rules | |
| should add an entry for each index used in a path rule | Add an entry for each index used in a path rule | |
| should replace an array element with null when all other properties are replaced | Replace an array element with null when all other properties are replaced | |
| should assign extensions on elements of a primitive array | Assign extensions on elements of a primitive array | |
| should assign extensions on elements of a primitive array when extensions are assigned before the values | Assign extensions on elements of a primitive array when extensions are assigned before the values | |
| should assign extensions and values on out-of-order elements on a primitive array | Assign extensions and values on out-of-order elements on a primitive array | |
| should assign extensions and values on out-of-order elements on a primitive array when extensions are assigned before values | Assign extensions and values on out-of-order elements on a primitive array when extensions are assigned before values | |
| should assign values and extensions on elements of a primitive array at the same index | Assign values and extensions on elements of a primitive array at the same index | |
| should assign extensions on elements of a sliced primitive array | Assign extensions on elements of a sliced primitive array | |
| should log an error when a required primitive value element is missing on the second element of a parent array primitive, with strict slice ordering enabled | Log an error when a required primitive value element is missing on the second element of a parent array primitive, with strict slice ordering enabled | |

#### `InstanceExporter > #exportInstance > #TimeTravelingResources`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should export a R5 ActorDefinition in a R4 IG | Export a R5 ActorDefinition in a R4 IG | |
| should export a R5 Requirements in a R4 IG | Export a R5 Requirements in a R4 IG | |
| should export a R5 SubscriptionTopic in a R4 IG | Export a R5 SubscriptionTopic in a R4 IG | |
| should export a R5 TestPlan w/ a CodeableReference in a R4 IG | Export a R5 TestPlan w/ a CodeableReference in a R4 IG | |
| should NOT export a R5 NutritionProduct in a R4 IG | NOT export a R5 NutritionProduct in a R4 IG | |

#### `InstanceExporter > #exportInstance > #Logical Models`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set resourceType to the logical type we are making an instance of | Set resourceType to the logical type we are making an instance of | |
| should set resourceType to the logical type for the profile of a logical we are making an instance of | Set resourceType to the logical type for the profile of a logical we are making an instance of | |
| should not set meta.profile when we are making an instance of a logical | Not set meta.profile when we are making an instance of a logical | |
| should not set meta.profile when we are making an instance of a logical even when it has meta | Not set meta.profile when we are making an instance of a logical even when it has meta | |
| should not set meta.profile when we are making an instance of a profile of logical that has no meta | Not set meta.profile when we are making an instance of a profile of logical that has no meta | |
| should set meta.profile to the defining profile URL we are making an instance of logical (for profile of logical that has meta) | Set meta.profile to the defining profile URL we are making an instance of logical (for profile of logical that has meta) | |
| should not set meta.profile when we are making an instance of a profile of a logical with >1 meta | Not set meta.profile when we are making an instance of a profile of a logical with >1 meta | |
| should not set meta.profile when we are making an instance of a profile that constrains >1 meta to 1 meta | Not set meta.profile when we are making an instance of a profile that constrains >1 meta to 1 meta | |
| should not set id for logicals without id element | Not set id for logicals without id element | |
| should set id to instance name for logicals with inherited id element | Set id to instance name for logicals with inherited id element | |
| should set id to instance name for logicals with new id element | Set id to instance name for logicals with new id element | |
| should not set id for logical with >1 id element | Not set id for logical with >1 id element | |
| should not set id for logical with profile constraining >1 id to 1 id | Not set id for logical with profile constraining >1 id to 1 id | |
| should export simple assignment rules for a logical model | Export simple assignment rules for a logical model | |
| should export fixed values and assignment rules for a profile of a logical model | Export fixed values and assignment rules for a profile of a logical model | |

#### `InstanceExporter > #exportInstance > #Inline Instances`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should assign an inline resource to an instance | Assign an inline resource to an instance | |
| should assign multiple inline resources to an instance | Assign multiple inline resources to an instance | |
| should assign other resources to an instance | Assign other resources to an instance | |
| should assign an inline resource to an instance element with a specific type | Assign an inline resource to an instance element with a specific type | |
| should assign an inline resource to an instance element with a choice type | Assign an inline resource to an instance element with a choice type | |
| should assign an inline resource that is not the first type to an instance element with a choice type | Assign an inline resource that is not the first type to an instance element with a choice type | |
| should assign an inline resource to an instance when the resource is not a profile and uses meta | Assign an inline resource to an instance when the resource is not a profile and uses meta | |
| should log an error when assigning an inline resource to an invalid choice | Log an error when assigning an inline resource to an invalid choice | |
| should log an error when assigning an inline resource that does not exist to an instance | Log an error when assigning an inline resource that does not exist to an instance | |
| should override an assigned inline resource on an instance | Override an assigned inline resource on an instance | |
| should override an assigned via resourceType inline resource on an instance | Override an assigned via resourceType inline resource on an instance | |
| should override an assigned inline resource on an instance with paths that mix usage of [0] indexing | Override an assigned inline resource on an instance with paths that mix usage of [0] indexing | |
| should override an assigned via resourceType inline resource on an instance with paths that mix usage of [0] indexing | Override an assigned via resourceType inline resource on an instance with paths that mix usage of [0] indexing | |
| should override a nested assigned inline resource on an instance | Override a nested assigned inline resource on an instance | |
| should override an inline profile on an instance | Override an inline profile on an instance | |
| should assign an inline instance of a type to an instance | Assign an inline instance of a type to an instance | |
| should assign an inline instance of a specialization of a type to an instance | Assign an inline instance of a specialization of a type to an instance | |
| should not overwrite the value property when assigning a Quantity object | Not overwrite the value property when assigning a Quantity object | |
| should assign an inline instance of a profile of a type to an instance | Assign an inline instance of a profile of a type to an instance | |
| should assign an inline instance of a FSH defined profile of a type to an instance | Assign an inline instance of a FSH defined profile of a type to an instance | |
| should assign an inline instance of an extension to an instance | Assign an inline instance of an extension to an instance | |
| should assign an inline instance with a numeric id | Assign an inline instance with a numeric id | |
| should log a warning and assign an example instance within a definition instance | Log a warning and assign an example instance within a definition instance | |
| should log a warning and assign an example instance with a numeric id within a definition instance | Log a warning and assign an example instance with a numeric id within a definition instance | |
| should assign an inline instance with an id that resembles a boolean | Assign an inline instance with an id that resembles a boolean | |
| should log a message when trying to assign a value that is numeric and refers to an Instance, but both types are wrong | Log a message when trying to assign a value that is numeric and refers to an Instance, but both types are wrong | |
| should log a message when trying to assign a value that is boolean and refers to an Instance, but both types are wrong | Log a message when trying to assign a value that is boolean and refers to an Instance, but both types are wrong | |
| should assign an instance that matches existing values | Assign an instance that matches existing values | |
| should log an error when assigning an instance that would overwrite an existing value | Log an error when assigning an instance that would overwrite an existing value | |
| should log an error when assigning an instance with a numeric id that would overwrite an existing value | Log an error when assigning an instance with a numeric id that would overwrite an existing value | |
| should assign an instance of a type to an instance and log a warning when the type is not inline | Assign an instance of a type to an instance and log a warning when the type is not inline | |
| should assign an inline instance of a primitive to a primitive element | Assign an inline instance of a primitive to a primitive element | |
| should assign an inline instance of a primitive with additional properties to a primitive element | Assign an inline instance of a primitive with additional properties to a primitive element | |

#### `#export`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should still apply valid rules if one fails | Still apply valid rules if one fails | |
| should log a message when the path for a assigned value is not found | Log a message when the path for a assigned value is not found | |
| should log a warning when exporting an instance of a custom resource | Log a warning when exporting an instance of a custom resource | |
| should log a warning when exporting multiple instances of custom resources | Log a warning when exporting multiple instances of custom resources | |
| should NOT log a warning when exporting an instance of a logical model | NOT log a warning when exporting an instance of a logical model | |

#### `#insertRules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply rules from an insert rule | Apply rules from an insert rule | |
| should assign elements from a rule set with soft indexing used within a path | Assign elements from a rule set with soft indexing used within a path | |
| should log an error and not apply rules from an invalid insert rule | Log an error and not apply rules from an invalid insert rule | |
| should populate title and description when specified for instances with #definition | Populate title and description when specified for instances with #definition | |
| should not populate title and description when specified for instances that aren't #definition | Not populate title and description when specified for instances that aren't #definition | |
| should not populate title and description for instances that don't have title or description (like Patient) | Not populate title and description for instances that don't have title or description (like Patient) | |

#### `#fishForMetadata`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should use the passed in fisher to fish metadata for instances | Use the passed in fisher to fish metadata for instances | |

#### `#fishForMetadatas`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should use the passed in fisher to fish metadatas for instances | Use the passed in fisher to fish metadatas for instances | |

### `InstanceExporter R5`

#### `#exportInstance`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set the reference child element when assigning a Reference directly to a CodeableReference | Set the reference child element when assigning a Reference directly to a CodeableReference | |
| should set the concept child element when assigning a code directly to a CodeableReference | Set the concept child element when assigning a code directly to a CodeableReference | |
| should set both reference and concept when assigning directly to a CodeableReference | Set both reference and concept when assigning directly to a CodeableReference | |
| should set both concept and reference when assigning directly to a CodeableReference | Set both concept and reference when assigning directly to a CodeableReference | |
| should assign a reference while resolving the Instance being referred to on a CodeableReference | Assign a reference while resolving the Instance being referred to on a CodeableReference | |
| should log an error when an invalid reference is assigned on a CodeableReference | Log an error when an invalid reference is assigned on a CodeableReference | |

---

## [`MappingExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/MappingExporter.test.ts)

**21 tests**

### `MappingExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should log an error when the mapping source does not exist | Log an error when the mapping source does not exist | |

#### `#setMetadata`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should export no mappings with empty input | Export no mappings with empty input | |
| should export the simplest possible mapping | Export the simplest possible mapping | |
| should export a mapping when one does not yet exist | Export a mapping when one does not yet exist | |
| should export a mapping whose source is based on a structure definition without any existing mappings | Export a mapping whose source is based on a structure definition without any existing mappings | |
| should export a mapping with optional metadata | Export a mapping with optional metadata | |
| should log an error and not apply a mapping with an invalid Id | Log an error and not apply a mapping with an invalid Id | |
| should log an error when multiple mappings have the same source and the same id | Log an error when multiple mappings have the same source and the same id | |
| should not log an error when multiple mappings have different sources and the same id | Not log an error when multiple mappings have different sources and the same id | |
| should not log an error and not add metadata but add rules for a simple Mapping that is inherited from the parent | Not log an error and not add metadata but add rules for a simple Mapping that is inherited from the parent | |
| should not log an error and not add metadata but add rules for a Mapping that is inherited from the parent with the same metadata | Not log an error and not add metadata but add rules for a Mapping that is inherited from the parent with the same metadata | |
| should not log an error, should update metadata, and should add rules for a Mapping that is inherited from the parent and has additional metadata not on the parent | Not log an error, should update metadata, and should add rules for a Mapping that is inherited from the parent and has additional metadata not on the parent | |
| should log an error and not add mapping or rules when a Mapping has the same identity as one on the parent but name or uri differs | Log an error and not add mapping or rules when a Mapping has the same identity as one on the parent but name or uri differs | |

#### `#setMappingRules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a valid mapping rule | Apply a valid mapping rule | |
| should apply a valid mapping rule with no path | Apply a valid mapping rule with no path | |
| should apply a valid mapping rule with a Logical source | Apply a valid mapping rule with a Logical source | |
| should apply a valid mapping rule with a Resource source | Apply a valid mapping rule with a Resource source | |
| should log an error and skip rules with paths that cannot be found | Log an error and skip rules with paths that cannot be found | |
| should log an error and skip rules with invalid mappings | Log an error and skip rules with invalid mappings | |

#### `#insertRules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply rules from an insert rule | Apply rules from an insert rule | |
| should log an error and not apply rules from an invalid insert rule | Log an error and not apply rules from an invalid insert rule | |

---

## [`Package.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/Package.test.ts)

**55 tests**

### `Package`

#### `#fishForFHIR()`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should find profiles | Find profiles | |
| should find instances of profiles | Find instances of profiles | |
| should find profiles when fishing with a version | Find profiles when fishing with a version | |
| should not find a profile when fishing with a version that does not match | Not find a profile when fishing with a version that does not match | |
| should find instances of profiles when fishing with a version | Find instances of profiles when fishing with a version | |
| should find extensions | Find extensions | |
| should find instances of extensions | Find instances of extensions | |
| should find extensions when fishing with a version | Find extensions when fishing with a version | |
| should not find an extension when fishing with a version that does not match | Not find an extension when fishing with a version that does not match | |
| should find instances of extensions when fishing with a version | Find instances of extensions when fishing with a version | |
| should find logicals | Find logicals | |
| should find instances of logicals | Find instances of logicals | |
| should find logicals when fishing with a version | Find logicals when fishing with a version | |
| should not find a logical when fishing with a version that does not match | Not find a logical when fishing with a version that does not match | |
| should find instances of logicals when fishing with a version | Find instances of logicals when fishing with a version | |
| should find resources | Find resources | |
| should find instances of resources | Find instances of resources | |
| should find resources when fishing with a version | Find resources when fishing with a version | |
| should not find a resource when fishing with a version that does not match | Not find a resource when fishing with a version that does not match | |
| should find instances of resources when fishing with a version | Find instances of resources when fishing with a version | |
| should find value sets | Find value sets | |
| should find instances of value sets | Find instances of value sets | |
| should find value sets when fishing with a version | Find value sets when fishing with a version | |
| should not find a value set when fishing with a version that does not match | Not find a value set when fishing with a version that does not match | |
| should find instances of value sets when fishing with a version | Find instances of value sets when fishing with a version | |
| should find code systems | Find code systems | |
| should find instances of code systems | Find instances of code systems | |
| should find code systems when fishing with a version | Find code systems when fishing with a version | |
| should not find a code system when fishing with a version that does not match | Not find a code system when fishing with a version that does not match | |
| should find instances of code systems when fishing with a version | Find instances of code systems when fishing with a version | |
| should find instances | Find instances | |
| should find instances that have a version when fishing with a version | Find instances that have a version when fishing with a version | |
| should not find an instance that does not have a version when fishing with a version | Not find an instance that does not have a version when fishing with a version | |
| should not find the definition when the type is not requested | Not find the definition when the type is not requested | |
| should globally find any definition | Globally find any definition | |

#### `#fishForMetadata()`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should find profiles | Find profiles | |
| should find profiles w/ declared imposeProfiles | Find profiles w/ declared imposeProfiles | |
| should find extensions | Find extensions | |
| should find logicals that can not be a reference target or have bindings | Find logicals that can not be a reference target or have bindings | |
| should find logicals that can be a reference target using the logical-target extension | Find logicals that can be a reference target using the logical-target extension | |
| should find logicals that can be a reference target using the structuredefinition-type-characteristics extension | Find logicals that can be a reference target using the structuredefinition-type-characteristics extension | |
| should find logicals that can have a binding using the can-bind extension | Find logicals that can have a binding using the can-bind extension | |
| should find resources | Find resources | |
| should find value sets | Find value sets | |
| should find code systems | Find code systems | |
| should find instances | Find instances | |
| should not find the definition when the type is not requested | Not find the definition when the type is not requested | |
| should globally find any definition | Globally find any definition | |
| should return package metadata when fishing with the package id | Return package metadata when fishing with the package id | |
| should return package metadata when fishing with the package name | Return package metadata when fishing with the package name | |
| should return package metadata with an auto-generated url when url is missing | Return package metadata with an auto-generated url when url is missing | |

#### `#fishForMetadatas()`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should return all matches when there are multiple matches | Return all matches when there are multiple matches | |
| should return one match when there is a single match | Return one match when there is a single match | |
| should return package metadata with an auto-generated url when url is missing | Return package metadata with an auto-generated url when url is missing | |
| should return empty array when there are no matches | Return empty array when there are no matches | |

---

## [`StructureDefinition.ExtensionExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.ExtensionExporter.test.ts)

**44 tests**

### `ExtensionExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single extension | Export a single extension | |
| should add source info for the exported extension to the package | Add source info for the exported extension to the package | |
| should export multiple extensions | Export multiple extensions | |
| should still export extensions if one fails | Still export extensions if one fails | |
| should log a message with source information when the parent is not found | Log a message with source information when the parent is not found | |
| should log a message with source information when the parent is not an extension | Log a message with source information when the parent is not an extension | |
| should export extensions with FSHy parents | Export extensions with FSHy parents | |
| should export extensions with the same FSHy parents | Export extensions with the same FSHy parents | |
| should export extensions with deep FSHy parents | Export extensions with deep FSHy parents | |
| should export extensions with out-of-order FSHy parents | Export extensions with out-of-order FSHy parents | |
| should not log an error when an inline extension is used | Not log an error when an inline extension is used | |
| should export extensions with extension instance parents | Export extensions with extension instance parents | |

#### `#context`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set extension context by a quoted string | Set extension context by a quoted string | |
| should set extension context for an extension by url | Set extension context for an extension by url | |
| should set extension context for an extension by name | Set extension context for an extension by name | |
| should set extension context for an extension by id | Set extension context for an extension by id | |
| should set extension context for a base resource root element by id/name | Set extension context for a base resource root element by id/name | |
| should set extension context for a base resource root element by url | Set extension context for a base resource root element by url | |
| should set extension context for a base resource by id with a FSH path | Set extension context for a base resource by id with a FSH path | |
| should set extension context to itself by url | Set extension context to itself by url | |
| should set extension context to itself by name | Set extension context to itself by name | |
| should set extension context to itself by id | Set extension context to itself by id | |
| should set extension context for a base resource by url with a FSH path | Set extension context for a base resource by url with a FSH path | |
| should set extension context for a base resource (with no derivation) root element by id/name | Set extension context for a base resource (with no derivation) root element by id/name | |
| should set extension context for a base resource (with no derivation) root element by url | Set extension context for a base resource (with no derivation) root element by url | |
| should set extension context for a base resource (with no derivation) by id with a FSH path | Set extension context for a base resource (with no derivation) by id with a FSH path | |
| should set extension context for a base resource (with no derivation) by url with a FSH path | Set extension context for a base resource (with no derivation) by url with a FSH path | |
| should set extension context with type "extension" when the path is part of a complex extension by name | Set extension context with type "extension" when the path is part of a complex extension by name | |
| should set extension context with type "extension" when the path is part of a complex extension by url | Set extension context with type "extension" when the path is part of a complex extension by url | |
| should set extension context with type "extension" when the path is its own sub-extension by name | Set extension context with type "extension" when the path is its own sub-extension by name | |
| should set extension context with type "extension" when the path is is its own sub-extension by url | Set extension context with type "extension" when the path is is its own sub-extension by url | |
| should set extension context with type "extension" when the path is a deep part of a complex extension by name | Set extension context with type "extension" when the path is a deep part of a complex extension by name | |
| should set extension context with type "element" when the path is a deep part of a complex extension, but contains non-extension elements | Set extension context with type "element" when the path is a deep part of a complex extension, but contains non-extension elements | |
| should set extension context when an alias is used for a resource URL | Set extension context when an alias is used for a resource URL | |
| should log an error when no extension or resource can be found with the provided value | Log an error when no extension or resource can be found with the provided value | |

#### `ExtensionExporter > #context > #withCustomResource`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set extension context for a custom resource root element by id | Set extension context for a custom resource root element by id | |
| should set extension context for a custom resource root element by name | Set extension context for a custom resource root element by name | |
| should set extension context for a custom resource root element by url | Set extension context for a custom resource root element by url | |
| should set extension context for a custom resource by id with a FSH path | Set extension context for a custom resource by id with a FSH path | |
| should set extension context for a custom resource by name with a FSH path | Set extension context for a custom resource by name with a FSH path | |
| should set extension context for a custom resource by url with a FSH path | Set extension context for a custom resource by url with a FSH path | |
| should set extension context for a custom resource by url when the url contains a # character | Set extension context for a custom resource by url when the url contains a # character | |
| should log an error when a custom resource element is specified with an invalid FSH path | Log an error when a custom resource element is specified with an invalid FSH path | |

---

## [`StructureDefinition.LogicalExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.LogicalExporter.test.ts)

**42 tests**

### `LogicalExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single logical model | Export a single logical model | |
| should add source info for the exported logical model to the package | Add source info for the exported logical model to the package | |
| should export multiple logical models | Export multiple logical models | |
| should still export logical models if one fails | Still export logical models if one fails | |
| should export a single logical model with Base parent when parent not defined | Export a single logical model with Base parent when parent not defined | |
| should export a single logical model with Base parent by id | Export a single logical model with Base parent by id | |
| should export a single logical model with Base parent by url | Export a single logical model with Base parent by url | |
| should export a single logical model with Element parent by id | Export a single logical model with Element parent by id | |
| should export a single logical model with Element parent by url | Export a single logical model with Element parent by url | |
| should export a single logical model with another logical model parent by id | Export a single logical model with another logical model parent by id | |
| should export a single logical model with another logical model parent by url | Export a single logical model with another logical model parent by url | |
| should export a single logical model with a complex-type parent by id | Export a single logical model with a complex-type parent by id | |
| should export a single logical model with a complex-type parent by url | Export a single logical model with a complex-type parent by url | |
| should export a single logical model with a resource parent by id | Export a single logical model with a resource parent by id | |
| should export a single logical model with a resource parent by url | Export a single logical model with a resource parent by url | |
| should log an error with source information when the parent is invalid | Log an error with source information when the parent is invalid | |
| should log an error with source information when the parent is not found | Log an error with source information when the parent is not found | |
| should export logical models with FSHy parents | Export logical models with FSHy parents | |
| should export logical models with the same FSHy parents | Export logical models with the same FSHy parents | |
| should export logical models with deep FSHy parents | Export logical models with deep FSHy parents | |
| should export logical models with out-of-order FSHy parents | Export logical models with out-of-order FSHy parents | |
| should include added element having logical model as datatype when parent is Base without regard to definition order - order Foo then Bar | Include added element having logical model as datatype when parent is Base without regard to definition order - order Foo then Bar | |
| should include added element having logical model as datatype when parent is Base without regard to definition order - order Bar then Foo | Include added element having logical model as datatype when parent is Base without regard to definition order - order Bar then Foo | |
| should include added element having logical model as datatype when parent is Element | Include added element having logical model as datatype when parent is Element | |
| should include added element having logical model as datatype when parent is another logical model | Include added element having logical model as datatype when parent is another logical model | |
| should not re-add elements that are defined on the parent logical model | Not re-add elements that are defined on the parent logical model | |
| should not re-add elements that are defined on the parent logical model even when the parent type is overwritten with a caret value rule | Not re-add elements that are defined on the parent logical model even when the parent type is overwritten with a caret value rule | |
| should have correct base and types for each nested logical model | Have correct base and types for each nested logical model | |
| should log an error when an inline extension is used | Log an error when an inline extension is used | |
| should allow constraints on newly added elements and sub-elements | Allow constraints on newly added elements and sub-elements | |
| should allow constraints on root elements | Allow constraints on root elements | |
| should allow constraints on inherited elements | Allow constraints on inherited elements | |
| should add new elements after inherited elements | Add new elements after inherited elements | |
| should log an error when slicing an inherited element | Log an error when slicing an inherited element | |
| should export a logical model with characteristics and warn that they are not verified | Export a logical model with characteristics and warn that they are not verified | |
| should create Logical root element with short equal to title if short not available AND definition equal to description if definition not available | Create Logical root element with short equal to title if short not available AND definition equal to description if definition not available | |
| should create Logical root element with short equal to name if short and title not available AND definition equal to name if description and definition not available | Create Logical root element with short equal to name if short and title not available AND definition equal to name if description and definition not available | |
| should create Logical root element with short equal to title if short not available AND definition equal to short if description and definition not available | Create Logical root element with short equal to title if short not available AND definition equal to short if description and definition not available | |
| should create Logical root element with short equal short caret rule AND definition equal to definition caret rule | Create Logical root element with short equal short caret rule AND definition equal to definition caret rule | |

#### `#with-type-characteristics-codes`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should export a logical model with characteristics | Export a logical model with characteristics | |
| should export a logical model with characteristics and warn when a characteristic is not found in the code system | Export a logical model with characteristics and warn when a characteristic is not found in the code system | |

---

## [`StructureDefinition.ProfileExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.ProfileExporter.test.ts)

**24 tests**

### `ProfileExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single profile | Export a single profile | |
| should add source info for the exported profile to the package | Add source info for the exported profile to the package | |
| should export multiple profiles | Export multiple profiles | |
| should still export profiles if one fails | Still export profiles if one fails | |
| should log a error with source information when the parent is not found | Log a error with source information when the parent is not found | |
| should log a error with source information when the parent is not provided | Log a error with source information when the parent is not provided | |
| should export profiles with FSHy parents | Export profiles with FSHy parents | |
| should export profiles with the same FSHy parents | Export profiles with the same FSHy parents | |
| should export profiles with deep FSHy parents | Export profiles with deep FSHy parents | |
| should export profiles with out-of-order FSHy parents | Export profiles with out-of-order FSHy parents | |
| should export a profile with an abstract profile parent | Export a profile with an abstract profile parent | |
| should export a profile with a logical parent | Export a profile with a logical parent | |
| should export profiles with deep logical parents | Export profiles with deep logical parents | |
| should export profiles with profile instance parents | Export profiles with profile instance parents | |
| should defer adding an instance to a profile as a contained resource | Defer adding an instance to a profile as a contained resource | |
| should defer adding an instance with a numeric id to a profile as a contained resource | Defer adding an instance with a numeric id to a profile as a contained resource | |
| should defer adding an instance with an id that resembles a boolean to a profile as a contained resource | Defer adding an instance with an id that resembles a boolean to a profile as a contained resource | |
| should defer adding a binding to an inline ValueSet resource | Defer adding a binding to an inline ValueSet resource | |
| should allow a contained resource with a resourceType to be built from several caret rules | Allow a contained resource with a resourceType to be built from several caret rules | |
| should defer applying a caret rule that would be applied within a contained instance | Defer applying a caret rule that would be applied within a contained instance | |
| should NOT export a profile of an R5 resource in an R4 project | NOT export a profile of an R5 resource in an R4 project | |
| should throw a MismatchedBindingTypeError when a code property is bound to a code system | Throw a MismatchedBindingTypeError when a code property is bound to a code system | |
| should log an error when an inline extension is used | Log an error when an inline extension is used | |

---

## [`StructureDefinition.ResourceExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinition.ResourceExporter.test.ts)

**28 tests**

### `ResourceExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single resource | Export a single resource | |
| should add source info for the exported resource to the package | Add source info for the exported resource to the package | |
| should export multiple resources | Export multiple resources | |
| should still export resources if one fails | Still export resources if one fails | |
| should export resource with Resource parent by id | Export resource with Resource parent by id | |
| should export resource with Resource parent by url | Export resource with Resource parent by url | |
| should export resource with DomainResource parent by id | Export resource with DomainResource parent by id | |
| should export resource with DomainResource parent by url | Export resource with DomainResource parent by url | |
| should export resource with DomainResource parent when parent not specified | Export resource with DomainResource parent when parent not specified | |
| should log an error with source information when the parent is invalid | Log an error with source information when the parent is invalid | |
| should log an error with source information when the parent is not found | Log an error with source information when the parent is not found | |
| should log an error when an inline extension is used | Log an error when an inline extension is used | |
| should allow constraints on newly added elements and sub-elements | Allow constraints on newly added elements and sub-elements | |
| should allow constraints on root elements | Allow constraints on root elements | |
| should allow constraints on inherited elements | Allow constraints on inherited elements | |
| should log an error when slicing an inherited element | Log an error when slicing an inherited element | |
| should log an error when adding an element with the same path as an inherited element | Log an error when adding an element with the same path as an inherited element | |
| should log an error when two rules add a new element with the same path | Log an error when two rules add a new element with the same path | |
| should log an error when a rule with the same path is added by directly calling newElement | Log an error when a rule with the same path is added by directly calling newElement | |
| should not log a warning when exporting a conformant resource | Not log a warning when exporting a conformant resource | |
| should log a warning when exporting a non-conformant resource | Log a warning when exporting a non-conformant resource | |
| should log a warning when exporting a multiple non-conformant resources | Log a warning when exporting a multiple non-conformant resources | |
| should log a warning and truncate the name when exporting a non-conformant resource with a long name | Log a warning and truncate the name when exporting a non-conformant resource with a long name | |
| should create Resource root element with short equal to title if short not available AND definition equal to description if definition not available | Create Resource root element with short equal to title if short not available AND definition equal to description if definition not available | |
| should create Resource root element with short equal to name if short and title not available AND definition equal to name if description and definition not available | Create Resource root element with short equal to name if short and title not available AND definition equal to name if description and definition not available | |
| should create Resource root element with short equal to title if short not available AND definition equal to short if description and definition not available | Create Resource root element with short equal to title if short not available AND definition equal to short if description and definition not available | |
| should create Resource root element with short equal short caret rule AND definition equal to definition caret rule | Create Resource root element with short equal short caret rule AND definition equal to definition caret rule | |

---

## [`StructureDefinitionExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/StructureDefinitionExporter.test.ts)

**376 tests**

### `StructureDefinitionExporter R4`

#### `#StructureDefinition`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should not export duplicate structure definitions | Not export duplicate structure definitions | |
| should warn when the structDef is a profile and title and/or description is an empty string | Warn when the structDef is a profile and title and/or description is an empty string | |
| should warn when the structDef is an extension and title and/or description is an empty string | Warn when the structDef is an extension and title and/or description is an empty string | |
| should warn when the structDef is a logical and title and/or description is an empty string | Warn when the structDef is a logical and title and/or description is an empty string | |
| should warn when the structDef is a resource and title and/or description is an empty string | Warn when the structDef is a resource and title and/or description is an empty string | |
| should log a message when the structure definition has an invalid id | Log a message when the structure definition has an invalid id | |
| should not log a message when the structure definition overrides an invalid id with a Caret Rule | Not log a message when the structure definition overrides an invalid id with a Caret Rule | |
| should log a message when the structure definition overrides an invalid id with an invalid Caret Rule | Log a message when the structure definition overrides an invalid id with an invalid Caret Rule | |
| should log a message when the structure definition overrides an valid id with an invalid Caret Rule | Log a message when the structure definition overrides an valid id with an invalid Caret Rule | |
| should log a message when the structure definition has an invalid name | Log a message when the structure definition has an invalid name | |
| should not log a message when the structure definition overrides an invalid name with a Caret Rule | Not log a message when the structure definition overrides an invalid name with a Caret Rule | |
| should log a message when the structure definition overrides an invalid name with an invalid Caret Rule | Log a message when the structure definition overrides an invalid name with an invalid Caret Rule | |
| should log a message when the structure definition overrides a valid name with an invalid Caret Rule | Log a message when the structure definition overrides a valid name with an invalid Caret Rule | |
| should sanitize the id and log a message when a valid name is used to make an invalid id | Sanitize the id and log a message when a valid name is used to make an invalid id | |
| should sanitize the id and log a message when a long valid name is used to make an invalid id | Sanitize the id and log a message when a long valid name is used to make an invalid id | |
| should log error messages for validation errors on the StructureDefinition | Log error messages for validation errors on the StructureDefinition | |

#### `#Parents`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should create a profile when the definition specifies a resource for a parent | Create a profile when the definition specifies a resource for a parent | |
| should create a profile when the definition specifies another profile for a parent | Create a profile when the definition specifies another profile for a parent | |
| should create a profile when the definition specifies a complex data type for a parent | Create a profile when the definition specifies a complex data type for a parent | |
| should create a profile when the definition specifies a primitive data type for a parent | Create a profile when the definition specifies a primitive data type for a parent | |
| should create an extension with default parent of base Extension when the definition does not specify a parent | Create an extension with default parent of base Extension when the definition does not specify a parent | |
| should create an extension when the definition specifies the base Extension for a parent | Create an extension when the definition specifies the base Extension for a parent | |
| should create an extension when the definition specifies another extension for a parent | Create an extension when the definition specifies another extension for a parent | |
| should create a logical model with default parent of Base when the definition does not specify a parent | Create a logical model with default parent of Base when the definition does not specify a parent | |
| should create a logical model when the definition specifies Element for a parent | Create a logical model when the definition specifies Element for a parent | |
| should create a logical model when the definition specifies another logical model for a parent | Create a logical model when the definition specifies another logical model for a parent | |
| should create a resource with default parent of DomainResource when the definition does not specify a parent | Create a resource with default parent of DomainResource when the definition does not specify a parent | |
| should create a resource when the definition specifies Resource for a parent | Create a resource when the definition specifies Resource for a parent | |
| should throw ParentNotProvidedError when parent specifies an empty parent | Throw ParentNotProvidedError when parent specifies an empty parent | |
| should throw ParentNotDefinedError when parent is not found | Throw ParentNotDefinedError when parent is not found | |
| should throw ParentDeclaredAsNameError when the extension declares itself as the parent | Throw ParentDeclaredAsNameError when the extension declares itself as the parent | |
| should throw ParentDeclaredAsIdError when a extension sets the same value for parent and id | Throw ParentDeclaredAsIdError when a extension sets the same value for parent and id | |
| should throw ParentDeclaredAsNameError when the profile declares itself as the parent | Throw ParentDeclaredAsNameError when the profile declares itself as the parent | |
| should throw ParentDeclaredAsNameError and suggest resource URL when the profile declares itself as the parent and it is a FHIR resource | Throw ParentDeclaredAsNameError and suggest resource URL when the profile declares itself as the parent and it is a FHIR resource | |
| should throw ParentDeclaredAsIdError when a profile sets the same value for parent and id | Throw ParentDeclaredAsIdError when a profile sets the same value for parent and id | |
| should throw ParentDeclaredAsIdError and suggest resource URL when a profile sets the same value for parent and id and the parent is a FHIR resource | Throw ParentDeclaredAsIdError and suggest resource URL when a profile sets the same value for parent and id and the parent is a FHIR resource | |
| should throw ParentDeclaredAsNameError when the resource declares itself as the parent | Throw ParentDeclaredAsNameError when the resource declares itself as the parent | |
| should throw ParentDeclaredAsIdError when a resource sets the same value for parent and id | Throw ParentDeclaredAsIdError when a resource sets the same value for parent and id | |
| should throw ParentDeclaredAsNameError when the logical model declares itself as the parent | Throw ParentDeclaredAsNameError when the logical model declares itself as the parent | |
| should throw ParentDeclaredAsNameError and suggest resource URL when the logical model declares itself as the parent and it is a FHIR resource | Throw ParentDeclaredAsNameError and suggest resource URL when the logical model declares itself as the parent and it is a FHIR resource | |
| should throw ParentDeclaredAsIdError when a logical model sets the same value for parent and id | Throw ParentDeclaredAsIdError when a logical model sets the same value for parent and id | |
| should throw ParentDeclaredAsIdError and suggest resource URL when a logical model sets the same value for parent and id and the parent is a FHIR resource | Throw ParentDeclaredAsIdError and suggest resource URL when a logical model sets the same value for parent and id and the parent is a FHIR resource | |
| should throw InvalidExtensionParentError when an extension has a non-extension for a parent | Throw InvalidExtensionParentError when an extension has a non-extension for a parent | |
| should throw InvalidLogicalParentError when a logical model has a profile for a parent | Throw InvalidLogicalParentError when a logical model has a profile for a parent | |
| should throw InvalidResourceParentError when a resource does not have Resource or DomainResource for a parent | Throw InvalidResourceParentError when a resource does not have Resource or DomainResource for a parent | |

#### `StructureDefinitionExporter R4 > #Parents > Issue #1553 Bug Fix`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should create a profile when the definition specifies another profile does not have a canonical version for the parent | Create a profile when the definition specifies another profile does not have a canonical version for the parent | |
| should create profiles when the definition specifies a different canonical version for the parent | Create profiles when the definition specifies a different canonical version for the parent | |
| should throw an Error when the definition specifies another profile having an unsupported canonical version for the parent | Throw an Error when the definition specifies another profile having an unsupported canonical version for the parent | |
| should throw an Error when the definition specifies another profile having an unexpected canonical version for the parent | Throw an Error when the definition specifies another profile having an unexpected canonical version for the parent | |

#### `#Profile`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set all user-provided metadata for a profile | Set all user-provided metadata for a profile | |
| should set status and version metadata for a profile in FSHOnly mode | Set status and version metadata for a profile in FSHOnly mode | |
| should properly set/clear all metadata properties for a profile | Properly set/clear all metadata properties for a profile | |
| should remove inherited top-level underscore-prefixed metadata properties for a profile | Remove inherited top-level underscore-prefixed metadata properties for a profile | |
| should only inherit inheritable extensions for a profile | Only inherit inheritable extensions for a profile | |
| should not overwrite metadata that is not given for a profile | Not overwrite metadata that is not given for a profile | |
| should allow metadata to be overwritten with caret rule | Allow metadata to be overwritten with caret rule | |
| should log an error when multiple profiles have the same id | Log an error when multiple profiles have the same id | |

#### `#Profile-Element`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply constraints to all instances of contentReference elements when the profile-element extension is applied | Apply constraints to all instances of contentReference elements when the profile-element extension is applied | |
| should apply the profile-element extension when there are several extensions in the type.profile array | Apply the profile-element extension when there are several extensions in the type.profile array | |
| should not apply constraints to all instances of contentReference elements when the profile-element extension is misapplied | Not apply constraints to all instances of contentReference elements when the profile-element extension is misapplied | |

#### `#Extension`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should set all user-provided metadata for an extension | Set all user-provided metadata for an extension | |
| should set status and version metadata for an extension in FSHOnly mode | Set status and version metadata for an extension in FSHOnly mode | |
| should not set metadata on the root element when applyExtensionMetadataToRoot is false | Not set metadata on the root element when applyExtensionMetadataToRoot is false | |
| should properly set/clear all metadata properties for an extension | Properly set/clear all metadata properties for an extension | |
| should remove inherited top-level underscore-prefixed metadata properties for an extension | Remove inherited top-level underscore-prefixed metadata properties for an extension | |
| should overwrite parent context when a new context is set | Overwrite parent context when a new context is set | |
| should not overwrite metadata that is not given for an extension | Not overwrite metadata that is not given for an extension | |
| should export sub-extensions, with similar starting names and different types | Export sub-extensions, with similar starting names and different types | |
| should not hardcode in the default context if parent already had a context | Not hardcode in the default context if parent already had a context | |
| should allow metadata to be overwritten with caret rule | Allow metadata to be overwritten with caret rule | |
| should log an error when multiple extensions have the same id | Log an error when multiple extensions have the same id | |
| should log an error when a profile and an extension have the same id | Log an error when a profile and an extension have the same id | |

#### `#LogicalModel`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should have the correct baseDefinition of Base when parent is not provided | Have the correct baseDefinition of Base when parent is not provided | |
| should have the correct baseDefinition for a provided parent | Have the correct baseDefinition for a provided parent | |
| should set all user-provided metadata for a logical model | Set all user-provided metadata for a logical model | |
| should set status and version metadata for a logical model in FSHOnly mode | Set status and version metadata for a logical model in FSHOnly mode | |
| should properly set/clear all metadata properties for a logical model | Properly set/clear all metadata properties for a logical model | |
| should remove inherited top-level underscore-prefixed metadata properties for a logical model | Remove inherited top-level underscore-prefixed metadata properties for a logical model | |
| should not overwrite metadata that is not given for a logical model | Not overwrite metadata that is not given for a logical model | |
| should allow metadata to be overwritten with caret rule | Allow metadata to be overwritten with caret rule | |
| should allow type to be overwritten with caret rule with a uri value | Allow type to be overwritten with caret rule with a uri value | |
| should log a warning and allow overwriting type with caret rule with a non-uri value | Log a warning and allow overwriting type with caret rule with a non-uri value | |
| should log an error when multiple logical models have the same id | Log an error when multiple logical models have the same id | |
| should log an error when a profile and a logical model have the same id | Log an error when a profile and a logical model have the same id | |
| should include added elements along with parent elements | Include added elements along with parent elements | |
| should include added elements for BackboneElement and children | Include added elements for BackboneElement and children | |
| should log an error when MustSupport is true in a logical model | Log an error when MustSupport is true in a logical model | |

#### `#Resource`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should have the correct baseDefinition of Element when parent is not provided | Have the correct baseDefinition of Element when parent is not provided | |
| should have the correct baseDefinition for a Resource parent | Have the correct baseDefinition for a Resource parent | |
| should have the correct baseDefinition for a DomainResource parent | Have the correct baseDefinition for a DomainResource parent | |
| should set all user-provided metadata for a resource | Set all user-provided metadata for a resource | |
| should set status and version metadata for a resource in FSHOnly mode | Set status and version metadata for a resource in FSHOnly mode | |
| should properly set/clear all metadata properties for a resource | Properly set/clear all metadata properties for a resource | |
| should remove inherited top-level underscore-prefixed metadata properties for a resource | Remove inherited top-level underscore-prefixed metadata properties for a resource | |
| should not overwrite metadata that is not given for a resource | Not overwrite metadata that is not given for a resource | |
| should allow metadata to be overwritten with caret rule | Allow metadata to be overwritten with caret rule | |
| should log an error when multiple resources have the same id | Log an error when multiple resources have the same id | |
| should log an error when a resource and a logical model have the same id | Log an error when a resource and a logical model have the same id | |
| should include added elements along with parent root element | Include added elements along with parent root element | |
| should include added elements for BackboneElement and children | Include added elements for BackboneElement and children | |
| should log an error when MustSupport is true in a resource | Log an error when MustSupport is true in a resource | |

#### `#Invariant`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should not warn or error on a valid Invariant using keywords | Not warn or error on a valid Invariant using keywords | |
| should not warn or error on a valid Invariant using rules | Not warn or error on a valid Invariant using rules | |
| should log an error when description is not provided | Log an error when description is not provided | |
| should log an error when severity is not provided | Log an error when severity is not provided | |
| should log an error when severity is not one of the valid values (set by keyword) | Log an error when severity is not one of the valid values (set by keyword) | |
| should log an error when severity is not one of the valid values (set by rule) | Log an error when severity is not one of the valid values (set by rule) | |
| should log a warning when severity includes a system (set by keyword) | Log a warning when severity includes a system (set by keyword) | |
| should log a warning when severity includes a system (set by rule) | Log a warning when severity includes a system (set by rule) | |

#### `#Rules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should emit an error and continue when the path is not found | Emit an error and continue when the path is not found | |
| should emit an error and continue when the path for the child of a choice element is not found | Emit an error and continue when the path for the child of a choice element is not found | |

#### `#AddElementRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should throw an error for an invalid AddElementRule path | Throw an error for an invalid AddElementRule path | |
| should add an element with a type and minimum required attributes | Add an element with a type and minimum required attributes | |
| should add an element with a content reference and minimum required attributes | Add an element with a content reference and minimum required attributes | |
| should add an element with additional constraint attributes | Add an element with additional constraint attributes | |
| should add an element with multiple targetTypes | Add an element with multiple targetTypes | |
| should add an element with all boolean flags set to true | Add an element with all boolean flags set to true | |
| should add an element with all boolean flags set to false | Add an element with all boolean flags set to false | |
| should add an element with trial use standards flag set to true | Add an element with trial use standards flag set to true | |
| should add an element with normative standards flag set to true | Add an element with normative standards flag set to true | |
| should add an element with draft standards flag set to true | Add an element with draft standards flag set to true | |
| should add an element with all standards flags set to false | Add an element with all standards flags set to false | |
| should log an error when more than one standards flag is set to true | Log an error when more than one standards flag is set to true | |
| should add an element with supported doc attributes | Add an element with supported doc attributes | |
| should log an error and add an element when an element name contains a prohibited special character or is more than 64 characters long | Log an error and add an element when an element name contains a prohibited special character or is more than 64 characters long | |
| should log a warning and add an element when an element name is not a simple alphanumeric | Log a warning and add an element when an element name is not a simple alphanumeric | |
| should log an error when SDRule added before AddElementRule | Log an error when SDRule added before AddElementRule | |
| should log an error when path does not have [x] for multiple data types in AddElementRule | Log an error when path does not have [x] for multiple data types in AddElementRule | |
| should not log an error when path does not have [x] for multiple reference types in AddElementRule | Not log an error when path does not have [x] for multiple reference types in AddElementRule | |
| should not log an error when path does not have [x] for multiple canonical types in AddElementRule | Not log an error when path does not have [x] for multiple canonical types in AddElementRule | |

#### `#CardRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a correct card rule | Apply a correct card rule | |
| should not apply an incorrect card rule | Not apply an incorrect card rule | |
| should apply a card rule with only min specified | Apply a card rule with only min specified | |
| should apply a card rule with only max specified | Apply a card rule with only max specified | |
| should not apply an incorrect min only card rule | Not apply an incorrect min only card rule | |
| should not apply an incorrect max only card rule | Not apply an incorrect max only card rule | |
| should not apply a card rule with no sides specified | Not apply a card rule with no sides specified | |

#### `#FlagRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a valid flag rule | Apply a valid flag rule | |
| should apply a flag rule that specifies an element is trial use | Apply a flag rule that specifies an element is trial use | |
| should apply a flag rule that specifies an element is normative | Apply a flag rule that specifies an element is normative | |
| should apply a flag rule that specifies an element is a draft | Apply a flag rule that specifies an element is a draft | |
| should log an error when more than one standards status flag rule is specified on an element | Log an error when more than one standards status flag rule is specified on an element | |
| should apply a flag rule that changes the existing standards status | Apply a flag rule that changes the existing standards status | |

#### `#ValueSetRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a correct value set rule to an unbound string | Apply a correct value set rule to an unbound string | |
| should apply a correct value set rule that overrides a previous binding | Apply a correct value set rule that overrides a previous binding | |
| should apply a correct value set rule when the VS is referenced by name | Apply a correct value set rule when the VS is referenced by name | |
| should apply a correct value set rule when the VS has a rule that sets its name and it is referenced by name | Apply a correct value set rule when the VS has a rule that sets its name and it is referenced by name | |
| should apply a correct value set rule when the VS specifies a version | Apply a correct value set rule when the VS specifies a version | |
| should use the url specified in a CaretValueRule when referencing a named value set | Use the url specified in a CaretValueRule when referencing a named value set | |
| should apply a value set rule on an element that has the #can-bind characteristic | Apply a value set rule on an element that has the #can-bind characteristic | |
| should apply a value set rule on an element that has the #can-bind type characteristic extension | Apply a value set rule on an element that has the #can-bind type characteristic extension | |
| should apply a value set rule on an element that has the #can-bind type characteristic extension using extension path syntax with url | Apply a value set rule on an element that has the #can-bind type characteristic extension using extension path syntax with url | |
| should log a warning and apply a value set rule on an element that is missing the #can-bind characteristic and extension | Log a warning and apply a value set rule on an element that is missing the #can-bind characteristic and extension | |
| should not apply a value set rule on an element that cannot support it | Not apply a value set rule on an element that cannot support it | |
| should not override a binding with a less strict binding | Not override a binding with a less strict binding | |

#### `#OnlyRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a correct OnlyRule on a non-reference choice | Apply a correct OnlyRule on a non-reference choice | |
| should apply a correct OnlyRule on a reference | Apply a correct OnlyRule on a reference | |
| should apply a correct OnlyRule on a reference to Any | Apply a correct OnlyRule on a reference to Any | |
| should apply a correct OnlyRule on a canonical | Apply a correct OnlyRule on a canonical | |
| should apply a correct OnlyRule with a version on a canonical | Apply a correct OnlyRule with a version on a canonical | |
| should apply a correct OnlyRule on a canonical to Any | Apply a correct OnlyRule on a canonical to Any | |
| should apply a correct OnlyRule with a specific reference target constrained | Apply a correct OnlyRule with a specific reference target constrained | |
| should apply a correct OnlyRule with a specific canonical target constrained | Apply a correct OnlyRule with a specific canonical target constrained | |
| should apply a correct OnlyRule on a non-reference FSHy choice | Apply a correct OnlyRule on a non-reference FSHy choice | |
| should apply a correct OnlyRule on a FSHy reference | Apply a correct OnlyRule on a FSHy reference | |
| should apply a correct OnlyRule on a FSHy canonical | Apply a correct OnlyRule on a FSHy canonical | |
| should apply a correct OnlyRule with a specific target constrained to FSHy definition | Apply a correct OnlyRule with a specific target constrained to FSHy definition | |
| should apply a correct OnlyRule with a specific canonical target constrained to FSHy definition | Apply a correct OnlyRule with a specific canonical target constrained to FSHy definition | |
| should apply correct OnlyRules on circular FSHy reference choices | Apply correct OnlyRules on circular FSHy reference choices | |
| should apply correct OnlyRules on circular FSHy canonical choices | Apply correct OnlyRules on circular FSHy canonical choices | |
| should safely apply correct OnlyRule with circular FSHy parent | Safely apply correct OnlyRule with circular FSHy parent | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with url | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with url | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with alias | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with alias | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with id | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with id | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with name | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with name | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with url | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the type characteristics extension defined using extension path syntax with url | |
| should apply a correct OnlyRule on a reference to a logical type defined as a reference target with the logical target extension | Apply a correct OnlyRule on a reference to a logical type defined as a reference target with the logical target extension | |
| should apply a correct OnlyRule on a reference to a FSHy logical type defined with the can-be-target characteristic | Apply a correct OnlyRule on a reference to a FSHy logical type defined with the can-be-target characteristic | |
| should apply a correct OnlyRule on a reference to a FSHy logical type defined with the type characteristics extension | Apply a correct OnlyRule on a reference to a FSHy logical type defined with the type characteristics extension | |
| should apply a correct OnlyRule on a reference to a FSHy logical type defined with the logical target extension | Apply a correct OnlyRule on a reference to a FSHy logical type defined with the logical target extension | |
| should apply a correct OnlyRule on a self-referential FSHy logical type with the can-be-target characteristic | Apply a correct OnlyRule on a self-referential FSHy logical type with the can-be-target characteristic | |
| should apply a correct OnlyRule on a self-referential FSHy logical type with the type characteristics extension | Apply a correct OnlyRule on a self-referential FSHy logical type with the type characteristics extension | |
| should apply an OnlyRule on a reference to a FSHy logical type and log a warning if it is not specified as a reference target | Apply an OnlyRule on a reference to a FSHy logical type and log a warning if it is not specified as a reference target | |
| should apply a correct OnlyRule on a reference to a defined logical type defined with the logical target extension | Apply a correct OnlyRule on a reference to a defined logical type defined with the logical target extension | |
| should apply a correct OnlyRule on a reference to a defined logical type and log a warning if it is defined without the logical target extension | Apply a correct OnlyRule on a reference to a defined logical type and log a warning if it is defined without the logical target extension | |
| should log a debug message when we detect a circular dependency in OnlyRules that might result in incomplete definitions | Log a debug message when we detect a circular dependency in OnlyRules that might result in incomplete definitions | |
| should log a warning message when we detect a circular dependency that causes an incomplete parent | Log a warning message when we detect a circular dependency that causes an incomplete parent | |
| should apply an OnlyRule to constrain an id element | Apply an OnlyRule to constrain an id element | |
| should apply an OnlyRule to constrain a url element | Apply an OnlyRule to constrain a url element | |
| should not apply an incorrect OnlyRule | Not apply an incorrect OnlyRule | |
| should log an error when a type constraint implicitly removes a choice created in the current StructureDefinition | Log an error when a type constraint implicitly removes a choice created in the current StructureDefinition | |
| should not log an error when a type constraint implicitly removes a choice that has no rules applied in the current StructureDefinition | Not log an error when a type constraint implicitly removes a choice that has no rules applied in the current StructureDefinition | |
| should not log an error when a type constraint is applied to a specific slice | Not log an error when a type constraint is applied to a specific slice | |
| should not log an error when a type constraint is applied to a slice with a name that is the prefix of another slice | Not log an error when a type constraint is applied to a slice with a name that is the prefix of another slice | |
| should log an error when extension is constrained with a modifier extension | Log an error when extension is constrained with a modifier extension | |
| should log an error each time a modifier extension is used to constrain an extension element | Log an error each time a modifier extension is used to constrain an extension element | |
| should not log an error when extension is constrained with a non-modifier extension | Not log an error when extension is constrained with a non-modifier extension | |
| should log an error when modifierExtension is constrained with a non-modifier extension | Log an error when modifierExtension is constrained with a non-modifier extension | |
| should not log an error when modifierExtension is constrained with a modifier extension | Not log an error when modifierExtension is constrained with a modifier extension | |

#### `#AssignedValueRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a correct AssignmentRule | Apply a correct AssignmentRule | |
| should apply a correct AssignmentRule for Quantity w/ value 0 | Apply a correct AssignmentRule for Quantity w/ value 0 | |
| should apply a Reference AssignmentRule and replace the Reference | Apply a Reference AssignmentRule and replace the Reference | |
| should apply a Reference AssignmentRule when the instance type is a logical type with the can-be-target characteristic | Apply a Reference AssignmentRule when the instance type is a logical type with the can-be-target characteristic | |
| should apply a Reference AssignmentRule and log a warning when the instance type is a logical type without the can-be-target characteristic | Apply a Reference AssignmentRule and log a warning when the instance type is a logical type without the can-be-target characteristic | |
| should not apply a Reference AssignmentRule with invalid type and log an error | Not apply a Reference AssignmentRule with invalid type and log an error | |
| should apply AssignmentRules to different types of a choice element | Apply AssignmentRules to different types of a choice element | |
| should apply a Code AssignmentRule and replace the local complete code system name with its url | Apply a Code AssignmentRule and replace the local complete code system name with its url | |
| should apply a Code AssignmentRule that uses a name set by a rule and replace the local complete code system name with its url | Apply a Code AssignmentRule that uses a name set by a rule and replace the local complete code system name with its url | |
| should apply a Code AssignmentRule and replace the local incomplete code system name with its url when the code is not in the system | Apply a Code AssignmentRule and replace the local incomplete code system name with its url when the code is not in the system | |
| should apply a Code AssignmentRule and replace the local complete instance of CodeSystem name with its url | Apply a Code AssignmentRule and replace the local complete instance of CodeSystem name with its url | |
| should apply a Code AssignmentRule that uses a name set by a rule and replace the local complete instance of CodeSystem name with its url | Apply a Code AssignmentRule that uses a name set by a rule and replace the local complete instance of CodeSystem name with its url | |
| should apply a Code AssignmentRule and replace the local incomplete instance of CodeSystem name with its url when the code is not in the system | Apply a Code AssignmentRule and replace the local incomplete instance of CodeSystem name with its url when the code is not in the system | |
| should apply a Code AssignmentRule and replace the local complete code system name with its url when the code is added by a RuleSet | Apply a Code AssignmentRule and replace the local complete code system name with its url when the code is added by a RuleSet | |
| should apply a Code AssignmentRule and replace the local complete instance of CodeSystem name with its url when the code is added by a RuleSet | Apply a Code AssignmentRule and replace the local complete instance of CodeSystem name with its url when the code is added by a RuleSet | |
| should log an error when applying a Code AssignmentRule with a local complete code system name when the code does not exist | Log an error when applying a Code AssignmentRule with a local complete code system name when the code does not exist | |
| should log an error when applying a Code AssignmentRule with a local complete instance of CodeSystem name when the code does not exist | Log an error when applying a Code AssignmentRule with a local complete instance of CodeSystem name when the code does not exist | |
| should log an error when applying a Code AssignmentRule with a local complete code system url when the code does not exist | Log an error when applying a Code AssignmentRule with a local complete code system url when the code does not exist | |
| should apply a Code AssignmentRule and replace the id of code system (from the core version fhir or dependency) with its url | Apply a Code AssignmentRule and replace the id of code system (from the core version fhir or dependency) with its url | |
| should apply a Code AssignmentRule and replace the name of code system (from the core version fhir or dependency) with its url | Apply a Code AssignmentRule and replace the name of code system (from the core version fhir or dependency) with its url | |
| should apply a Code AssignmentRule and keep the url of code system (from the core version fhir or dependency) as the system url | Apply a Code AssignmentRule and keep the url of code system (from the core version fhir or dependency) as the system url | |
| should apply an AssignmentRule with a valid Canonical entity defined in FSH | Apply an AssignmentRule with a valid Canonical entity defined in FSH | |
| should apply an Assignment rule with Canonical of a Questionnaire instance | Apply an Assignment rule with Canonical of a Questionnaire instance | |
| should apply an Assignment rule with Canonical of an inline instance | Apply an Assignment rule with Canonical of an inline instance | |
| should apply an AssignmentRule with Canonical of a FHIR entity | Apply an AssignmentRule with Canonical of a FHIR entity | |
| should apply an AssignmentRule with Canonical of a FHIR entity with a given version | Apply an AssignmentRule with Canonical of a FHIR entity with a given version | |
| should not apply an AssignmentRule with an invalid Canonical entity and log an error | Not apply an AssignmentRule with an invalid Canonical entity and log an error | |
| should apply an instance AssignmentRule and replace the instance | Apply an instance AssignmentRule and replace the instance | |
| should log a warning and apply an instance AssignmentRule and replace the instance when the instance is an example | Log a warning and apply an instance AssignmentRule and replace the instance when the instance is an example | |
| should apply an instance AssignmentRule when the instance has a numeric id | Apply an instance AssignmentRule when the instance has a numeric id | |
| should log a warning and apply an instance AssignmentRule when the instance has a numeric id | Log a warning and apply an instance AssignmentRule when the instance has a numeric id | |
| should apply an instance AssignmentRule when the instance has an id that resembles a boolean | Apply an instance AssignmentRule when the instance has an id that resembles a boolean | |
| should not apply an instance AssignmentRule when the instance cannot be found | Not apply an instance AssignmentRule when the instance cannot be found | |
| should use the url specified in a CaretValueRule when referencing a named code system | Use the url specified in a CaretValueRule when referencing a named code system | |
| should apply an AssignmentRule on the child of a choice element with constrained choices that share a type | Apply an AssignmentRule on the child of a choice element with constrained choices that share a type | |
| should apply an AssignmentRule on the child of a choice element with constrained choices that share a profile | Apply an AssignmentRule on the child of a choice element with constrained choices that share a profile | |
| should not apply an incorrect AssignmentRule | Not apply an incorrect AssignmentRule | |
| should not apply an AssignmentRule when the value is refers to an Instance that is not found | Not apply an AssignmentRule when the value is refers to an Instance that is not found | |
| should not apply an AssignmentRule when the value is numeric and refers to an Instance, but both types are wrong | Not apply an AssignmentRule when the value is numeric and refers to an Instance, but both types are wrong | |
| should not apply an AssignmentRule when the value is boolean and refers to an Instance, but both types are wrong | Not apply an AssignmentRule when the value is boolean and refers to an Instance, but both types are wrong | |
| should not apply an AssignmentRule when the value is numeric and refers to an Instance, but it conflicts with an existing value | Not apply an AssignmentRule when the value is numeric and refers to an Instance, but it conflicts with an existing value | |
| should not apply a AssignmentRule to a parent element when it would conflict with a child element | Not apply a AssignmentRule to a parent element when it would conflict with a child element | |
| should not apply a AssignmentRule to a complex typed element when it would conflict with a child element present in an array in the type | Not apply a AssignmentRule to a complex typed element when it would conflict with a child element present in an array in the type | |
| should not apply a AssignmentRule to a slice when it would conflict with a child of the list element | Not apply a AssignmentRule to a slice when it would conflict with a child of the list element | |
| should resolve soft indexing within Caret Paths on profiles | Resolve soft indexing within Caret Paths on profiles | |
| should not change slice cardinality when an AssignmentRule is applied directly on the slice | Not change slice cardinality when an AssignmentRule is applied directly on the slice | |
| should not apply a AssignmentRule to a slice when it would conflict with a child slice of the list element | Not apply a AssignmentRule to a slice when it would conflict with a child slice of the list element | |

#### `#ContainsRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a ContainsRule on an element with defined slicing | Apply a ContainsRule on an element with defined slicing | |
| should apply a ContainsRule on a slice | Apply a ContainsRule on a slice | |
| should apply a ContainsRule on an extension slice | Apply a ContainsRule on an extension slice | |
| should log a warning when an element has both a slice name and slicing | Log a warning when an element has both a slice name and slicing | |
| should apply a ContainsRule of a defined extension on an extension element | Apply a ContainsRule of a defined extension on an extension element | |
| should apply a ContainsRule of a defined extension on a modifierExtension element | Apply a ContainsRule of a defined extension on a modifierExtension element | |
| should apply a ContainsRule of an aliased extension on an extension element | Apply a ContainsRule of an aliased extension on an extension element | |
| should apply a ContainsRule of an existing aliased extension on an extension element | Apply a ContainsRule of an existing aliased extension on an extension element | |
| should apply a ContainsRule of an inline extension to an extension element | Apply a ContainsRule of an inline extension to an extension element | |
| should apply a ContainsRule of an inline extension with a name that resolves to a non-extension type | Apply a ContainsRule of an inline extension with a name that resolves to a non-extension type | |
| should apply a ContainsRule of an extension with a versioned URL | Apply a ContainsRule of an extension with a versioned URL | |
| should apply a ContainsRule of an extension with a versioned URL and log a warning if the version does not match | Apply a ContainsRule of an extension with a versioned URL and log a warning if the version does not match | |
| should apply a ContainsRule of an extension with an overridden URL | Apply a ContainsRule of an extension with an overridden URL | |
| should apply a ContainsRule of an extension with an overridden URL by URL | Apply a ContainsRule of an extension with an overridden URL by URL | |
| should apply multiple ContainsRule on an element with defined slicing | Apply multiple ContainsRule on an element with defined slicing | |
| should apply a containsRule on the child of a choice element with a common ancestor of Element | Apply a containsRule on the child of a choice element with a common ancestor of Element | |
| should report an error and not add the slice when a ContainsRule tries to add a slice that already exists | Report an error and not add the slice when a ContainsRule tries to add a slice that already exists | |
| should report an error and not add the extension when an extension ContainsRule tries to add a slice that already exists but has a different extension URL | Report an error and not add the extension when an extension ContainsRule tries to add a slice that already exists but has a different extension URL | |
| should report a warning and not re-add the extension when an extension ContainsRule tries to add a slice that already exists with a matching extension URL | Report a warning and not re-add the extension when an extension ContainsRule tries to add a slice that already exists with a matching extension URL | |
| should report an error and not add the slice when a ContainsRule tries to add a slice that was created on the parent | Report an error and not add the slice when a ContainsRule tries to add a slice that was created on the parent | |
| should not apply a ContainsRule on an element without defined slicing | Not apply a ContainsRule on an element without defined slicing | |
| should NOT report a warning if the extension slice name resolves to an external extension type and no explicit type was specified | NOT report a warning if the extension slice name resolves to an external extension type and no explicit type was specified | |
| should NOT report a warning if the extension slice name resolves to a FSH extension and no explicit type was specified | NOT report a warning if the extension slice name resolves to a FSH extension and no explicit type was specified | |
| should not report a warning if the extension slice name resolves to an extension type but explicit type was specified | Not report a warning if the extension slice name resolves to an extension type but explicit type was specified | |
| should not report a warning if the extension slice name does not resolve to an extension type | Not report a warning if the extension slice name does not resolve to an extension type | |
| should report an error if the author specifies a slice type on a non-extension | Report an error if the author specifies a slice type on a non-extension | |
| should report an error for an extension ContainsRule with a type that resolves to a non-extension | Report an error for an extension ContainsRule with a type that resolves to a non-extension | |
| should report an error for an extension ContainsRule with a non-modifier extension type on a modifierExtension path | Report an error for an extension ContainsRule with a non-modifier extension type on a modifierExtension path | |
| should not report an error for an extension ContainsRule with a modifier extension type on a modifierExtension path | Not report an error for an extension ContainsRule with a modifier extension type on a modifierExtension path | |
| should report an error for an extension ContainsRule with a modifier extension type on an extension path | Report an error for an extension ContainsRule with a modifier extension type on an extension path | |
| should not report an error for an extension ContainsRule with a non-modifier extension type on an extension path | Not report an error for an extension ContainsRule with a non-modifier extension type on an extension path | |
| should report an error for an extension ContainsRule with a type that does not resolve | Report an error for an extension ContainsRule with a type that does not resolve | |
| should report an error for a ContainsRule on a single element | Report an error for a ContainsRule on a single element | |
| should not report an error for an extension Contains rule with an extension that is missing a snapshot when checking if its a modifierExtension | Not report an error for an extension Contains rule with an extension that is missing a snapshot when checking if its a modifierExtension | |

#### `#CaretValueRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a CaretValueRule on an element with a path | Apply a CaretValueRule on an element with a path | |
| should not apply an invalid CaretValueRule on an element with a path | Not apply an invalid CaretValueRule on an element with a path | |
| should apply a CaretValueRule on the parent element | Apply a CaretValueRule on the parent element | |
| should apply a CaretValueRule on an element without a path | Apply a CaretValueRule on an element without a path | |
| should apply a CaretValueRule on the child of a primitive element without a path | Apply a CaretValueRule on the child of a primitive element without a path | |
| should apply a CaretValueRule on an extension of a primitive element without a path | Apply a CaretValueRule on an extension of a primitive element without a path | |
| should apply a CaretValueRule on an extension on ElementDefinition | Apply a CaretValueRule on an extension on ElementDefinition | |
| should apply a CaretValueRule on an extension on ElementDefinition even when the extension references an allowed R5 resource in an R4 IG | Apply a CaretValueRule on an extension on ElementDefinition even when the extension references an allowed R5 resource in an R4 IG | |
| should not apply an invalid CaretValueRule on an element without a path | Not apply an invalid CaretValueRule on an element without a path | |
| should apply a CaretValueRule on an extension element without a path | Apply a CaretValueRule on an extension element without a path | |
| should apply a Reference CaretValueRule on an SD and replace the Reference | Apply a Reference CaretValueRule on an SD and replace the Reference | |
| should apply a Reference CaretValueRule on an ED and replace the Reference | Apply a Reference CaretValueRule on an ED and replace the Reference | |
| should apply a CodeSystem CaretValueRule on an SD and replace the CodeSystem | Apply a CodeSystem CaretValueRule on an SD and replace the CodeSystem | |
| should apply a CodeSystem CaretValueRule on an ED and replace the Reference | Apply a CodeSystem CaretValueRule on an ED and replace the Reference | |
| should identify existing extensions by URL when applying a CaretValueRule on a StructureDefintiion | Identify existing extensions by URL when applying a CaretValueRule on a StructureDefintiion | |
| should apply CaretValueRules on the targetProfile of a type | Apply CaretValueRules on the targetProfile of a type | |
| should apply CaretValueRules on the aggregation of a type and replace the parent values | Apply CaretValueRules on the aggregation of a type and replace the parent values | |
| should apply CaretValueRules on elements within the aggregation of a type and replace the parent values | Apply CaretValueRules on elements within the aggregation of a type and replace the parent values | |
| should apply CaretValueRules on elements within the aggregation of a type and replace the children of parent values when there is no parent value | Apply CaretValueRules on elements within the aggregation of a type and replace the children of parent values when there is no parent value | |
| should output an error when a choice element has values assigned to more than one choice type | Output an error when a choice element has values assigned to more than one choice type | |

#### `#ObeysRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply an ObeysRule at the specified path | Apply an ObeysRule at the specified path | |
| should apply an ObeysRule at specified path (for Invariant with rules) | Apply an ObeysRule at specified path (for Invariant with rules) | |
| should apply an ObeysRule at specified path (for Invariant with rules overriding keywords) | Apply an ObeysRule at specified path (for Invariant with rules overriding keywords) | |
| should apply an ObeysRule at specified path (for Invariant with soft-indexed rules) | Apply an ObeysRule at specified path (for Invariant with soft-indexed rules) | |
| should apply an ObeysRule at specified path (for Invariant with insert rules) | Apply an ObeysRule at specified path (for Invariant with insert rules) | |
| should apply an ObeysRule at the path which does not have a constraint | Apply an ObeysRule at the path which does not have a constraint | |
| should apply an ObeysRule to the base element when no path specified | Apply an ObeysRule to the base element when no path specified | |
| should apply an ObeysRule to the base element when no path specified (for Invariant with rules) | Apply an ObeysRule to the base element when no path specified (for Invariant with rules) | |
| should not apply an ObeysRule on an invariant that does not exist | Not apply an ObeysRule on an invariant that does not exist | |
| should log an error when applying an ObeysRule on an invariant with an invalid id | Log an error when applying an ObeysRule on an invariant with an invalid id | |
| should log an error with correct tracking info when applying an ObeysRule with an invalid rule | Log an error with correct tracking info when applying an ObeysRule with an invalid rule | |

#### `#Extension preprocessing`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should zero out Extension.value[x] when Extension.extension is used | Zero out Extension.value[x] when Extension.extension is used | |
| should not zero out Extension.value[x] if Extension.extension is zeroed out | Not zero out Extension.value[x] if Extension.extension is zeroed out | |
| should log an error if Extension.extension and Extension.value[x] are both used but apply both rules | Log an error if Extension.extension and Extension.value[x] are both used but apply both rules | |
| should zero out Extension.extension when Extension.value[x] is used | Zero out Extension.extension when Extension.value[x] is used | |
| should not zero out Extension.extension if Extension.value[x] is zeroed out | Not zero out Extension.extension if Extension.value[x] is zeroed out | |
| should log an error if Extension.value[x] is changed after Extension.extension is used but apply both rules | Log an error if Extension.value[x] is changed after Extension.extension is used but apply both rules | |
| should zero out value[x] on an extension defined inline that uses extension | Zero out value[x] on an extension defined inline that uses extension | |
| should zero out extension on an extension defined inline that uses value[x] | Zero out extension on an extension defined inline that uses value[x] | |
| should not zero out extension if value[x] is zeroed out on an extension defined inline | Not zero out extension if value[x] is zeroed out on an extension defined inline | |
| should not zero out value[x] if extension is zeroed out on an extension defined inline | Not zero out value[x] if extension is zeroed out on an extension defined inline | |
| should log an error if extension is used after value[x] on an extension defined inline and apply both rules | Log an error if extension is used after value[x] on an extension defined inline and apply both rules | |
| should log an error if value[x] is used after extension on an extension defined inline and apply both rules | Log an error if value[x] is used after extension on an extension defined inline and apply both rules | |
| should zero out value[x] if extension is used on an extension defined inline on a profile | Zero out value[x] if extension is used on an extension defined inline on a profile | |
| should correctly allow both extension and value[x] on profiles | Correctly allow both extension and value[x] on profiles | |
| should not add value[x] onto non-extension elements | Not add value[x] onto non-extension elements | |
| should set value[x] on nested elements of a profile without zeroing extension | Set value[x] on nested elements of a profile without zeroing extension | |
| should not set inferred 0..0 CardRules if they were set on the FSH definition | Not set inferred 0..0 CardRules if they were set on the FSH definition | |

#### `#RulesWithSlices`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a CardRule that makes the cardinality of the child of a slice narrower | Apply a CardRule that makes the cardinality of the child of a slice narrower | |
| should apply a CardRule that would make the cardinality of a slice smaller than the root | Apply a CardRule that would make the cardinality of a slice smaller than the root | |
| should apply a CardRule that would increase the minimum cardinality of a child of a slice | Apply a CardRule that would increase the minimum cardinality of a child of a slice | |
| should apply a CardRule that would decrease the maximum cardinality of a child of a slice | Apply a CardRule that would decrease the maximum cardinality of a child of a slice | |
| should apply a CardRule that would increase the minimum cardinality and decrease the maximum cardinality of a child of a slice | Apply a CardRule that would increase the minimum cardinality and decrease the maximum cardinality of a child of a slice | |
| should not apply a CardRule that is incompatible with the existing cardinality of a child of a slice | Not apply a CardRule that is incompatible with the existing cardinality of a child of a slice | |
| should not apply a CardRule that is incompatible with the existing cardinality on some of the children of slices | Not apply a CardRule that is incompatible with the existing cardinality on some of the children of slices | |
| should apply a FlagRule on a sliced element that updates the flags on its slices | Apply a FlagRule on a sliced element that updates the flags on its slices | |
| should apply a FlagRule on the child of a sliced element that updates the flags on the child of a slice | Apply a FlagRule on the child of a sliced element that updates the flags on the child of a slice | |
| should apply BindingRules on a slice, then a sliced element, with different value sets | Apply BindingRules on a slice, then a sliced element, with different value sets | |
| should apply BindingRules on a sliced element, then a slice, with different value sets | Apply BindingRules on a sliced element, then a slice, with different value sets | |
| should apply BindingRules on a slice, then the sliced element, with the same value set | Apply BindingRules on a slice, then the sliced element, with the same value set | |
| should not apply a BindingRule on a sliced element that would bind it to the same value set as the root, but more weakly | Not apply a BindingRule on a sliced element that would bind it to the same value set as the root, but more weakly | |
| should apply BindingRules on the child of a slice, then the child of a sliced element, with different value sets | Apply BindingRules on the child of a slice, then the child of a sliced element, with different value sets | |
| should apply BindingRules on the child of a sliced element, then the child of a slice, with different value sets | Apply BindingRules on the child of a sliced element, then the child of a slice, with different value sets | |
| should apply BindingRules on the child of a slice, then the child of the sliced element, with the same value set | Apply BindingRules on the child of a slice, then the child of the sliced element, with the same value set | |
| should not apply a BindingRule on the child of a sliced element that would bind it to the same value set as the child of the root, but more weakly | Not apply a BindingRule on the child of a sliced element that would bind it to the same value set as the child of the root, but more weakly | |
| should apply an OnlyRule on a sliced element that updates the types on its slices | Apply an OnlyRule on a sliced element that updates the types on its slices | |
| should apply an OnlyRule on a sliced element that includes more types than are allowed on its slices | Apply an OnlyRule on a sliced element that includes more types than are allowed on its slices | |
| should apply an OnlyRule on a sliced element that removes types available on a slice | Apply an OnlyRule on a sliced element that removes types available on a slice | |
| should apply an OnlyRule using a profile on a sliced element that matches the types available on its slices | Apply an OnlyRule using a profile on a sliced element that matches the types available on its slices | |
| should apply an OnlyRule using multiple profiles on a sliced element where at least one of the profiles matches the types available on its slices | Apply an OnlyRule using multiple profiles on a sliced element where at least one of the profiles matches the types available on its slices | |
| should not apply an OnlyRule on a sliced element that would invalidate any of its slices | Not apply an OnlyRule on a sliced element that would invalidate any of its slices | |
| should apply an OnlyRule on a sliced element that would remove all types from a zeroed-out slice | Apply an OnlyRule on a sliced element that would remove all types from a zeroed-out slice | |
| should apply an OnlyRule on a sliced element that constrains the types on its slices to subtypes | Apply an OnlyRule on a sliced element that constrains the types on its slices to subtypes | |
| should log an error when a type constraint implicitly removes a choice on a sliced element | Log an error when a type constraint implicitly removes a choice on a sliced element | |
| should log an error when a type constraint on the child of a slice implicitly removes a choice | Log an error when a type constraint on the child of a slice implicitly removes a choice | |
| should apply an ObeysRule on a sliced element and not update the constraints on its slices | Apply an ObeysRule on a sliced element and not update the constraints on its slices | |
| should apply an ObeysRule on the child of a sliced element and not update the child elements on its slices | Apply an ObeysRule on the child of a sliced element and not update the child elements on its slices | |

#### `#toJSON`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should correctly generate a diff containing only changed elements | Correctly generate a diff containing only changed elements | |
| should correctly generate a diff containing only changed elements when elements are unfolded | Correctly generate a diff containing only changed elements when elements are unfolded | |
| should correctly generate a diff containing only changed elements when elements are sliced | Correctly generate a diff containing only changed elements when elements are sliced | |
| should not include inherited extension slices in a child differential when the child adds slicing on another element | Not include inherited extension slices in a child differential when the child adds slicing on another element | |
| should include sliceName in a differential when an attribute of the slice is changed | Include sliceName in a differential when an attribute of the slice is changed | |
| should include mustSupport in the differential of a new slice, even if the base element is also mustSupport | Include mustSupport in the differential of a new slice, even if the base element is also mustSupport | |
| should include the children of primitive elements when serializing to JSON | Include the children of primitive elements when serializing to JSON | |

#### `#insertRules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply rules from an insert rule | Apply rules from an insert rule | |
| should log an error and not apply rules from an invalid insert rule | Log an error and not apply rules from an invalid insert rule | |

#### `#fishForMetadata`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should use the passed in fisher to fish metadata for instances | Use the passed in fisher to fish metadata for instances | |

#### `#fishForMetadatas`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should use the passed in fisher to fish metadatas for instances | Use the passed in fisher to fish metadatas for instances | |

### `StructureDefinitionExporter R5`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a Reference AssignmentRule and replace the Reference on a CodeableReference | Apply a Reference AssignmentRule and replace the Reference on a CodeableReference | |
| should apply a Reference AssignmentRule directly to a CodeableReference element | Apply a Reference AssignmentRule directly to a CodeableReference element | |
| should apply a FshCode AssignmentRule directly to a CodeableReference element | Apply a FshCode AssignmentRule directly to a CodeableReference element | |
| should not apply a Reference AssignmentRule with invalid type constraints on a parent CodeableReference | Not apply a Reference AssignmentRule with invalid type constraints on a parent CodeableReference | |

#### `#AddElementRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should not log an error when path does not have [x] for multiple CodeableReference types in AddElementRule | Not log an error when path does not have [x] for multiple CodeableReference types in AddElementRule | |

#### `#OnlyRule`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply a correct OnlyRule on a CodeableReference | Apply a correct OnlyRule on a CodeableReference | |
| should apply a correct OnlyRule on a CodeableReference reference to Any | Apply a correct OnlyRule on a CodeableReference reference to Any | |

---

## [`ValueSetExporter.test.ts`](https://github.com/FHIR/sushi/blob/main/test/export/ValueSetExporter.test.ts)

**94 tests**

### `ValueSetExporter`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should output empty results with empty input | Output empty results with empty input | |
| should export a single value set | Export a single value set | |
| should add source info for the exported value set to the package | Add source info for the exported value set to the package | |
| should export multiple value sets | Export multiple value sets | |
| should export a value set with additional metadata | Export a value set with additional metadata | |
| should export a value set with status and version in FSHOnly mode | Export a value set with status and version in FSHOnly mode | |
| should warn when title and/or description is an empty string | Warn when title and/or description is an empty string | |
| should log a message when the value set has an invalid id | Log a message when the value set has an invalid id | |
| should not log a message when the value set overrides an invalid id with a Caret Rule | Not log a message when the value set overrides an invalid id with a Caret Rule | |
| should log a message when the value set overrides an invalid id with an invalid Caret Rule | Log a message when the value set overrides an invalid id with an invalid Caret Rule | |
| should log a message when the value set overrides a valid id with an invalid Caret Rule | Log a message when the value set overrides a valid id with an invalid Caret Rule | |
| should log a message when the value set has an invalid name | Log a message when the value set has an invalid name | |
| should not log a message when the value set overrides an invalid name with a Caret Rule | Not log a message when the value set overrides an invalid name with a Caret Rule | |
| should log a message when the value set overrides an invalid name with an invalid Caret Rule | Log a message when the value set overrides an invalid name with an invalid Caret Rule | |
| should log a message when the value set overrides a valid name with an invalid Caret Rule | Log a message when the value set overrides a valid name with an invalid Caret Rule | |
| should sanitize the id and log a message when a valid name is used to make an invalid id | Sanitize the id and log a message when a valid name is used to make an invalid id | |
| should sanitize the id and log a message when a long valid name is used to make an invalid id | Sanitize the id and log a message when a long valid name is used to make an invalid id | |
| should log an error when multiple value sets have the same id | Log an error when multiple value sets have the same id | |
| should export each value set once, even if export is called more than once | Export each value set once, even if export is called more than once | |
| should export a value set that includes a component from a system | Export a value set that includes a component from a system | |
| should export a value set that includes a component from a named system | Export a value set that includes a component from a named system | |
| should export a value set that includes a component from a contained inline instance of code system and add the valueset-system extension | Export a value set that includes a component from a contained inline instance of code system and add the valueset-system extension | |
| should log an error and not add the component when attempting to reference an inline instance of code system that is not contained | Log an error and not add the component when attempting to reference an inline instance of code system that is not contained | |
| should log a warning and export the value set when containing an example instance of code system | Log a warning and export the value set when containing an example instance of code system | |
| should export a value set that includes a component from a value set | Export a value set that includes a component from a value set | |
| should export a value set that includes a component from a value set with a version | Export a value set that includes a component from a value set with a version | |
| should export a value set that includes a component from a local value set with a version | Export a value set that includes a component from a local value set with a version | |
| should export a value set that includes a component from a named value set | Export a value set that includes a component from a named value set | |
| should export a value set that includes a component from a named versioned value set | Export a value set that includes a component from a named versioned value set | |
| should export a value set that includes a component from a named versioned value set and warn on version mismatch | Export a value set that includes a component from a named versioned value set and warn on version mismatch | |
| should throw error for caret rule on valueset compose component without any concept | Throw error for caret rule on valueset compose component without any concept | |
| should export a value set with a contained resource created on the value set | Export a value set with a contained resource created on the value set | |
| should export a value set with a contained resource modified on the value set | Export a value set with a contained resource modified on the value set | |
| should log a warning and export a value set with a contained example resource with a numeric id modified on the value set | Log a warning and export a value set with a contained example resource with a numeric id modified on the value set | |
| should export a value set that includes a component from a contained code system created on the value set and referenced by id | Export a value set that includes a component from a contained code system created on the value set and referenced by id | |
| should export a value set that includes a component from a contained code system created on the value set and referenced by name | Export a value set that includes a component from a contained code system created on the value set and referenced by name | |
| should export a value set that includes a component from a contained code system created on the value set and referenced by url | Export a value set that includes a component from a contained code system created on the value set and referenced by url | |
| should not use a contained resource created on the value set as a component system when that resource is not a CodeSystem | Not use a contained resource created on the value set as a component system when that resource is not a CodeSystem | |
| should remove and log error when exporting a value set that includes a component from a self referencing value set | Remove and log error when exporting a value set that includes a component from a self referencing value set | |
| should export a value set that includes a concept component with at least one concept | Export a value set that includes a concept component with at least one concept | |
| should export a value set that includes a concept component from a local complete code system name with at least one concept | Export a value set that includes a concept component from a local complete code system name with at least one concept | |
| should export a value set that includes a concept component from a local complete code system name with concepts added by CaretValueRules | Export a value set that includes a concept component from a local complete code system name with concepts added by CaretValueRules | |
| should export a value set that includes a concept component from a local complete code system name with at least one concept added by a RuleSet | Export a value set that includes a concept component from a local complete code system name with at least one concept added by a RuleSet | |
| should export a value set that includes a concept component from a local complete CodeSystem instance name with at least one concept | Export a value set that includes a concept component from a local complete CodeSystem instance name with at least one concept | |
| should export a value set that includes a concept component from a local complete CodeSystem instance name with at least one concept added by a RuleSet | Export a value set that includes a concept component from a local complete CodeSystem instance name with at least one concept added by a RuleSet | |
| should export a value set that includes a concept component from a local incomplete CodeSystem when the concept is not in the system | Export a value set that includes a concept component from a local incomplete CodeSystem when the concept is not in the system | |
| should export a value set that includes a concept component from a local incomplete CodeSystem instance when the concept is not in the system | Export a value set that includes a concept component from a local incomplete CodeSystem instance when the concept is not in the system | |
| should log an error when exporting a value set that includes a concept component from a local complete code system name when the concept is not in the system | Log an error when exporting a value set that includes a concept component from a local complete code system name when the concept is not in the system | |
| should log an error when exporting a value set that includes a concept component from a local complete CodeSystem instance name when the concept is not in the system | Log an error when exporting a value set that includes a concept component from a local complete CodeSystem instance name when the concept is not in the system | |
| should log an error when exporting a value set that includes a concept component from a local complete code system url when the concept is not in the system | Log an error when exporting a value set that includes a concept component from a local complete code system url when the concept is not in the system | |
| should export a value set that includes a concept component where the concept system includes a version | Export a value set that includes a concept component where the concept system includes a version | |
| should export a value set that includes a filter component with a regex filter | Export a value set that includes a filter component with a regex filter | |
| should export a value set that includes a filter component with a code filter | Export a value set that includes a filter component with a code filter | |
| should export a value set that includes a filter component with a code filter where the value is from a local complete system | Export a value set that includes a filter component with a code filter where the value is from a local complete system | |
| should export a value set that includes a filter component with a code filter where the value is from a local incomplete system and the code is not in the system | Export a value set that includes a filter component with a code filter where the value is from a local incomplete system and the code is not in the system | |
| should export a value set that includes a filter component with a code filter where the value is from a local complete Instance of CodeSystem | Export a value set that includes a filter component with a code filter where the value is from a local complete Instance of CodeSystem | |
| should export a value set that includes a filter component with a code filter where the value is from a local incomplete Instance of CodeSystem and the code is not in the system | Export a value set that includes a filter component with a code filter where the value is from a local incomplete Instance of CodeSystem and the code is not in the system | |
| should log an error when exporting a value set that includes a filter component with a code filter where the value is from a local complete system, but is not present in the system | Log an error when exporting a value set that includes a filter component with a code filter where the value is from a local complete system, but is not present in the system | |
| should log an error when exporting a value set that includes a filter component with a code filter where the value is from a local complete Instance of CodeSystem, but is not present in the system | Log an error when exporting a value set that includes a filter component with a code filter where the value is from a local complete Instance of CodeSystem, but is not present in the system | |
| should export a value set that includes a filter component with a string filter | Export a value set that includes a filter component with a string filter | |
| should export a value set that excludes a component | Export a value set that excludes a component | |
| should log a message when a value set has a logical definition without inclusions | Log a message when a value set has a logical definition without inclusions | |
| should log a message when a value set from system is not a URI | Log a message when a value set from system is not a URI | |
| should log a message when a value set from is not a URI | Log a message when a value set from is not a URI | |
| should log a message and not add the concept again when a specific concept is included more than once | Log a message and not add the concept again when a specific concept is included more than once | |
| should apply a CaretValueRule | Apply a CaretValueRule | |
| should apply a CaretValueRule with soft indexing | Apply a CaretValueRule with soft indexing | |
| should apply a CaretValueRule with extension slices in the correct order | Apply a CaretValueRule with extension slices in the correct order | |
| should apply a CaretValueRule that assigns an inline Instance | Apply a CaretValueRule that assigns an inline Instance | |
| should apply a CaretValueRule that assigns an inline Instance with a numeric id | Apply a CaretValueRule that assigns an inline Instance with a numeric id | |
| should apply a CaretValueRule that assigns an inline Instance with an id that resembles a boolean | Apply a CaretValueRule that assigns an inline Instance with an id that resembles a boolean | |
| should log a message when trying to assign an Instance, but the Instance is not found | Log a message when trying to assign an Instance, but the Instance is not found | |
| should log a message when trying to assign a value that is numeric and refers to an Instance, but both types are wrong | Log a message when trying to assign a value that is numeric and refers to an Instance, but both types are wrong | |
| should log a message when trying to assign a value that is boolean and refers to an Instance, but both types are wrong | Log a message when trying to assign a value that is boolean and refers to an Instance, but both types are wrong | |
| should export a value set with an extension | Export a value set with an extension | |
| should log a message when applying invalid CaretValueRule | Log a message when applying invalid CaretValueRule | |
| should use the url specified in a CaretValueRule when referencing a named value set | Use the url specified in a CaretValueRule when referencing a named value set | |
| should use the url specified in a CaretValueRule when referencing a named code system | Use the url specified in a CaretValueRule when referencing a named code system | |
| should apply a CaretValueRule at an included concept | Apply a CaretValueRule at an included concept | |
| should apply a CaretValueRule at an included concept when there is a compose rule for a filter on the system first | Apply a CaretValueRule at an included concept when there is a compose rule for a filter on the system first | |
| should apply a CaretValueRule at a concept from a code system defined in FSH identified by name | Apply a CaretValueRule at a concept from a code system defined in FSH identified by name | |
| should apply a CaretValueRule at a concept from a code system defined in FSH identified by id | Apply a CaretValueRule at a concept from a code system defined in FSH identified by id | |
| should apply a CaretValueRule at an excluded concept | Apply a CaretValueRule at an excluded concept | |
| should apply a CaretValueRule at an excluded concept when there is a compose rule for a filter on the system first | Apply a CaretValueRule at an excluded concept when there is a compose rule for a filter on the system first | |
| should apply a CaretValueRule that assigns an instance at a concept | Apply a CaretValueRule that assigns an instance at a concept | |
| should log an error when a CaretValueRule is applied at a concept that is neither included nor excluded | Log an error when a CaretValueRule is applied at a concept that is neither included nor excluded | |
| should not throw an error when caret rules are applied to a code from a specific version of a codeSystem | Not throw an error when caret rules are applied to a code from a specific version of a codeSystem | |
| should output an error when a choice element has values assigned to more than one choice type | Output an error when a choice element has values assigned to more than one choice type | |

#### `#insertRules`

| Test name | Description | Ported |
|-----------|-------------|--------|
| should apply rules from an insert rule | Apply rules from an insert rule | |
| should apply a CaretValueRule from a rule set with soft indexing | Apply a CaretValueRule from a rule set with soft indexing | |
| should apply concept-creating rules from a rule set and combine concepts from the same system | Apply concept-creating rules from a rule set and combine concepts from the same system | |
| should apply concept-creating rules from a rule set and combine concepts from the same system and valuesets | Apply concept-creating rules from a rule set and combine concepts from the same system and valuesets | |
| should apply concept-creating rules from a rule set and combine excluded concepts from the same system and valuesets | Apply concept-creating rules from a rule set and combine excluded concepts from the same system and valuesets | |
| should log an error and not apply rules from an invalid insert rule | Log an error and not apply rules from an invalid insert rule | |

---

