#!/usr/bin/env python3
"""
Generate the canonical TOTP conformance test vectors.

This script is a THIRD, independent implementation of the token format
specified in spec/totp-token-format.md, written in pure Python stdlib and
deliberately sharing no code with either implementation under test:

    * Vanaheimr Hermod   (C#)          libs/Hermod/Hermod/TOTP/TOTP.cs
    * TOTP.ts            (TypeScript)  libs/TOTP.TS/src/index.ts

Before writing anything, the script re-derives every anchor vector - the
token sets that were hand-cross-validated between the two implementations
before this repository existed - and refuses to write when even one of them
does not match. The emitted JSON is therefore backed by three independent
implementations agreeing on every anchored value.

Both implementation repositories carry VENDORED COPIES of the two JSON files,
so that their own test suites run against the very same vectors without a
submodule (which would be cyclic - this repository already contains both
implementations as submodules). This script mirrors the canonical files into
the checked-out submodules, and VendoredCopiesTests in the C# harness fails
when a pinned (CI) or upstream (nightly) copy has drifted:

    libs/Hermod/HermodTests/TOTP/TestVectors/
    libs/TOTP.TS/test/vectors/

Usage:
    python tools/generate-test-vectors.py            # (re)write canonical + vendored files
    python tools/generate-test-vectors.py --check    # verify all of them are current

The JSON files are generated - do not edit them by hand.
"""

import hmac
import json
import sys
from pathlib import Path

REPO_ROOT    = Path(__file__).resolve().parent.parent
VECTORS_DIR  = REPO_ROOT / "TestVectors"

# Vendored copies inside the implementation submodules. A copy is only
# written/checked when its submodule is checked out (the CI vector job checks
# out no submodules; there the C# harness's VendoredCopiesTests is the guard).
VENDOR_DIRS  = [
    REPO_ROOT / "libs" / "Hermod"  / "HermodTests" / "TOTP" / "TestVectors",
    REPO_ROOT / "libs" / "TOTP.TS" / "test"        / "vectors",
]

SPEC_NAME    = "Open Charging Cloud TOTP Token Format 1.0 (draft)"
GENERATOR    = "tools/generate-test-vectors.py"

MASK64       = 0xFFFF_FFFF_FFFF_FFFF

# Shared defaults, normative in the specification.
DEFAULT_VALIDITY  = 30
DEFAULT_LENGTH    = 12
DEFAULT_ALPHABET  = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
DEFAULT_ALGORITHM = "sha256"

BASE32_ALPHABET   = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
BASE64URL_ALPHABET = DEFAULT_ALPHABET + "-_"

SECRET            = "secure!Charging!"


# --------------------------------------------------------------------------
# The reference implementation of the token format (spec section 4).
# --------------------------------------------------------------------------

def calc_slot_token(slot:      int,
                    secret:    bytes,
                    length:    int,
                    alphabet:  str,
                    algorithm: str,
                    material:  bytes | None = None) -> str:
    """One token for one slot number (spec section 4.3 / 6)."""

    slot_bytes = (slot & MASK64).to_bytes(8, "big")
    digest     = hmac.new(secret, slot_bytes, algorithm).digest()

    # TLS v1.3 channel binding extension: a second HMAC, keyed with the TLS
    # exporter material, over the first digest - same hash algorithm.
    if material:
        digest = hmac.new(material, digest, algorithm).digest()

    offset = digest[-1] & 0x0F

    return "".join(alphabet[digest[(offset + i) % len(digest)] % len(alphabet)]
                   for i in range(length))


def generate_totps(shared_secret: str,
                   unix_ms:       int,
                   validity:      int | None = None,
                   length:        int | None = None,
                   alphabet:      str | None = None,
                   algorithm:     str | None = None,
                   material:      bytes | None = None) -> dict:
    """Previous/current/next token plus remaining time (spec sections 4-5)."""

    validity  = validity  if validity  is not None else DEFAULT_VALIDITY
    length    = length    if length    is not None else DEFAULT_LENGTH
    alphabet  = (alphabet if alphabet  is not None else DEFAULT_ALPHABET).strip()
    algorithm = algorithm if algorithm is not None else DEFAULT_ALGORITHM

    secret    = shared_secret.strip().encode("utf-8")

    unix_s    = unix_ms // 1000
    slot      = unix_s  // validity
    remaining = validity - (unix_s % validity)

    return {
        "previous":              calc_slot_token((slot - 1) & MASK64, secret, length, alphabet, algorithm, material),
        "current":               calc_slot_token(slot,                secret, length, alphabet, algorithm, material),
        "next":                  calc_slot_token((slot + 1) & MASK64, secret, length, alphabet, algorithm, material),
        "remainingTimeSeconds":  remaining
    }


