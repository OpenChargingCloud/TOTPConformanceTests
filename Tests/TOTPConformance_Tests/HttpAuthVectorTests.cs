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

#region Usings

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace cloud.charging.open.TOTP.Conformance.tests
{

    /// <summary>
    /// Runs Hermod's HTTPTOTPAuthentication against the canonical HTTP
    /// authentication vectors in TestVectors/totp-http-auth-vectors.json -
    /// the "Authorization: TOTP" scheme of spec/totp-http-authentication.md:
    /// "TOTP" SP type digit SP base64(identity):base64(totp). Build vectors
    /// round-trip in both directions, parse vectors pin leniency (scheme
    /// case-insensitivity), invalid headers MUST be rejected.
    /// </summary>
    [TestFixture]
    public class HttpAuthVectorTests
    {

        #region (static) Vector sources

        private static TOTPHTTPHeaderType AsType(Int32 Type)

            => Type switch {
                   0  => TOTPHTTPHeaderType.RAW,
                   1  => TOTPHTTPHeaderType.TLSChannelBinding,
                   _  => throw new ArgumentException($"Unknown TOTP type '{Type}' in the vector file!")
               };

        public static IEnumerable<TestCaseData> BuildVectors()
            => VectorFiles.HttpAuth.Vectors       .Select(vector => new TestCaseData(vector).SetArgDisplayNames(vector.Id));

        public static IEnumerable<TestCaseData> ParseVectors()
            => VectorFiles.HttpAuth.ParseVectors  .Select(vector => new TestCaseData(vector).SetArgDisplayNames(vector.Id));

        public static IEnumerable<TestCaseData> InvalidHeaders()
            => VectorFiles.HttpAuth.InvalidHeaders.Select(vector => new TestCaseData(vector).SetArgDisplayNames(vector.Id));

        #endregion


        #region FileIsLoadedAndApplicable

        /// <summary>
        /// The whole file requires capabilities; this harness must provide
        /// every one of them (Hermod implements the HTTP bindings), and any
        /// capability it does not know about must fail, never skip silently.
        /// </summary>
        [Test]
        public void FileIsLoadedAndApplicable()
        {

            Assert.Multiple(() => {

                Assert.That(VectorFiles.HttpAuth.Requires,
                            Is.SubsetOf(VectorFiles.Capabilities),
                            "This harness lacks a capability the vector file requires!");

                Assert.That(VectorFiles.HttpAuth.Vectors,        Has.Count.GreaterThanOrEqualTo(4));
                Assert.That(VectorFiles.HttpAuth.ParseVectors,   Has.Count.GreaterThanOrEqualTo(1));
                Assert.That(VectorFiles.HttpAuth.InvalidHeaders, Has.Count.GreaterThanOrEqualTo(8));

            });

        }

        #endregion


        #region BuildsAndRoundTripsHeader (vector-driven)

        [TestCaseSource(nameof(BuildVectors))]
        public void BuildsAndRoundTripsHeader(HttpAuthVector Vector)
        {

            var auth = HTTPTOTPAuthentication.Create(
                           Vector.Input.Username,
                           Vector.Input.Totp,
                           AsType(Vector.Input.Type)
                       );

            Assert.That(auth.HTTPText, Is.EqualTo(Vector.Expected.Header), "build");

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader(Vector.Expected.Header, out var parsed),
                        Is.True,
                        "The built header must parse again!");

            Assert.Multiple(() => {
                Assert.That(parsed?.Username,  Is.EqualTo(Vector.Input.Username));
                Assert.That(parsed?.TOTP,      Is.EqualTo(Vector.Input.Totp));
                Assert.That(parsed?.Type,      Is.EqualTo(AsType(Vector.Input.Type)));
            });

        }

        #endregion

        #region ParsesLenientForm (vector-driven)

        [TestCaseSource(nameof(ParseVectors))]
        public void ParsesLenientForm(HttpAuthParseVector Vector)
        {

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader(Vector.Header, out var parsed),
                        Is.True,
                        $"'{Vector.Header}' must parse!");

            Assert.Multiple(() => {
                Assert.That(parsed?.Username,  Is.EqualTo(Vector.Expected.Username));
                Assert.That(parsed?.TOTP,      Is.EqualTo(Vector.Expected.Totp));
                Assert.That(parsed?.Type,      Is.EqualTo(AsType(Vector.Expected.Type)));
            });

        }

        #endregion

        #region RejectsInvalidHeader (vector-driven)

        [TestCaseSource(nameof(InvalidHeaders))]
        public void RejectsInvalidHeader(InvalidHeaderVector Vector)
        {

            Assert.That(HTTPTOTPAuthentication.TryParseHTTPHeader(Vector.Header, out _),
                        Is.False,
                        $"'{Vector.Header}' must be rejected!");

        }

        #endregion

    }

}
