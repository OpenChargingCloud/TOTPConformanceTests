# Open Charging Cloud TOTP — HTTP Authentication

**Version 1.0 — Draft**, 2026-08-22
Apache License 2.0 · [OpenChargingCloud/TOTPConformanceTests](https://github.com/OpenChargingCloud/TOTPConformanceTests)

This document specifies how the tokens of the
[Open Charging Cloud TOTP Token Format](totp-token-format.md) are carried in
HTTP for **machine-to-machine authentication** — as a drop-in replacement for
HTTP Basic Authentication and for long-lived static bearer tokens (e.g.
OCPI-style `Authorization: Token …`). Two bindings are defined:

 * the **`Authorization: TOTP` scheme** (section 4) — carries an identity
   *and* a token, the direct Basic replacement;
 * the **`TOTP` request header** (section 5) — carries a token only, for
   deployments where the peer's identity is already established, with an
   in-band signal for TLS v1.3 channel binding.

Both are implemented by [Vanaheimr
Hermod](https://github.com/Vanaheimr/Hermod); Appendix A records the
implementation status. Unlike the token format itself — frozen by deployed
verifiers and finalized — this binding document is a **draft**: it has a
single implementation, and its wire details may still change.


## 1. Introduction

HTTP Basic Authentication ships a static password with every request: one
captured request, one log line, one misconfigured proxy, and the credential
is compromised until somebody rotates it. Replacing the password with a TOTP
bounds that damage to the token's validity time (30 seconds by default), and
adding TLS channel binding (section 5.2) removes even that window: a bound
token is useless outside the TLS session it was derived for.

The tokens themselves — derivation, parameters, validation rules, the
previous/current/next acceptance window — are defined in the [token format
specification](totp-token-format.md) and are **not** redefined here. This
document only defines how they travel in HTTP requests and how HTTP servers
verify them.

Typical deployments: OCPP charging station WebSocket connections (the HTTP
Upgrade handshake, section 6, today mostly protected by Basic Auth), OCPI
peer connections, and internal service-to-service APIs.


## 2. Conventions

The key words "MUST", "MUST NOT", "REQUIRED", "SHOULD", "SHOULD NOT",
"RECOMMENDED", "MAY", and "OPTIONAL" are to be interpreted as described in
[RFC 2119] and [RFC 8174] when, and only when, they appear in all capitals.

*Token format spec* refers to [totp-token-format.md](totp-token-format.md).
*Identity* is the name under which the verifier looks up a peer's TOTP
configuration (shared secret, validity time, length, alphabet, hash
algorithm — the `TOTPConfig` of token format spec Appendix B). *Base64* is
the Base64 encoding of [RFC 4648] section 4, with padding.


## 3. Prerequisites

Client and verifier share a TOTP configuration per identity, provisioned out
of band. The verifier maintains a mapping *identity → TOTP configuration*;
the client knows its own identity and configuration. How the mapping is
provisioned and rotated is out of scope (token format spec section 8 states
the requirements on the secret itself).


## 4. The `Authorization: TOTP` scheme

### 4.1. Syntax

```abnf
credentials  = "TOTP" SP totp-type SP totp-cred
totp-type    = "0" / "1"
totp-cred    = base64-user ":" base64-totp
base64-user  = <Base64 of the UTF-8 encoded identity>
base64-totp  = <Base64 of the UTF-8 encoded TOTP>
```

The scheme name is matched case-insensitively. The type digit is
**MANDATORY** and has the same semantics as in the `TOTP` request header
(section 5.1), which puts its type digit before the value in the very same
way: `0` for a raw token, `1` for a token bound to this TLS session
(section 5.2). Verifiers MUST reject credentials without a type digit and
credentials whose type digit is neither `0` nor `1`. `totp-cred` is a
single whitespace-free parameter; Base64 never contains a `":"`, so its
split is unambiguous.

Example — identity `chargingstation-0001`, raw token `CN63y502maVh` (the
`defaults-mid-slot` vector of the canonical test vectors):

```http
GET /ocpp HTTP/1.1
Host: csms.example.com
Authorization: TOTP 0 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Q042M3k1MDJtYVZo
```

The same identity with a TLS-channel-bound token (`gAzxPfYtmRgd`, the
`tls-binding-sha256` vector):

```http
Authorization: TOTP 1 Y2hhcmdpbmdzdGF0aW9uLTAwMDE=:Z0F6eFBmWXRtUmdk
```

Unlike Basic Authentication — which Base64-encodes `user:password` as one
string and therefore cannot represent a `":"` inside the username ([RFC
7617] section 2) — identity and token are encoded **separately** here, so
both may contain any Unicode character, including `":"`.

### 4.2. Client behaviour

For every request the client computes the **current** token for its
configuration (token format spec section 4) and sends it as above. A token
MAY be reused for consecutive requests within its remaining validity time;
clients MUST NOT send a cached token beyond that.

### 4.3. Server verification

On receiving the header, the verifier:

1. Reads the type digit and splits `totp-cred` at the `":"` into exactly
   two segments, which are Base64-decoded and UTF-8-decoded into identity
   and presented token. A missing or unknown type digit, malformed
   encodings and any other segment count are rejected as malformed
   (section 7).
2. Looks up the TOTP configuration for the identity. An unknown identity is
   rejected exactly like a wrong token (section 7).
3. Computes `previous`, `current` and `next` for its own clock and the
   identity's configuration — for type `1` with the exporter material of the
   receiving TLS session applied on the verifier's side as well, under the
   rules of section 5.2 — and accepts iff the presented token equals one
   of the three: the acceptance window of token format spec section 7,
   including its guidance: constant-time comparison, no widening of the
   window, single-use enforcement and rate limiting where replay matters.

Requests without an `Authorization` header, and requests that fail
verification, are answered with `401 Unauthorized`; the response SHOULD
carry a challenge:

```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: TOTP realm="ocpp"
```

The `realm` parameter is OPTIONAL and follows [RFC 9110] section 11.


## 5. The `TOTP` request header

### 5.1. Syntax

For deployments where the peer's identity is already established — a mutual
TLS connection, a per-client URL path, an already-authenticated session —
the token travels in a dedicated request header, prefixed with a one-digit
**type**:

```abnf
TOTP-header  = "TOTP:" OWS totp-type SP totp-value OWS
totp-type    = "0" / "1"
totp-value   = 1*VCHAR   ; a token of the token format spec
```

| Type | Meaning |
|------|---------|
| `0`  | A raw token (token format spec section 4). |
| `1`  | A token bound to this TLS session (token format spec section 6). |

Examples — the `defaults-mid-slot` and `tls-binding-sha256` vectors:

```http
TOTP: 0 CN63y502maVh
```

```http
TOTP: 1 gAzxPfYtmRgd
```

Verifiers MUST reject unknown type digits.

### 5.2. Type `1`: TLS v1.3 channel binding

A type `1` token is derived with TLS exporter material per token format spec
section 6 (label `EXPORTER-Time-Based-One-Time-Password-v1`, empty context,
32 bytes RECOMMENDED). The verifier derives the **same** material from its
own side of the TLS connection the request arrived on, and verifies against
tokens computed with it. A captured type `1` token is useless on any other
connection, which makes replay impractical even if the token leaks into a
log.

Type `1` MUST only be used on TLS v1.3 (or newer) connections — earlier TLS
versions have no exporter interface with these properties. A verifier that
cannot derive exporter material for the receiving connection MUST reject a
type `1` token; it MUST NOT fall back to verifying it as type `0`.

These rules apply to type `1` in **either** binding — the `TOTP` request
header and the `Authorization: TOTP` scheme (section 4.1) carry the same
type semantics.

### 5.3. Verification

As in section 4.3, steps 2–3, with the identity taken from the connection
context instead of the header, and — for type `1` — the exporter material of
the receiving TLS session applied to the token derivation on the verifier's
side as well.


## 6. WebSocket handshakes

Both bindings apply unchanged to the HTTP Upgrade request of a WebSocket
handshake ([RFC 6455]) — the primary use in OCPP, where a charging station's
WebSocket connection is today typically protected by HTTP Basic Auth.
Verification happens once, during the handshake; an accepted connection
stays authenticated for its lifetime. Deployments needing periodic
re-authentication of long-lived connections re-establish the connection or
use an application-level mechanism; in OCPP the TOTP then also plays its
other role, in dynamic QR codes (token format spec section 1.2).


## 7. Error handling

 * Syntactically malformed credentials (bad Base64, missing `":"`, unknown
   type digit, empty token): `400 Bad Request` — or `401` as below; a server
   MAY treat malformed as failed.
 * Failed verification and unknown identities: `401 Unauthorized`. The
   response MUST NOT distinguish "unknown identity" from "wrong token"
   (identity enumeration).
 * Servers SHOULD NOT write presented tokens to logs. A leaked raw token
   dies with its slot, but log lines outlive validity windows and reveal
   token length and alphabet; a leaked type `1` token is harmless, which is
   one more reason to prefer it.


## 8. Security considerations

 * **TLS is REQUIRED** for the `Authorization: TOTP` scheme and for type `0`
   of the `TOTP` header: a raw token is replayable within its acceptance
   window by anyone who observes it. (This is still a categorical
   improvement over Basic Auth, where observation compromises the
   credential *permanently*.) Type `1` is self-protecting against replay,
   but the identity and everything else in the request still deserve TLS.
 * The full guidance of token format spec sections 7 and 8 applies:
   constant-time comparison, single-use enforcement per (identity, slot,
   token) where replay matters, rate limiting of failed attempts, clock
   synchronization, and randomly generated secrets of at least 128 bits.
 * The scheme name `TOTP` is **not** registered in the IANA HTTP
   Authentication Scheme Registry; this is a private-use scheme between
   consenting implementations. Its two whitespace-separated parameters also
   do not follow the generic `token68`/auth-param credentials grammar of
   [RFC 9110] section 11.4 — generic HTTP libraries treat such a value as
   opaque, which is fine, and one more reason the scheme stays private-use
   (informative).


## 9. Test vectors

The token values in the examples above come from the canonical vectors of
this repository (`defaults-mid-slot`, `tls-binding-sha256` — see
[`TestVectors/`](../TestVectors/)). Header-level vectors — credential
encoding, malformed-header rejection — are future work and will join the
same vector files with their own capability name once a second
implementation exists.


## 10. References

 * [Token format specification](totp-token-format.md) — the tokens carried here
 * [RFC 2119] / [RFC 8174] — Key words for use in RFCs
 * [RFC 4648] — The Base16, Base32, and Base64 Data Encodings
 * [RFC 6455] — The WebSocket Protocol
 * [RFC 7617] — The 'Basic' HTTP Authentication Scheme
 * [RFC 8446] — The Transport Layer Security (TLS) Protocol Version 1.3
 * [RFC 9110] — HTTP Semantics

[RFC 2119]: https://www.rfc-editor.org/rfc/rfc2119
[RFC 4648]: https://www.rfc-editor.org/rfc/rfc4648
[RFC 6455]: https://www.rfc-editor.org/rfc/rfc6455
[RFC 7617]: https://www.rfc-editor.org/rfc/rfc7617
[RFC 8174]: https://www.rfc-editor.org/rfc/rfc8174
[RFC 8446]: https://www.rfc-editor.org/rfc/rfc8446
[RFC 9110]: https://www.rfc-editor.org/rfc/rfc9110


## Appendix A — Implementation status (informative)

| Piece | Hermod | TOTP.ts |
|-------|--------|---------|
| Token derivation | yes | yes |
| `Authorization: TOTP` — build & parse, incl. the type digit (`HTTPTOTPAuthentication`) | yes | no |
| `TOTP` request header — build & parse (`TOTPHTTPHeader`), auto-attached by the HTTP client from its `TOTPConfig` | yes | no |
| TLS exporter material (type `1` derivation) | yes | no |
| Server-side verification against the identity → config mapping (`AWebSocketServer.ClientTOTPConfig`) | prepared, in progress | no |

The server-side verification procedure of sections 4.3/5.3 is what this
document exists to pin down before that code lands.
