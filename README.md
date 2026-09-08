# TOTP Conformance Tests

[![CI](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/ci.yml/badge.svg)](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/ci.yml)
[![Nightly](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/nightly.yml/badge.svg)](https://github.com/OpenChargingCloud/TOTPConformanceTests/actions/workflows/nightly.yml)

Conformance test suite for the **Open Charging Technology TOTP token format** — the
machine-to-machine Time-based One-Time Password format shared by two
independent implementations:

 * **[Vanaheimr Hermod](https://github.com/Vanaheimr/Hermod)** (`TOTPGenerator`, C#/.NET)
 * **[TOTP.TS](https://github.com/OpenChargingCloud/TOTP.TS)** (`@open-charging-cloud/totp`, TypeScript/Node)

Both derive **byte-identical tokens** for the same shared secret, time slot,
length, alphabet and hash algorithm. That agreement used to rest on
hand-duplicated expected values in the two test suites; it now rests on a
**written specification** with a **normative test-vector annex** — maintained
in [OpenChargingTechnology/Whitepapers → TimeBasedOneTimePasswords](https://github.com/OpenChargingTechnology/Whitepapers/tree/master/TimeBasedOneTimePasswords)
and consumed here as the `libs/specification` submodule — and a harness per
implementation running against the very same vectors, so a divergence fails
in a test run instead of in the field.

The format is used for OCPP QR-code based payment/authorization flows (secure
dynamic QR codes on charging station displays), as a time-limited replacement
for legacy HTTP Basic Auth, and to strengthen OCPI-style token
authentication.

## What is in here

| Directory | Content |
|-----------|---------|
| [`libs/specification`](https://github.com/OpenChargingTechnology/Whitepapers/tree/master/TimeBasedOneTimePasswords) | **The specification** (git submodule → `TimeBasedOneTimePasswords/` in OpenChargingTechnology/Whitepapers): the token format (v1.0, final — frozen by deployed verifiers, described exactly as implemented), the HTTP bindings (v1.0, draft — Basic migration path, the `Authorization: TOTP` auth-params scheme, the `TOTP` request header), the **normative test-vector annex** this suite executes (38 generation vectors including the OCPP 2.1 "TOTP algorithm, version 1" profile, 18 invalid-input vectors, the HTTP header vectors — generated, never hand-edited), and the vector generator (an independent third implementation verifying the hand-cross-validated anchors before writing anything). |
| `Tests/TOTPConformance_Tests/` | The **C# harness** (NUnit): runs Hermod's `TOTPGenerator` against the vectors, plus format property tests and characterization tests pinning the known deviations. |
| `Tests/TOTPConformance_Tests.TS/` | The **TypeScript harness** (vitest): runs TOTP.ts against the same vectors, plus the same format property tests. |
| [`docs/spec-deviations.md`](docs/spec-deviations.md) | What the implementations deviate in, pinned by characterization tests so a fix cannot go unnoticed. The input validation gaps this suite found in Hermod are fixed; remaining: a theoretical year-9999 `EndTime` overflow. |
| [`docs/ocpp-totp-comparison.md`](docs/ocpp-totp-comparison.md) | The format's origin story, held to account: OCPP 2.1 (use case C25) already uses this token format for dynamic QR-code payments — its "TOTP algorithm, version 1" is the sha256/Base62 profile of the spec. The document maps the OCPP text to spec and implementations and lists where the OCPP text should be modernized (shared-secret minimum, URL placeholder vocabulary, test vectors, ...). |
| [`specs/`](specs/) | Verbatim extract of the TOTP-related sections of OCPP 2.1 Edition 2, Part 2 (use case C25, "TOTP algorithm, version 1", `WebPaymentsCtrlr`) — redistributed under the source document's CC BY-ND 4.0 license, © Open Charge Alliance. |
| `libs/` | Git submodules: the two implementations under test and Hermod's foundation. |

## Submodules

| Submodule | Purpose |
|-----------|---------|
| [`libs/specification`](https://github.com/OpenChargingTechnology/Whitepapers) | The specification and its normative test-vector annex (`TimeBasedOneTimePasswords/`) |
| [`libs/Hermod`](https://github.com/Vanaheimr/Hermod) | Implementation under test #1: `TOTPGenerator` (C#) |
| [`libs/TOTP.TS`](https://github.com/OpenChargingCloud/TOTP.TS) | Implementation under test #2: `@open-charging-cloud/totp` (TypeScript) |
| [`libs/Styx`](https://github.com/Vanaheimr/Styx) | Foundation of Hermod |

```bash
git clone --recurse-submodules https://github.com/OpenChargingCloud/TOTPConformanceTests.git
```

## Building and testing

The C# harness (requires the .NET 10 SDK):

```bash
dotnet test TOTPConformanceTests.slnx
```

The TypeScript harness (requires Node.js >= 18). It imports TOTP.TS **from
source**, so the implementation's own runtime dependencies have to be
installed as well — Node resolves them from `libs/TOTP.TS/node_modules`:

```bash
npm --prefix Tests/TOTPConformance_Tests.TS install
```

```bash
npm --prefix libs/TOTP.TS ci
```

```bash
npm --prefix Tests/TOTPConformance_Tests.TS test
```

The vector drift guard (requires Python >= 3.10; also verifies the anchors):

```bash
python libs/specification/TimeBasedOneTimePasswords/tools/generate-test-vectors.py --check
```

Regenerating after a vector change additionally refreshes the vendored
copies inside the implementation submodules:

```bash
python libs/specification/TimeBasedOneTimePasswords/tools/generate-test-vectors.py --mirror libs/Hermod/HermodTests/TOTP/TestVectors --mirror libs/TOTP.TS/test/vectors
```

(then commit the repositories in order: specification, implementations, the
pins here).

## How the vectors stay trustworthy

1. The generator re-derives the **anchor vectors** — the token sets that were
   hand-cross-validated between Hermod and TOTP.ts before this suite
   existed — and refuses to write when even one mismatches.
2. Every emitted vector is then confirmed by **both** implementation
   harnesses (the TLS channel binding vectors by Hermod and the generator —
   TOTP.ts does not implement that extension).
3. CI runs the generator in `--check` mode against the pinned specification
   submodule, so hand-edits of the annex and generator drift are caught.
4. The CI gate runs against the pinned submodules on Windows and Debian 13
   (the platform Hermod runs on in production); the nightly workflow advances
   all four submodules — the specification included — to their upstream HEADs
   and runs both harnesses again, so a format-breaking change in Hermod, Styx
   or TOTP.ts master, or an annex change without follow-up here, is noticed
   even on a day nobody pushes to this repository.
5. Both implementation repositories run their **own** test suites against
   vendored copies of the vector files (their hand-duplicated expected values
   are gone); the generator's `--mirror` invocation refreshes the copies in
   the checked-out submodules, and `VendoredCopiesTests` fails when a copy
   drifts — see the
   [annex README](https://github.com/OpenChargingTechnology/Whitepapers/tree/master/TimeBasedOneTimePasswords/test-vectors).

Vectors marked `requires: ["tlsChannelBinding"]` are skipped by harnesses
without that capability; invalid-input vectors marked
`knownDeviations`/`notApplicable` are skipped by the named implementation and
pinned by characterization tests instead — the semantics are documented in
the annex README.

## License

Apache License 2.0 — see [LICENSE](LICENSE).

Exceptions: the OCPP specification extract in
[`specs/`](specs/OCPP-2.1-Ed2-Part2-TOTP-extract.md) is © Open Charge
Alliance and redistributed under
[CC BY-ND 4.0](https://creativecommons.org/licenses/by-nd/4.0/), as declared
by the source document. The specification itself (the `libs/specification`
submodule) is published in OpenChargingTechnology/Whitepapers under
[CC-BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
