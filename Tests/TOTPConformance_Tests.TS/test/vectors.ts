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

import { readFileSync } from "node:fs";

/**
 * The name this harness (and its implementation under test) is known by
 * within the vector files ("knownDeviations"/"notApplicable").
 */
export const IMPLEMENTATION_NAME = "totp.ts";

/**
 * The optional capabilities TOTP.ts provides ("requires" lists of vectors).
 * TLS v1.3 channel binding is a Hermod-only extension so far.
 */
export const CAPABILITIES: ReadonlySet<string> = new Set([]);

/**
 * Every capability name that exists in the vector format at all. A vector
 * requiring anything outside this list means the vector file is newer than
 * this harness - fail, never skip silently.
 */
export const KNOWN_CAPABILITIES: ReadonlySet<string> = new Set([
    "tlsChannelBinding",
    "httpAuthentication"
]);


export interface VectorInput {
    sharedSecret:            string;
    unixTimestampMillis:     number;
    validityTimeSeconds?:    number;
    totpLength?:             number;
    alphabet?:               string;
    hashAlgorithm?:          string;
    tlsExporterMaterialHex?: string;
}

export interface GenerationVector {
    id:           string;
    description:  string;
    input:        VectorInput;
    expected: {
        previous:              string;
        current:               string;
        next:                  string;
        remainingTimeSeconds:  number;
        endTimeUnixSeconds:    number;
    };
    requires?:    string[];
}

export interface InvalidInputVector {
    id:                string;
    description:       string;
    input:             VectorInput;
    expectedError:     string;
    knownDeviations?:  string[];
    notApplicable?:    string[];
}

interface VectorFile<T> {
    specification:  string;
    description:    string;
    requires?:      string[];
    vectors:        T[];
}


function load<T>(fileName: string): VectorFile<T> {
    return JSON.parse(
        readFileSync(new URL(`../../../libs/specification/TimeBasedOneTimePasswords/test-vectors/${fileName}`, import.meta.url), "utf-8")
    ) as VectorFile<T>;
}

export const generationVectors    = load<GenerationVector>  ("totp-test-vectors.json").vectors;
export const invalidInputVectors  = load<InvalidInputVector>("totp-invalid-inputs.json").vectors;

/**
 * The HTTP authentication vectors (totp-http-authentication.md of the
 * specification) are capability-gated as a whole file: TOTP.ts implements no
 * HTTP binding, so this harness only checks the gate and skips the file
 * DECLARATIVELY.
 */
export const httpAuthFileRequires = load<unknown>("totp-http-auth-vectors.json").requires ?? [];
