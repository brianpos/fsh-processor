# Unit Testing Skill: Parser and Compiler Conformance

Use this checklist when adding or updating tests in `fsh-tester` and `fsh-compiler-tester-R4`.

## Primary goals

1. Verify compiler changes remain aligned with `Docs/language-reference.md`.
2. Preserve parser round-trip behavior (`parse -> serialize -> parse`) without loss.
3. Keep compiler behavior conformant to the language specification.
4. Enforce exact JSON output parity for migration tests against sushi.
5. Add targeted regression tests for newly discovered edge cases.

## Required checklist

### 1) Specification alignment
- For every compiler behavior change, add or update a test that asserts the expected behavior from `Docs/language-reference.md`.
- Prefer test names that describe the rule being validated.
- If possible, cite the relevant spec section in a test comment.

### 2) Parser round-trip must always hold
- When parser/serializer behavior is touched, add or update a round-trip test:
  - parse FSH input
  - serialize it
  - parse the serialized output
  - assert structural equivalence and retained semantics
- Do not accept changes that drop comments/whitespace fidelity when round-tripping is expected.

### 3) Compiler conformance
- For compiler changes, include tests that validate generated FHIR artifacts (metadata, differentials, bindings, constraints, cardinalities, etc.) against expected spec behavior.
- Prefer narrow tests with one focused assertion area per test method.

### 4) Sushi migration JSON comparisons must be strict
- For compiler migration parity tests, JSON comparisons must remain strict text equality.
- Do not normalize, reorder, or semantically compare JSON for these migration assertions.
- If output differs, treat as a regression unless the expected baseline is intentionally updated.

### 5) Edge-case regression discipline
- If file-based analysis uncovers an untested edge case, add a new dedicated test for that edge case.
- Ensure expected behavior is consistent with `Docs/language-reference.md`.
- If the spec does not explicitly cover the edge case, add a test comment stating that the behavior is intentionally pinned for regression safety.
- If the spec does cover it, add a test comment referencing the spec.

## Dependency detection in `SequenceFshDocs`

The `SequenceFshDocs` test in `fsh-compiler-tester-R4` dynamically auto-detects file-level
dependencies via `ComputeFileDependencies()` and validates them against the hardcoded
`_fileDependencies` dictionary.  Whenever `_fileDependencies` is updated for a new dependency
type, `ComputeFileDependencies()` must also be updated to detect that type automatically.

Currently detected dependency types:

| Dependency type | Where detected |
|---|---|
| Profile/Instance `Parent`/`InstanceOf` pointing to a non-core type | Entity-level scan |
| `Canonical(localName)` in `InstanceFixedValueRule` | Instance rule scan |
| `NameValue` (cross-instance embed) in `InstanceFixedValueRule` | Instance rule scan |
| `Reference(localName)` (bare local instance) in `InstanceFixedValueRule` | Instance rule scan |
| `extension[Name]` in RuleSet path (non-param) | RuleSet path scan |
| `extension[Name]` in parameterized RuleSet `UnparsedContent` | RuleSet raw-text regex |
| `Reference({param})` in parameterized RuleSet `UnparsedContent` | `ruleSetReferenceParamIdxs` regex + call-site param resolution |
| ValueSet binding (`from Name`) | `ValueSetRule` scan |
| Mapping `Source` | Mapping entity scan |

If a new dependency type is added to the compiler (e.g. a new value class that requires a local
entity to be in scope), both `_fileDependencies` **and** `ComputeFileDependencies()` must be
updated.  Run `SequenceFshDocs` after updating `_fileDependencies`; if it fails with a MISMATCH,
add the corresponding detection logic to `ComputeFileDependencies()`.

> **Note:** `SequenceFshDocs` may be updated even when general instruction says "do not update
> tests", unless `SequenceFshDocs` itself is explicitly called out as off-limits.

## Test authoring notes
- Keep tests deterministic and small.
- Use `Console.WriteLine` only for useful diagnostics.
- Avoid broad assertions that hide which rule regressed.
- Prefer adding new test methods over weakening existing assertions.
