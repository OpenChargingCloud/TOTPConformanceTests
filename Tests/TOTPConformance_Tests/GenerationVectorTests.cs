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

#endregion

namespace cloud.charging.open.TOTP.Conformance.tests
{

    /// <summary>
    /// Runs the Hermod TOTPGenerator against every canonical generation vector
    /// in totp-test-vectors.json of the specification's annex
    /// (libs/specification/TimeBasedOneTimePasswords/test-vectors/). The
    /// TypeScript harness runs TOTP.ts against the very same file, so a
    /// divergence of the two implementations fails here instead of in the field.
    /// </summary>
    [TestFixture]
    public class GenerationVectorTests
    {

        #region (static) Vectors

        public static IEnumerable<TestCaseData> Vectors()

            => VectorFiles.Generation.Vectors.Select(vector => new TestCaseData(vector)
                                                                   .SetArgDisplayNames(vector.Id)
                                                                   .SetDescription(vector.Description));

        #endregion


        #region VectorsAreLoaded

        [Test]
        public void VectorsAreLoaded()
        {
            Assert.That(VectorFiles.Generation.Vectors, Has.Count.GreaterThanOrEqualTo(30));
        }

        #endregion

        #region KnownCapabilitiesOnly

        /// <summary>
        /// Every "requires" entry must name a capability this harness knows about,
        /// so that a new capability in the vector file cannot be skipped silently.
        /// </summary>
        [Test]
        public void KnownCapabilitiesOnly()
        {

            var unknown = VectorFiles.Generation.Vectors
                              .SelectMany(vector => vector.Requires ?? [])
                              .Where(capability => !VectorFiles.Capabilities.Contains(capability))
                              .Distinct()
                              .ToList();

            Assert.That(unknown, Is.Empty,
                        $"Unknown capabilities in the vector file: {String.Join(", ", unknown)}");

        }

        #endregion


        #region GenerateTOTPs (vector-driven)

        [TestCaseSource(nameof(Vectors))]
        public void GenerateTOTPs(GenerationVector Vector)
        {

            // Hermod provides every capability defined so far, therefore no
            // vector is skipped here (KnownCapabilitiesOnly guards the rest).

            var input          = Vector.Input;

            var hashAlgorithm  = default(TOTPHashAlgorithm?);
            if (input.HashAlgorithm is not null)
            {
                Assert.That(TOTPHashAlgorithmExtensions.TryParse(input.HashAlgorithm, out var parsed),
                            Is.True,
                            $"Could not parse hash algorithm '{input.HashAlgorithm}'!");
                hashAlgorithm = parsed;
            }

            var timestamp      = DateTimeOffset.FromUnixTimeMilliseconds(input.UnixTimestampMillis);

            var (previous,
                 current,
                 next,
                 remainingTime,
                 endTime)      = TOTPGenerator.GenerateTOTPs(

                                     input.SharedSecret,

                                     input.ValidityTimeSeconds is Double validityTime
                                         ? TimeSpan.FromSeconds(validityTime)
                                         : null,

                                     input.TotpLength is Double totpLength
                                         ? (UInt32) totpLength
                                         : null,

                                     input.Alphabet,
                                     timestamp,

                                     input.TlsExporterMaterialHex is String hex
                                         ? Convert.FromHexString(hex)
                                         : null,

                                     hashAlgorithm

                                 );

            Assert.Multiple(() => {
                Assert.That(previous,       Is.EqualTo(Vector.Expected.Previous),  "previous");
                Assert.That(current,        Is.EqualTo(Vector.Expected.Current),   "current");
                Assert.That(next,           Is.EqualTo(Vector.Expected.Next),      "next");
                Assert.That(remainingTime,  Is.EqualTo(TimeSpan.FromSeconds(Vector.Expected.RemainingTimeSeconds)));
                Assert.That(endTime,        Is.EqualTo(timestamp + remainingTime), "endTime");
            });

        }

        #endregion

    }

}
