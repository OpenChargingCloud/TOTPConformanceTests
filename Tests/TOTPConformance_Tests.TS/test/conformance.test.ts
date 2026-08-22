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

import { generateTOTPs, type TOTPHashAlgorithm } from "../../../libs/TOTP.TS/src/index";

import {
    CAPABILITIES,
    KNOWN_CAPABILITIES,
    IMPLEMENTATION_NAME,
    generationVectors,
    httpAuthFileRequires,
    invalidInputVectors,
    type VectorInput
} from "./vectors";


function run(input: VectorInput) {
    return generateTOTPs({
        sharedSecret:   input.sharedSecret,
        validityTime:   input.validityTimeSeconds ?? null,
        totpLength:     input.totpLength          ?? null,
        alphabet:       input.alphabet            ?? null,
        timestamp:      input.unixTimestampMillis,
        hashAlgorithm: (input.hashAlgorithm       ?? null) as TOTPHashAlgorithm | null
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
                          ...httpAuthFileRequires ]
            .filter(capability => !KNOWN_CAPABILITIES.has(capability));
        expect(unknown).toEqual([]);
    });

    // The HTTP authentication vectors are gated as a whole file. TOTP.ts has
    // no HTTP binding, so the skip is declared here - visible, never silent.
    const missingHttp = httpAuthFileRequires.filter(capability => !CAPABILITIES.has(capability));

    if (missingHttp.length > 0)
        it.skip(`totp-http-auth-vectors.json (requires: ${missingHttp.join(", ")})`, () => {});

});


describe("Generation vectors", () => {

    // TOTP.ts runs TestVectors/totp-test-vectors.json - the same file the C#
    // harness runs Hermod against, so a divergence of the two implementations
    // fails here instead of in the field.
    for (const vector of generationVectors) {

        const missing = (vector.requires ?? []).filter(capability => !CAPABILITIES.has(capability));

        if (missing.length > 0) {
            it.skip(`${vector.id} (requires: ${missing.join(", ")})`, () => {});
            continue;
        }

        it(vector.id, () => {
            expect(run(vector.input)).toEqual({
                previous:       vector.expected.previous,
                current:        vector.expected.current,
                next:           vector.expected.next,
                remainingTime:  vector.expected.remainingTimeSeconds
            });
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
