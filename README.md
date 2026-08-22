# TOTP Conformance Tests

[![CI](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/ci.yml/badge.svg)](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/ci.yml)
[![Nightly](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/nightly.yml/badge.svg)](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/nightly.yml)

Conformance test suite for the **Open Charging Cloud TOTP token format** — the
machine-to-machine Time-based One-Time Password format shared by two
independent implementations:

 * **[Vanaheimr Hermod](https://github.com/Vanaheimr/Hermod)** (`TOTPGenerator`, C#/.NET)
 * **[TOTP.ts](https://github.com/OpenChargingCloud/TOTP.ts)** (`@open-charging-cloud/totp`, TypeScript/Node)

Both derive **byte-identical tokens** for the same shared secret, time slot,
length, alphabet and hash algorithm. That agreement used to rest on
hand-duplicated expected values in the two test suites; this repository
replaces it with a **written specification**, **canonical test vectors**, and
a harness per implementation running against the very same vectors — so a
divergence fails in a test run instead of in the field.

The format is used for OCPP QR-code based payment/authorization flows (secure
dynamic QR codes on charging station displays), as a time-limited replacement
for legacy HTTP Basic Auth, and to strengthen OCPI-style token
authentication.

## What is in here

| Directory | Content |
|-----------|---------|
| [`spec/totp-token-format.md`](spec/totp-token-format.md) | **The specification** (v1.0 draft): token derivation, parameters, validation rules, verification, the TLS v1.3 channel binding extension, security considerations. The format predates the document and is frozen by deployed verifiers; the spec describes it exactly as implemented. |
| [`TestVectors/`](TestVectors/) | **The canonical test vectors** (normative): 38 generation vectors (including the OCPP 2.1 "TOTP algorithm, version 1" profile) and 18 invalid-input vectors, shared by all harnesses. Generated — never hand-edited. |
| [`tools/generate-test-vectors.py`](tools/generate-test-vectors.py) | The vector generator: an independent third implementation of the spec (Python stdlib), which verifies the historical hand-cross-validated anchor values before writing anything. Three implementations agree on every vector. |
| `Tests/TOTPConformance_Tests/` | The **C# harness** (NUnit): runs Hermod's `TOTPGenerator` against the vectors, plus format property tests and characterization tests pinning the known deviations. |
| `Tests/TOTPConformance_Tests.TS/` | The **TypeScript harness** (vitest): runs TOTP.ts against the same vectors, plus the same format property tests. |
| [`docs/spec-deviations.md`](docs/spec-deviations.md) | What the implementations deviate in, pinned by characterization tests so a fix cannot go unnoticed. The input validation gaps this suite found in Hermod are fixed; remaining: a theoretical year-9999 `EndTime` overflow. |
| [`docs/ocpp-totp-comparison.md`](docs/ocpp-totp-comparison.md) | The format's origin story, held to account: OCPP 2.1 (use case C25) already uses this token format for dynamic QR-code payments — its "TOTP algorithm, version 1" is the sha256/Base62 profile of the spec. The document maps the OCPP text to spec and implementations and lists where the OCPP text should be modernized (shared-secret minimum, URL placeholder vocabulary, test vectors, ...). |
| [`specs/`](specs/) | Verbatim extract of the TOTP-related sections of OCPP 2.1 Edition 2, Part 2 (use case C25, "TOTP algorithm, version 1", `WebPaymentsCtrlr`) — redistributed under the source document's CC BY-ND 4.0 license, © Open Charge Alliance. |
| `libs/` | Git submodules: the two implementations under test and Hermod's foundation. |

## Submodules

| Submodule | Purpose |
|-----------|---------|
| [`libs/Hermod`](https://github.com/Vanaheimr/Hermod) | Implementation under test #1: `TOTPGenerator` (C#) |
| [`libs/TOTP.TS`](https://github.com/OpenChargingCloud/TOTP.ts) | Implementation under test #2: `@open-charging-cloud/totp` (TypeScript) |
| [`libs/Styx`](https://github.com/Vanaheimr/Styx) | Foundation of Hermod |

```bash
git clone --recurse-submodules https://github.com/OpenChargingCloud/TOTPConformanceTests.git
```

## Building and testing

The C# harness (requires the .NET 10 SDK):

```bash
dotnet test TOTPConformanceTests.slnx
```

The TypeScript harness (requires Node.js >= 18):

```bash
npm --prefix Tests/TOTPConformance_Tests.TS install
npm --prefix Tests/TOTPConformance_Tests.TS test
```

The vector drift guard (requires Python >= 3.10; also verifies the anchors):

```bash
python tools/generate-test-vectors.py --check
```

## How the vectors stay trustworthy

1. The generator re-derives the **anchor vectors** — the token sets that were
   hand-cross-validated between Hermod and TOTP.ts before this repository
   existed — and refuses to write when even one mismatches.
2. Every emitted vector is then confirmed by **both** implementation
   harnesses (the TLS channel binding vectors by Hermod and the generator —
   TOTP.ts does not implement that extension).
3. CI regenerates the files in `--check` mode, so hand-edits and generator
   drift are caught.
4. The CI gate runs against the pinned submodules on Windows and Debian 13
   (the platform Hermod runs on in production); the nightly workflow advances
   all three submodules to their upstream HEADs and runs both harnesses
   again — so a format-breaking change in Hermod, Styx or TOTP.ts master is
   noticed even on a day nobody pushes here.
5. Both implementation repositories run their **own** test suites against
   vendored copies of the vector files (their hand-duplicated expected values
   are gone); the generator mirrors the canonical files into the submodules,
   and `VendoredCopiesTests` fails when a copy drifts — see
   [`TestVectors/README.md`](TestVectors/README.md).

Vectors marked `requires: ["tlsChannelBinding"]` are skipped by harnesses
without that capability; invalid-input vectors marked
`knownDeviations`/`notApplicable` are skipped by the named implementation and
pinned by characterization tests instead — the semantics are documented in
[`TestVectors/README.md`](TestVectors/README.md).

## License

Apache License 2.0 — see [LICENSE](LICENSE).

Exception: the OCPP specification extract in
[`specs/`](specs/OCPP-2.1-Ed2-Part2-TOTP-extract.md) is © Open Charge
Alliance and redistributed under
[CC BY-ND 4.0](https://creativecommons.org/licenses/by-nd/4.0/), as declared
by the source document.
