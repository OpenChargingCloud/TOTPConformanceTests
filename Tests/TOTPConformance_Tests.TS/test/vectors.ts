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
 * The optional capabilities TOTP.ts provides ("requires" lists of vectors):
 * TLS v1.3 channel binding (the tlsExporterMaterial option) and the
 * "Authorization: TOTP" scheme (formatTOTPAuthorization/parseTOTPAuthorization).
 */
export const CAPABILITIES: ReadonlySet<string> = new Set([
    "tlsChannelBinding",
    "httpAuthentication"
]);

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

export interface HttpAuthCredentials {
    login:  string;
    totp:   string;
    tlscb:  boolean;
}

export interface HttpAuthRoundTripVector {
    id:           string;
    description:  string;
    input:        HttpAuthCredentials;
    expected:     { header: string };
}

export interface HttpAuthParseVector {
    id:           string;
    description:  string;
    header:       string;
    expected:     HttpAuthCredentials;
}

export interface HttpAuthInvalidHeader {
    id:           string;
    description:  string;
    header:       string;
}

interface VectorFile<T> {
    specification:  string;
    description:    string;
    requires?:      string[];
    vectors:        T[];
}

interface HttpAuthVectorFile extends VectorFile<HttpAuthRoundTripVector> {
    parseVectors:    HttpAuthParseVector[];
    invalidHeaders:  HttpAuthInvalidHeader[];
}


function load(fileName: string): unknown {
    return JSON.parse(
        readFileSync(new URL(`../../../libs/specification/TimeBasedOneTimePasswords/test-vectors/${fileName}`, import.meta.url), "utf-8")
    );
}

export const generationVectors    = (load("totp-test-vectors.json")   as VectorFile<GenerationVector>).vectors;
export const invalidInputVectors  = (load("totp-invalid-inputs.json") as VectorFile<InvalidInputVector>).vectors;

/**
 * The HTTP authentication vectors (totp-http-authentication.md of the
 * specification) are capability-gated as a whole file, not per vector.
 */
export const httpAuthFile          = load("totp-http-auth-vectors.json") as HttpAuthVectorFile;
export const httpAuthFileRequires  = httpAuthFile.requires ?? [];
