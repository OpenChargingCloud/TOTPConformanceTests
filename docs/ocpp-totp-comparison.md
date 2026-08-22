# OCPP 2.1 vs. the TOTP token format specification

The TOTP token format of this repository was originally developed **for OCPP
v2.1**: use case **C25 "Ad hoc payment via a QR code"** (OCPP 2.1 Edition 2,
Part 2 - Specification, © Open Charge Alliance, 2025-12-03, pp. 145–153)
normatively defines "**TOTP algorithm, version 1**", and the C# sample code
printed there is the direct ancestor of Hermod's `TOTPGenerator`. This
document compares the OCPP text against [our
specification](../spec/totp-token-format.md) and the two implementations, and
collects the points where the OCPP text should be modernized.

Clause references: C25 requirements `C25.FR.xx`, the unnumbered sections "URL
definition for dynamic QR code", "Time-based One-Time password algorithm" and
"Validation of TOTP", and component `WebPaymentsCtrlr` (Part 2, "Referenced
Components and Variables", section 2.4.2). A verbatim extract of these
sections is part of this repository:
[`specs/OCPP-2.1-Ed2-Part2-TOTP-extract.md`](../specs/OCPP-2.1-Ed2-Part2-TOTP-extract.md)
— the OCPP specification is published under CC BY-ND 4.0, so the excerpt is
redistributed with attribution and word-for-word (see the file's header).

## 1. Where OCPP and this specification agree exactly

OCPP's "TOTP algorithm, version 1" and section 4 of our specification derive
**byte-identical tokens** for the parameters OCPP can express. Verified
element by element against the OCPP pseudocode and C# sample:

| Element | OCPP 2.1 "v1" | Our spec §4 |
|---|---|---|
| Time slot | `floor(secondsSinceEpoch / validityTime)` | identical (§4.1) |
| Slot encoding | `UInt64`, big-endian (little-endian hosts reverse) | identical: 8 bytes big-endian, unsigned 64 bit (§4.2) |
| Keyed function | HMAC-SHA256, key = UTF-8 bytes of SharedSecret | identical; SHA-256 is the default of the algorithm family (§3) |
| Offset | `hash[last] & 0x0F` | identical (§4.2 step 4) |
| Character mapping | `base62[hash[(offset+i) % hash.Length] % base62.Length]` | identical, including both frozen quirks — modulo bias and ring buffer (§4.3) |
| Alphabet | Base62, exactly our default alphabet string | identical (§3) |
| Acceptance window | current, then previous, then next interval ("Validation of TOTP") | the previous/current/next triple (§4.4, §7) — same ±1-slot window |

The `ocpp-v1-length-8` and `ocpp-v1-min-profile` vectors in
`TestVectors/totp-test-vectors.json` pin OCPP-representable parameter sets, so
both implementations remain provably compatible with OCPP C25 deployments.

**Conclusion: OCPP TOTP v1 is the profile of this specification with
`hashAlgorithm = sha256` and the default Base62 alphabet.** Our specification
is the v1-compatible superset (hash agility, alphabet agility, length range
4–255, TLS channel binding).

## 2. Conflicts between the OCPP text and the deployed implementations

These are the points where a literally-configured OCPP deployment collides
with what the implementations (and our specification) enforce — the most
important candidates for the next OCPP revision.

### 2.1. `SharedSecret` minimum length: 8 vs. 16 characters — hard conflict

`WebPaymentsCtrlr.SharedSecret`: "Must be at least 8 characters long."
Both implementations reject secrets shorter than **16** characters
(`The length of the given shared secret must be at least 16 characters!`), as
does our specification (§3). A CSMS setting a spec-legal 9-character secret
via `SetVariables` produces a charging station that cannot generate a single
token with these libraries.

8 random Base62 characters carry ≈ 47.6 bits — far below the ≥ 128 bits that
RFC 4226 (section 7.5, R6) **requires** for HOTP-family shared secrets.
→ Raise the minimum to 16 characters and RECOMMEND ≥ 22 random Base62
characters (≈ 128 bits), i.e. align with our spec §8.

### 2.2. URL template placeholders: Hermod's vocabulary differs from C25.FR.50–53

OCPP normatively defines the placeholders `{chargingstationid}` /
`{roamingevseid}`, `{evse}`, `{totp}`, `{version}` (lowercase). Hermod's
`TOTPGenerator.ProcessURLTemplate` (`Hermod/TOTP/TOTP.cs`) replaces:

* `{TOTP}` — **case-sensitively**, so OCPP's lowercase `{totp}` stays
  unreplaced (every other placeholder there uses `OrdinalIgnoreCase`);
* `{evseId}` — OCPP says `{evse}`: no match;
* `{evRoamingEVSEId}` — OCPP says `{roamingevseid}`: no match;
* `{chargingstationid}` — not supported at all;
* plus a superset OCPP does not know: `{connectorId}`, `{maxPower}`,
  `{maxEnergy}`, `{maxTime}`, `{maxSoC}`, `{maxCost}`, `{tariffId}`,
  `{chargingProfile}`, `{endTime}`, `{uiLanguage}`, `{currency}`,
  `{signature}`.

An OCPP-conforming `WebPaymentsCtrlr.URLTemplate` such as
`https://qr.example.com/{totp}/{version}/{chargingstationid}/` therefore
passes through Hermod's URL generator with `{totp}` and `{chargingstationid}`
literally intact. Two-sided action item:

* **Hermod**: replace `{totp}` case-insensitively and add
  `{chargingstationid}`, `{evse}`, `{roamingevseid}` as aliases.
* **OCPP**: state explicitly that placeholder matching is case-insensitive
  (or normatively lowercase-only), and consider adopting the useful superset
  (see 3.2).

## 3. Where the OCPP text should be modernized

Ordered roughly by impact; 1–3 are the substantive ones.

1. **Raise the SharedSecret floor** (see 2.1) — from 8 to at least 16
   characters, RECOMMEND ≈ 128 bits of randomness, and add a note that the
   secret is compared/keyed as raw UTF-8 **without Unicode normalization and
   without whitespace** (our spec §3). The variable being write-only and
   "random on first boot" is good and should stay.

2. **Add a secret-rotation procedure.** `SharedSecret` is WO and settable at
   any time, but C25 says nothing about the changeover: a CSMS that sets a new
   secret invalidates every QR code currently displayed until the station
   re-renders. One sentence fixes it: *during rotation the CSMS SHOULD accept
   tokens under both the previous and the new secret for at least one validity
   interval.*

3. **Version the algorithm forward instead of freezing on "v1".**
   `WebPaymentsCtrlr.TOTPVersion` (valuesList: `"v1"`) is exactly the right
   extension point. A future `"v2"` should reference the [token format
   specification](../spec/totp-token-format.md) as the algorithm family:
   configurable hash (sha256/sha384/sha512), configurable alphabet (with the
   no-bias sizes 16/32/64 RECOMMENDED), length 4–255. That turns the OCPP text
   from a re-specification into a profile of a maintained spec with canonical
   test vectors.

4. **Bound and explain the token length.** "Must be at least 6 characters"
   has no upper bound and no hint that positions beyond the 32-byte SHA-256
   hash repeat verbatim (ring buffer, our spec §4.3): a deployment configuring
   `Length = 40` gains nothing over 32 and may believe otherwise. State the
   useful range (6…32 for v1) and RECOMMEND 12 (the implementations'
   default). Six Base62 characters are ≈ 35.7 bits — thin when combined with
   the maximum `ValidityTime` of 3600 s and no mandated rate limiting; the
   weakest OCPP-legal corner (`Length = 6`, `ValidityTime = 3600`) is pinned
   by our `ocpp-v1-min-profile` vector on purpose.

5. **Add `maxsoc` as a URL query parameter.** `transactionLimit` explicitly
   supports `maxSoC` (C25.FR.24 note, E16), but the URL parameter list
   (C25.FR.56–59, `WebPaymentsCtrlr.URLParameters`) only defines `maxenergy`,
   `maxtime`, `maxcost` — an EV driver can cap cost, energy and time via the
   QR flow, but not state of charge. Hermod's template vocabulary
   (`{maxSoC}`) already anticipates it.

6. **Specify the slot encoding in the pseudocode, not only in the C# sample.**
   "convert timeInterval to bytes" leaves the width open; only the C# sample
   (`UInt64` + `BitConverter`) pins 8 bytes. Say: *64-bit unsigned integer, 8
   bytes, big-endian (network byte order)* — and that `timeInSeconds` is the
   **whole** number of seconds (millisecond fractions floored). Also fix the
   loop bound ambiguity: "for i from 0 to totpLength" reads inclusive; the C#
   is `i < totpLength`.

7. **Harden the "Validation of TOTP" section.** It defines the
   current/previous/next window (good, matches §7 of our spec) but should
   also say: comparison SHOULD be constant-time; the window MUST NOT be
   widened further (increase `ValidityTime` instead); CSMS SHOULD rate-limit
   failed validations per station; and where replay within one interval
   matters, accepted (station, slot, token) triples SHOULD be single-use.
   A leaked dynamic-QR URL is otherwise replayable for up to `ValidityTime`
   plus one interval.

8. **State the input constraints as requirements.** The implementations
   reject empty/whitespace/short secrets, bad lengths, bad validity times and
   pre-epoch timestamps with shared error messages (our spec §5); the OCPP
   text has only the two variable-table bounds. Referencing the validation
   table (or this suite's `totp-invalid-inputs.json`) would make CSMS and
   station behaviour testable.

9. **Publish test vectors.** The OCPP text contains none — the single example
   token `aHicux89` in Figure 46 is illustrative, not derivable. The
   canonical vectors in this repository (`TestVectors/`) cover the OCPP v1
   profile explicitly and are Apache-2.0 — they could be referenced or
   included in the next edition.

10. **Modernize the C# sample.** `DateTime.UtcNow - new DateTime(1970, 1, 1)`
    predates `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`; the sample performs
    none of the input validation; and it returns only the current token even
    though validation needs previous/next as well. Pointing at the two
    maintained open-source implementations (Hermod, TOTP.ts) would shorten
    the section and keep it current.

11. **Editorial.** "verfication" (C25 error handling row), "entered bij EV
    Driver" (scenario #1 step 9), "The URL is a defined as a template" (URL
    definition), "the users inputs values" (C25.FR.03 note),
    "WebPaymentCtrlr.RoamingEvseId" missing the "s" (URL pointing to external
    party), and C25.FR.23 speaks of "Payment Kiosk" — copied from C24; in C25
    the authorizing party is the PSP.

## 4. Differences that are fine as they are

* **ValidityTime range 6…3600 s** is a legitimate deployment profile of our
  "any positive integer" (§3). No change needed beyond the length coupling in
  point 4.
* **`{version}` in the URL** is OCPP's out-of-band algorithm signal; our
  format deliberately has no in-band version. Compatible by construction.
* **TLS v1.3 channel binding** (our §6) does not apply to the C25 QR flow —
  the token travels via the EV driver's phone, not over the station↔CSMS TLS
  session. It stays an extension for direct machine-to-machine links (e.g.
  OCPI-style header auth); no OCPP action needed.
* **Static QR codes** (scenario #2) involve no TOTP at all; the fraud WARNING
  in C25 covers the trade-off.
