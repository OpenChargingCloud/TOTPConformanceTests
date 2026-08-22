# Canonical TOTP conformance test vectors

These files are the single source of truth for the
[token format specification](../spec/totp-token-format.md) — every
implementation (Hermod, TOTP.ts, and any future port) tests against the very
same JSON, so a divergence fails in a test run instead of in the field.

**The JSON files are generated — do not edit them by hand.** They come from
[`tools/generate-test-vectors.py`](../tools/generate-test-vectors.py), an
independent third implementation of the specification (pure Python stdlib,
sharing no code with either implementation under test). Before writing
anything, the generator re-derives the historical anchor values that were
hand-cross-validated between Hermod and TOTP.ts, and refuses to write when
one of them does not match:

```
python tools/generate-test-vectors.py           # regenerate
python tools/generate-test-vectors.py --check   # drift guard (CI)
```

## totp-test-vectors.json — generation vectors

Each vector: `input` → expected `previous`/`current`/`next` token and
`remainingTimeSeconds`.

 * An **absent** optional field in `input` means: the harness passes
   null/undefined, so that the implementation's **own defaulting** is
   exercised. An explicitly present field is passed as given (including
   untrimmed values — trimming is the implementation's job).
 * `unixTimestampMillis` is the timestamp in Unix **milliseconds**;
   implementations floor it to whole seconds.
 * `tlsExporterMaterialHex` is the TLS exporter material as lowercase hex.
 * `requires` lists capabilities a vector needs. A harness runs the vector
   iff its implementation provides **all** of them, and MUST fail (not skip)
   on capability names it does not know. Capabilities so far:
   * `tlsChannelBinding` — TLS v1.3 channel binding (spec section 6),
     currently Hermod only.

## totp-invalid-inputs.json — inputs that MUST be rejected

Each vector: `input` → `expectedError`, the shared error message of spec
section 5. Harnesses assert that the thrown message **starts with**
`expectedError` (the C# side appends the parameter name).

 * `knownDeviations` lists implementations that do not perform the check yet.
   Their harnesses skip the vector and pin the actual behaviour in a
   characterization test instead — see
   [`docs/spec-deviations.md`](../docs/spec-deviations.md).
 * `notApplicable` lists implementations whose typed API cannot represent the
   input at all (rejection by construction, not a deviation).

Implementation names used in these two fields: `hermod`, `totp.ts`.

## Consuming the vectors from the implementation repositories

Both implementation repositories can run against these files directly —
add this repository as a git submodule, or vendor the two JSON files and
compare them against this repository in CI. Their ids are stable; new vectors
only ever get new ids.
