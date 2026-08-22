# Known deviations from the specification

The [token format specification](../libs/specification/TimeBasedOneTimePasswords/totp-token-format.md)
requires input validation (section 5) beyond what the implementations
historically performed. Deviations are

 * marked on their vector in `totp-invalid-inputs.json` of the
   specification's annex
   (`"knownDeviations": ["<implementation>"]`), so the conformance harness
   skips them instead of failing, and
 * **pinned** by a characterization test in
   `Tests/TOTPConformance_Tests/KnownDeviationTests.cs`, so a fix cannot go
   unnoticed: when the implementation changes, the characterization test
   fails — then remove the marker in the annex generator
   (`libs/specification/TimeBasedOneTimePasswords/tools/generate-test-vectors.py`),
   regenerate (commit Whitepapers first, then the implementation repos, then
   the pins here), and delete the characterization test.

TOTP.ts has no deviations: it implements every validation rule of
specification section 5.


## Hermod

### timestamp-year-9999 (last slot of the calendar)

Found by the `timestamp-year-9999` vector, which therefore uses
9999-12-31T23:59:**28**Z: within the very last slot of the calendar Hermod's
`EndTime` convenience value (`timestamp + remainingTime`) would reach the
year 10000 and leave the `DateTime` range, so `GenerateTOTPs` throws an
`ArgumentOutOfRangeException` instead of returning the — perfectly derivable
— tokens. TOTP.ts returns no end time and computes the same slot fine.

Purely theoretical, recorded for completeness. Pinned by
`KnownDeviationTests.LastSlotOfTheCalendar_EndTimeOverflows`.


## Fixed deviations

The following input validation gaps in Hermod were found by this suite and
**fixed in Hermod on 2026-08-22** (`Hermod/TOTP/TOTP.cs`, both `Initial
Checks` regions, in the same check order as TOTP.ts). Their vectors run as
regular `InvalidInputVectorTests` on both implementations now:

 * **validity-zero** — used to reach `currentUnixTime % 0` and throw a
   `DivideByZeroException`; now rejected with the shared message
   `The validity time must be a positive integer number of seconds!`.
 * **validity-negative** — used to be silently accepted (the slot quotient
   saturated to 0, the remaining time went negative); now rejected.
 * **validity-fractional** — used to be silently accepted (the `TimeSpan`
   parameter made e.g. 4.5 s representable, with slot arithmetic outside the
   specified format); now rejected: the validity must be a whole, positive
   number of seconds (`Ticks % TimeSpan.TicksPerSecond == 0`).
 * **timestamp-negative** — pre-epoch timestamps used to saturate to slot 0
   silently; now rejected with the shared message
   `The timestamp must be a non-negative Unix timestamp in milliseconds!`.


## Differences that are NOT deviations

 * **TLS v1.3 channel binding** (spec section 6) is an OPTIONAL extension,
   implemented by Hermod only. The vectors requiring the `tlsChannelBinding`
   capability are skipped by the TOTP.ts harness — by design, not as a
   deviation.
 * **Lenient hash algorithm parsing**: Hermod's
   `TOTPHashAlgorithmExtensions.TryParse` also accepts `SHA-256`,
   `HMAC_SHA512` etc. Spec section 3 explicitly allows this (MAY).
 * **Rejection point for unknown hash algorithms**: Hermod rejects them at
   its parsing boundary (string → enum), TOTP.ts inside the generator. Spec
   section 5 allows both.
 * **Fractional token length** (`length-fractional`) is unrepresentable in
   Hermod's typed API (`UInt32`) — a rejection by construction, marked
   `notApplicable`, not a deviation.
 * **`EndTime` return value**: Hermod additionally returns
   `timestamp + remainingTime`; TOTP.ts returns only the remaining seconds.
   Return shapes are API surface, not token format.
