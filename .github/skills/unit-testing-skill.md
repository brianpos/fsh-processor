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

## Test authoring notes
- Keep tests deterministic and small.
- Use `Console.WriteLine` only for useful diagnostics.
- Avoid broad assertions that hide which rule regressed.
- Prefer adding new test methods over weakening existing assertions.