# --------------------------------------------------------------------------
# Anchors: token sets hand-cross-validated between Hermod and TOTP.ts
# (HermodTests/TOTP/TOTPGeneratorTests.cs and TOTP.TS/test/index.test.ts)
# before this repository existed. If one of these fails, this script is
# wrong - never the other way around.
# --------------------------------------------------------------------------

TS1 = 1718611200000   # 2024-06-17T08:00:00Z, an exact slot boundary for 30s
TS2 = 1716423785000   # 2024-05-23T00:23:05Z, mid-slot

ANCHORS = [
    (dict(shared_secret=SECRET, unix_ms=TS1, validity=30, length=12),
     ("QT1cCdKsIb9e", "akF3c7qY2uiu", "1U70OgaBA48M", 30)),
    (dict(shared_secret=SECRET, unix_ms=TS2),
     ("MdPU0jCm5tXz", "CN63y502maVh", "dI54vnA25m2h", 25)),
    (dict(shared_secret=SECRET, unix_ms=TS2, length=23),
     ("MdPU0jCm5tXzkaPrPj61KwI", "CN63y502maVhAsv27Sd7JlE", "dI54vnA25m2hWW3bUcdY13q", 25)),
    (dict(shared_secret=SECRET, unix_ms=TS2, alphabet="0123456789"),
     ("233045043555", "894361286613", "545817627227", 25)),
    (dict(shared_secret=SECRET, unix_ms=TS2, validity=60),
     ("nTdkiuG6yUyg", "XJZr0L1DGKn0", "ft0ONZ62MdMj", 55)),
    (dict(shared_secret=SECRET, unix_ms=TS2, algorithm="sha384"),
     ("0SbZV69lSa4W", "jAzmwLzuPuUb", "mqtkOMRrX1aS", 25)),
    (dict(shared_secret=SECRET, unix_ms=TS2, algorithm="sha512"),
     ("wjmTW4LVTdwv", "yBhTTbXO2ILd", "MHMqTXE1oVf9", 25)),
    (dict(shared_secret=SECRET, unix_ms=0),
     ("SzcwtcR5qcY7", "u5CoKdo5HUS1", "tVGiyLys7Y1V", 30)),
]

def check_anchors() -> None:

    for inputs, (previous, current, next_, remaining) in ANCHORS:
        result = generate_totps(**inputs)
        expected = {
            "previous":             previous,
            "current":              current,
            "next":                 next_,
            "remainingTimeSeconds": remaining
        }
        if result != expected:
            raise AssertionError(f"Anchor mismatch for {inputs}:\n"
                                 f"  expected {expected}\n"
                                 f"  got      {result}")

    # The 64 character sha256 ring buffer anchor (both test suites).
    ring = generate_totps(SECRET, TS1, validity=30, length=64)
    if ring["current"] != "akF3c7qY2uiuO4rpyU0SC0W8VFE6nvxz" * 2:
        raise AssertionError("Ring buffer anchor mismatch")

    # The options object API anchor of TOTP.ts (length 6, decimal alphabet).
    short = generate_totps(SECRET, TS1, validity=30, length=6, alphabet="0123456789")
    if short["current"] != "441749":
        raise AssertionError("Six digit anchor mismatch")


# --------------------------------------------------------------------------
# The generation vectors.
#
# "input" carries only the parameters that are explicitly set; an absent
# optional parameter means: the harness passes null/undefined, so that the
# implementation's own defaulting is exercised.
# --------------------------------------------------------------------------

