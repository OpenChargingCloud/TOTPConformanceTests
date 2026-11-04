/*
 * Copyright (c) 2024-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of TOTPConformanceTests <https://github.com/OpenChargingCloud/TOTPConformanceTests>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

import { describe, expect, it } from "vitest";

import { formatTOTPAuthorization,
         generateTOTPs,
         parseTOTPAuthorization,
         type TOTPHashAlgorithm } from "../../../libs/TOTP.TS/src/index";

import {
    CAPABILITIES,
    KNOWN_CAPABILITIES,
    IMPLEMENTATION_NAME,
    generationVectors,
    httpAuthFile,
    httpAuthFileRequires,
    invalidInputVectors,
    verificationFile,
    verificationFileRequires,
    type VectorInput
} from "./vectors";


function bytesOfHex(hex: string | undefined): Uint8Array | null {
    return hex === undefined
               ? null
               : Uint8Array.from(hex.match(/../g) ?? [], byte => parseInt(byte, 16));
}

function run(input: VectorInput) {
    return generateTOTPs({
        sharedSecret:         input.sharedSecret,
        validityTime:         input.validityTimeSeconds ?? null,
        totpLength:           input.totpLength          ?? null,
        alphabet:             input.alphabet            ?? null,
        timestamp:            input.unixTimestampMillis,
        hashAlgorithm:       (input.hashAlgorithm       ?? null) as TOTPHashAlgorithm | null,
        tlsExporterMaterial:  bytesOfHex(input.tlsExporterMaterialHex)
    });
}


describe("Vector files", () => {

    it("are loaded", () => {
        expect(generationVectors.length).toBeGreaterThanOrEqual(30);
        expect(invalidInputVectors.length).toBeGreaterThanOrEqual(15);
    });

    it("require only capabilities this harness knows about", () => {
        // A new capability in the vector files must fail here, so that it
        // cannot be skipped silently.
        const unknown = [ ...generationVectors.flatMap(vector => vector.requires ?? []),
                          ...httpAuthFileRequires,
                          ...verificationFileRequires ]
            .filter(capability => !KNOWN_CAPABILITIES.has(capability));
        expect(unknown).toEqual([]);
    });

});


// The HTTP authentication vectors are gated as a whole file: should a
// capability they require ever be missing, the skip is declared here -
// visible, never silent.
describe.skipIf(httpAuthFileRequires.some(capability => !CAPABILITIES.has(capability)))("HTTP authentication vectors", () => {

    it("are loaded", () => {
        expect(httpAuthFile.vectors.length).       toBeGreaterThanOrEqual(5);
        expect(httpAuthFile.parseVectors.length).  toBeGreaterThanOrEqual(9);
        expect(httpAuthFile.invalidHeaders.length).toBeGreaterThanOrEqual(17);
    });

    // Round trip: the canonical header out, and the same credentials back in.
    for (const vector of httpAuthFile.vectors)
        it(`round-trip: ${vector.id}`, () => {
            expect(formatTOTPAuthorization(vector.input)).toBe(vector.expected.header);
            expect(parseTOTPAuthorization(vector.expected.header)).toEqual(vector.input);
        });

    for (const vector of httpAuthFile.parseVectors)
        it(`parse: ${vector.id}`, () => {
            expect(parseTOTPAuthorization(vector.header)).toEqual(vector.expected);
        });

    for (const vector of httpAuthFile.invalidHeaders)
        it(`reject: ${vector.id}`, () => {
            expect(() => parseTOTPAuthorization(vector.header)).toThrow();
        });

});


// The server verification vectors need a verifier, and TOTP.ts is none:
// it builds and parses credentials, the server holding the login's
// configuration verifies them. Every vector is skipped by name - visible,
// never silent - and claiming the capability without writing a runner
// here fails.
describe("HTTP verification vectors", () => {

    const missing = verificationFileRequires.filter(capability => !CAPABILITIES.has(capability));

    it("are loaded", () => {
        expect(verificationFile.vectors.length).toBeGreaterThanOrEqual(20);
    });

    it("are skipped only for a capability this harness lacks", () => {
        expect(missing).not.toEqual([]);
    });

    for (const vector of verificationFile.vectors)
        it.skip(`${vector.id} (requires: ${missing.join(", ")})`, () => {});

});


describe("Generation vectors", () => {

    // TOTP.ts runs totp-test-vectors.json of the specification's annex - the
    // same file the C# harness runs Hermod against, so a divergence of the
    // two implementations fails here instead of in the field.
    for (const vector of generationVectors) {

        const missing = (vector.requires ?? []).filter(capability => !CAPABILITIES.has(capability));

        if (missing.length > 0) {
            it.skip(`${vector.id} (requires: ${missing.join(", ")})`, () => {});
            continue;
        }

        it(vector.id, () => {

            const result = run(vector.input);

            expect(result).toEqual({
                previous:       vector.expected.previous,
                current:        vector.expected.current,
                next:           vector.expected.next,
                remainingTime:  vector.expected.remainingTimeSeconds
            });

            // The window ends at a fixed instant, (S + 1) * V (spec section
            // 4.1). TOTP.ts reports whole seconds from the floored timestamp,
            // so floor + remaining must land exactly on it.
            expect(Math.floor(vector.input.unixTimestampMillis / 1000) + result.remainingTime)
                .toBe(vector.expected.endTimeUnixSeconds);

        });

    }

});


describe("Invalid input vectors", () => {

    for (const vector of invalidInputVectors) {

        if (vector.notApplicable?.includes(IMPLEMENTATION_NAME)) {
            it.skip(`${vector.id} (not representable in this API)`, () => {});
            continue;
        }

        if (vector.knownDeviations?.includes(IMPLEMENTATION_NAME)) {
            it.skip(`${vector.id} (documented deviation, see docs/spec-deviations.md)`, () => {});
            continue;
        }

        it(vector.id, () => {
            expect(() => run(vector.input)).toThrow(vector.expectedError);
        });

    }

});


describe("Format properties", () => {

    // Structural properties that hold for ALL inputs, beyond the pinned
    // example values of the vector files. The C# harness asserts the same
    // properties against Hermod.

    const sharedSecret = "secure!Charging!";
    const timestamp    = 1716423785000;

    it("defaults to HMAC-SHA256", () => {
        expect(generateTOTPs(sharedSecret, null, null, null, timestamp).current)
            .toBe(generateTOTPs(sharedSecret, null, null, null, timestamp, "sha256").current);
    });

    it("overlaps previous/current/next across adjacent slots", () => {
        const next0     = generateTOTPs(sharedSecret, null, null, null, timestamp).next;
        const current1  = generateTOTPs(sharedSecret, null, null, null, timestamp + 30_000).current;
        const previous2 = generateTOTPs(sharedSecret, null, null, null, timestamp + 60_000).previous;
        expect(current1).toBe(next0);
        expect(previous2).toBe(next0);
    });

    it("reads the hash as a ring buffer: position i + hashLength repeats position i", () => {
        const totp = generateTOTPs(sharedSecret, 30, 96, null, timestamp).current;
        expect(totp.slice(32, 64)).toBe(totp.slice(0, 32));
        expect(totp.slice(64, 96)).toBe(totp.slice(0, 32));
    });

});