def vector(id_:        str,
           description: str,
           *,
           secret:     str = SECRET,
           unix_ms:    int,
           validity:   int | None = None,
           length:     int | None = None,
           alphabet:   str | None = None,
           algorithm:  str | None = None,
           material_hex: str | None = None,
           requires:   list[str] | None = None) -> dict:

    input_: dict = {
        "sharedSecret":         secret,
        "unixTimestampMillis":  unix_ms
    }
    if validity     is not None: input_["validityTimeSeconds"]    = validity
    if length       is not None: input_["totpLength"]             = length
    if alphabet     is not None: input_["alphabet"]               = alphabet
    if algorithm    is not None: input_["hashAlgorithm"]          = algorithm
    if material_hex is not None: input_["tlsExporterMaterialHex"] = material_hex

    result = {
        "id":           id_,
        "description":  description,
        "input":        input_,
        "expected":     generate_totps(
                            secret, unix_ms, validity, length, alphabet, algorithm,
                            bytes.fromhex(material_hex) if material_hex else None
                        )
    }
    if requires:
        result["requires"] = requires

    return result


def generation_vectors() -> list[dict]:

    TLS_MATERIAL_32 = bytes(range(0x00, 0x20)).hex()
    TLS_MATERIAL_64 = bytes(range(0x00, 0x40)).hex()
    TLS_MATERIAL_8  = "0011223344556677"

    return [

        # -- Anchors (hand-cross-validated between Hermod and TOTP.ts) -----

        vector("defaults-slot-boundary",
               "All defaults, timestamp exactly on a 30s slot boundary: remaining time is the full validity time.",
               unix_ms=TS1, validity=30, length=12),

        vector("defaults-mid-slot",
               "All parameters defaulted (validity 30s, length 12, Base62 alphabet, HMAC-SHA256), mid-slot timestamp.",
               unix_ms=TS2),

        vector("length-23",
               "Explicit token length of 23 characters.",
               unix_ms=TS2, length=23),

        vector("alphabet-decimal",
               "Decimal alphabet (10 characters, modulo bias: 256 = 25*10 + 6).",
               unix_ms=TS2, alphabet="0123456789"),

        vector("validity-60",
               "60 second validity time.",
               unix_ms=TS2, validity=60),

        vector("sha384-defaults",
               "HMAC-SHA384 (48 hash bytes) with default parameters.",
               unix_ms=TS2, algorithm="sha384"),

        vector("sha512-defaults",
               "HMAC-SHA512 (64 hash bytes) with default parameters.",
               unix_ms=TS2, algorithm="sha512"),

        vector("epoch-previous-slot-wraps",
               "Timestamp 0: within the first slot after the Unix epoch the previous slot number wraps to 2^64 - 1 (unchecked 64 bit arithmetic).",
               unix_ms=0, validity=30, length=12),

        vector("length-64-sha256-ring-buffer",
               "A 64 character HMAC-SHA256 token: the hash is read as a ring buffer, so the 32 character token repeats verbatim.",
               unix_ms=TS1, validity=30, length=64),

        vector("length-6-decimal",
               "Six decimal digits - the classic RFC 6238 look, in this token format.",
               unix_ms=TS1, validity=30, length=6, alphabet="0123456789"),

        # -- Token length ---------------------------------------------------

        vector("length-min-4",
               "Minimum allowed token length.",
               unix_ms=TS2, length=4),

        vector("length-max-255",
               "Maximum allowed token length (with SHA-512; positions beyond 64 repeat the ring buffer).",
               unix_ms=TS2, length=255, algorithm="sha512"),

        vector("length-33-sha256",
               "One character beyond the 32 byte SHA-256 hash: position 32 repeats position 0.",
               unix_ms=TS2, length=33),

        vector("length-48-sha384",
               "Token length equal to the 48 byte SHA-384 hash length.",
               unix_ms=TS2, length=48, algorithm="sha384"),

        vector("length-64-sha512",
               "Token length equal to the 64 byte SHA-512 hash length: no repetition.",
               unix_ms=TS2, length=64, algorithm="sha512"),

        # -- Alphabets ------------------------------------------------------

        vector("alphabet-min-4",
               "Minimum allowed alphabet size (4 characters).",
               unix_ms=TS2, alphabet="ACGT"),

        vector("alphabet-hex",
               "Hexadecimal alphabet (16 characters, no modulo bias: 16 divides 256).",
               unix_ms=TS2, alphabet="0123456789abcdef"),

        vector("alphabet-base32",
               "RFC 4648 Base32 alphabet (32 characters, no modulo bias).",
               unix_ms=TS2, alphabet=BASE32_ALPHABET),

        vector("alphabet-base64url",
               "Base64url alphabet (64 characters, no modulo bias: 256 = 4*64). RECOMMENDED when bias matters.",
               unix_ms=TS2, alphabet=BASE64URL_ALPHABET),

        # -- Validity times -------------------------------------------------

        vector("validity-1",
               "One second validity time: the slot number equals the Unix time.",
               unix_ms=TS2, validity=1),

        vector("validity-5",
               "Five second validity time.",
               unix_ms=TS2, validity=5),

        vector("validity-7",
               "Seven second validity time - not a divisor of 60, exercises slot arithmetic.",
               unix_ms=TS2, validity=7),

        vector("validity-86400",
               "One day validity time.",
               unix_ms=TS2, validity=86400),

        # -- Timestamps and remaining time ----------------------------------

        vector("remaining-1-second",
               "Last second of a slot: remaining time is 1.",
               unix_ms=1716423779000),

        vector("milliseconds-floor",
               "Milliseconds are floored to whole seconds: x999 ms yields the same tokens as x000 ms.",
               unix_ms=1716423785999),

        vector("milliseconds-floor-slot-edge",
               "999 ms before a slot change: still the old slot, remaining time 1 second.",
               unix_ms=1716423809999),

        vector("timestamp-y2038",
               "Unix time 2^31 - 1 (2038-01-19T03:14:07Z): no 32 bit rollover.",
               unix_ms=2147483647000),

        # 23:59:28, not 23:59:59: in the very last slot of the calendar
        # Hermod's EndTime (timestamp + remaining time) would leave the
        # DateTime range - a documented deviation, see docs/spec-deviations.md.
        vector("timestamp-year-9999",
               "9999-12-31T23:59:28Z, the far end of the calendar.",
               unix_ms=253402300768000),

        # -- Shared secrets -------------------------------------------------

        vector("secret-utf8",
               "Non-ASCII shared secret: the secret is encoded as UTF-8 before keying the HMAC. No Unicode normalization is applied.",
               secret="sécürè!Chärgîng!", unix_ms=TS2),

        vector("secret-64-chars",
               "A long, 64 character shared secret.",
               secret="0ZPatRVe1DTLBHRipD5cyOU9d1TCsdLLYhtLXGajUATuOwaVSVFPnbUAJyTFrPFI", unix_ms=TS2),

        vector("secret-surrounding-whitespace",
               "The shared secret is trimmed before use: surrounding whitespace yields the tokens of the trimmed secret.",
               secret="  " + SECRET + "  ", unix_ms=TS2),

        vector("alphabet-surrounding-whitespace",
               "The alphabet is trimmed before use: surrounding whitespace yields the tokens of the trimmed alphabet.",
               unix_ms=TS2, alphabet=" 0123456789 "),

        # -- OCPP 2.1 profile ----------------------------------------------
        #
        # OCPP 2.1 Ed.2, use case C25 "Ad hoc payment via a QR code" defines
        # "TOTP algorithm, version 1" = this format with HMAC-SHA256 and the
        # Base62 default alphabet. These vectors pin OCPP-representable
        # parameter sets (see docs/ocpp-totp-comparison.md).

        vector("ocpp-v1-length-8",
               "OCPP 2.1 C25 'TOTP algorithm, version 1' with the example token length 8 of WebPaymentsCtrlr.Length.",
               unix_ms=TS2, validity=30, length=8),

        vector("ocpp-v1-min-profile",
               "The weakest OCPP-legal corner: WebPaymentsCtrlr minimum Length 6 with maximum ValidityTime 3600s.",
               unix_ms=TS2, validity=3600, length=6),

        # -- Everything at once --------------------------------------------

        vector("all-parameters-custom",
               "Every parameter away from its default at once.",
               secret="Conformance!Secret#2026", unix_ms=TS2,
               validity=120, length=20, alphabet=BASE32_ALPHABET, algorithm="sha384"),

        # -- TLS v1.3 channel binding (optional extension, spec section 6) --

        vector("tls-binding-sha256",
               "TLS v1.3 channel binding: a second HMAC, keyed with 32 bytes of TLS exporter material, over the first digest.",
               unix_ms=TS2, material_hex=TLS_MATERIAL_32,
               requires=["tlsChannelBinding"]),

        vector("tls-binding-sha512",
               "TLS v1.3 channel binding with HMAC-SHA512: both HMACs use the same hash algorithm.",
               unix_ms=TS2, algorithm="sha512", material_hex=TLS_MATERIAL_64,
               requires=["tlsChannelBinding"]),

        vector("tls-binding-short-material",
               "TLS channel binding with only 8 bytes of exporter material (HMAC accepts any key length).",
               unix_ms=TS2, material_hex=TLS_MATERIAL_8,
               requires=["tlsChannelBinding"]),

    ]


# --------------------------------------------------------------------------
# The invalid input vectors.
#
# Implementations MUST reject these inputs (spec section 5.1). Both existing
# implementations share their error message texts verbatim; harnesses assert
# that the thrown message STARTS WITH "expectedError" (the C# side appends
# the parameter name).
#
# "knownDeviations" lists implementations that are known NOT to perform the
# check yet - see docs/spec-deviations.md. Harnesses for those skip the
# vector and characterize the actual behaviour separately.
#
# "notApplicable" lists implementations whose typed API cannot represent the
# input at all (which is a rejection by construction, not a deviation).
# --------------------------------------------------------------------------

def invalid(id_:         str,
            description: str,
            input_:      dict,
            error:       str,
            *,
            known_deviations: list[str] | None = None,
            not_applicable:   list[str] | None = None) -> dict:

    result = {
        "id":            id_,
        "description":   description,
        "input":         {"unixTimestampMillis": TS2, **input_},
        "expectedError": error
    }
    if known_deviations:
        result["knownDeviations"] = known_deviations
    if not_applicable:
        result["notApplicable"] = not_applicable

    return result


ERR_SECRET_EMPTY      = "The given shared secret must not be null or empty!"
ERR_SECRET_WHITESPACE = "The given shared secret must not contain any whitespace characters!"
ERR_SECRET_SHORT      = "The length of the given shared secret must be at least 16 characters!"
ERR_LENGTH            = "The expected length of the TOTP must be between 4 and 255 characters!"
ERR_VALIDITY          = "The validity time must be a positive integer number of seconds!"
ERR_TIMESTAMP         = "The timestamp must be a non-negative Unix timestamp in milliseconds!"
ERR_HASH              = "The hash algorithm must be one of: sha256, sha384, sha512!"
ERR_ALPHABET_EMPTY    = "The given alphabet must not be null or empty!"
ERR_ALPHABET_SIZE     = "The given alphabet must contain at least 4 characters!"
ERR_ALPHABET_DUP      = "The given alphabet must not contain duplicate characters!"
ERR_ALPHABET_WS       = "The given alphabet must not contain any whitespace characters!"


def invalid_vectors() -> list[dict]:

    return [

        # -- Shared secret --------------------------------------------------

        invalid("secret-empty",
                "An empty shared secret is rejected.",
                {"sharedSecret": ""},
                ERR_SECRET_EMPTY),

        invalid("secret-whitespace-only",
                "A whitespace-only shared secret trims to empty and is rejected.",
                {"sharedSecret": "        "},
                ERR_SECRET_EMPTY),

        invalid("secret-too-short",
                "A 15 character shared secret is rejected (minimum is 16).",
                {"sharedSecret": "secure!Charging"},
                ERR_SECRET_SHORT),

        invalid("secret-inner-whitespace",
                "A shared secret with interior whitespace is rejected (checked before the length).",
                {"sharedSecret": "secure Charging!"},
                ERR_SECRET_WHITESPACE),

        # -- Token length ---------------------------------------------------

        invalid("length-3",
                "A token length below 4 is rejected.",
                {"sharedSecret": SECRET, "totpLength": 3},
                ERR_LENGTH),

        invalid("length-256",
                "A token length above 255 is rejected.",
                {"sharedSecret": SECRET, "totpLength": 256},
                ERR_LENGTH),

        invalid("length-fractional",
                "A non-integer token length is rejected. (Unrepresentable in the C# API: the parameter is a UInt32.)",
                {"sharedSecret": SECRET, "totpLength": 12.5},
                ERR_LENGTH,
                not_applicable=["hermod"]),

        # -- Validity time --------------------------------------------------

        invalid("validity-zero",
                "A validity time of zero seconds is rejected.",
                {"sharedSecret": SECRET, "validityTimeSeconds": 0},
                ERR_VALIDITY),

        invalid("validity-negative",
                "A negative validity time is rejected.",
                {"sharedSecret": SECRET, "validityTimeSeconds": -30},
                ERR_VALIDITY),

        invalid("validity-fractional",
                "A non-integer validity time is rejected.",
                {"sharedSecret": SECRET, "validityTimeSeconds": 4.5},
                ERR_VALIDITY),

        # -- Timestamp ------------------------------------------------------

        invalid("timestamp-negative",
                "A timestamp before the Unix epoch is rejected.",
                {"sharedSecret": SECRET, "unixTimestampMillis": -1000},
                ERR_TIMESTAMP),

        # -- Hash algorithm -------------------------------------------------

        invalid("hash-sha1",
                "SHA-1 is not part of the format.",
                {"sharedSecret": SECRET, "hashAlgorithm": "sha1"},
                ERR_HASH),

        invalid("hash-md5",
                "MD5 is not part of the format.",
                {"sharedSecret": SECRET, "hashAlgorithm": "md5"},
                ERR_HASH),

        # -- Alphabet -------------------------------------------------------

        invalid("alphabet-empty",
                "An explicitly empty alphabet is rejected (it does not fall back to the default).",
                {"sharedSecret": SECRET, "alphabet": ""},
                ERR_ALPHABET_EMPTY),

        invalid("alphabet-whitespace-only",
                "A whitespace-only alphabet trims to empty and is rejected.",
                {"sharedSecret": SECRET, "alphabet": "   "},
                ERR_ALPHABET_EMPTY),

        invalid("alphabet-3-chars",
                "An alphabet with fewer than 4 characters is rejected.",
                {"sharedSecret": SECRET, "alphabet": "abc"},
                ERR_ALPHABET_SIZE),

        invalid("alphabet-duplicates",
                "An alphabet with duplicate characters is rejected.",
                {"sharedSecret": SECRET, "alphabet": "abcdeff"},
                ERR_ALPHABET_DUP),

        invalid("alphabet-inner-whitespace",
                "An alphabet with interior whitespace is rejected (duplicates are checked first).",
                {"sharedSecret": SECRET, "alphabet": "ab cdef"},
                ERR_ALPHABET_WS),

    ]


# --------------------------------------------------------------------------
# File emission.
# --------------------------------------------------------------------------

def render(payload: dict) -> str:
    return json.dumps(payload, indent=2, ensure_ascii=True) + "\n"


def build_files() -> dict[Path, str]:

    header = {
        "$comment":       f"GENERATED by {GENERATOR} - do not edit by hand. "
                          f"Regenerate with: python {GENERATOR}",
        "specification":  SPEC_NAME,
    }

    canonical = {
        VECTORS_DIR / "totp-test-vectors.json": render({
            **header,
            "description": "Canonical TOTP generation vectors. Absent optional input "
                           "parameters mean: pass null/undefined, so that the "
                           "implementation's own defaulting is exercised. Vectors with "
                           "a 'requires' list only apply to implementations providing "
                           "all listed capabilities.",
            "vectors": generation_vectors()
        }),
        VECTORS_DIR / "totp-invalid-inputs.json": render({
            **header,
            "description": "Inputs that implementations MUST reject, with the shared "
                           "error message text. Harnesses assert that the thrown "
                           "message starts with 'expectedError'. See the file's "
                           "'knownDeviations'/'notApplicable' semantics in "
                           "TestVectors/README.md.",
            "vectors": invalid_vectors()
        }),
    }

    files = dict(canonical)
    for vendor_dir in VENDOR_DIRS:
        if vendor_dir.parent.is_dir():
            for path, content in canonical.items():
                files[vendor_dir / path.name] = content

    return files


def main() -> int:

    check_anchors()

    files = build_files()
    check  = "--check" in sys.argv[1:]

    stale = []
    for path, content in files.items():
        # The vendored copies live in repositories without an eol=lf
        # .gitattributes rule, so autocrlf checkouts materialize them with
        # CRLF - that is not drift. (The canonical files are LF-pinned.)
        current = path.read_text(encoding="utf-8").replace("\r\n", "\n") if path.exists() else None
        if current != content:
            stale.append(path)
            if not check:
                path.parent.mkdir(parents=True, exist_ok=True)
                with open(path, "w", encoding="utf-8", newline="\n") as f:
                    f.write(content)

    if check:
        if stale:
            for path in stale:
                print(f"STALE: {path.relative_to(REPO_ROOT)}")
            print(f"Regenerate with: python {GENERATOR}")
            return 1
        print(f"All anchors hold and {len(files)} vector files are current.")
        return 0

    for path in files:
        marker = "updated" if path in stale else "unchanged"
        print(f"{marker}: {path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
