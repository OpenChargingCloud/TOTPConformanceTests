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
    /// Runs the Hermod TOTPGenerator against every invalid input vector in
    /// totp-invalid-inputs.json of the specification's annex: each input MUST
    /// be rejected with the shared error message (the C# side appends the
    /// parameter name, so only the start of the message is asserted).
    ///
    /// Vectors marked "knownDeviations": ["hermod"] are skipped here and
    /// characterized in KnownDeviationTests instead - see docs/spec-deviations.md.
    /// Vectors marked "notApplicable": ["hermod"] cannot be represented in the
    /// typed C# API at all, which is a rejection by construction.
    /// </summary>
    [TestFixture]
    public class InvalidInputVectorTests
    {

        #region (static) Vectors

        public static IEnumerable<TestCaseData> Vectors()

            => VectorFiles.InvalidInputs.Vectors.Select(vector => new TestCaseData(vector)
                                                                      .SetArgDisplayNames(vector.Id)
                                                                      .SetDescription(vector.Description));

        #endregion


        #region VectorsAreLoaded

        [Test]
        public void VectorsAreLoaded()
        {
            Assert.That(VectorFiles.InvalidInputs.Vectors, Has.Count.GreaterThanOrEqualTo(15));
        }

        #endregion


        #region RejectsInvalidInput (vector-driven)

        [TestCaseSource(nameof(Vectors))]
        public void RejectsInvalidInput(InvalidInputVector Vector)
        {

            if (Vector.NotApplicable?.Contains(VectorFiles.ImplementationName) == true)
                Assert.Ignore("Not representable in the typed C# API - rejected by construction.");

            if (Vector.KnownDeviations?.Contains(VectorFiles.ImplementationName) == true)
                Assert.Ignore($"Documented deviation - see docs/spec-deviations.md#{Vector.Id} " +
                               "and KnownDeviationTests.");

            var input = Vector.Input;

            // Hermod takes the hash algorithm as an enum: an unknown algorithm
            // name is rejected at its parsing boundary, which is this
            // implementation's conformant rejection point for these vectors.
            if (input.HashAlgorithm is not null &&
               !TOTPHashAlgorithmExtensions.TryParse(input.HashAlgorithm, out _))
            {
                Assert.Pass($"Rejected at the parsing boundary: TOTPHashAlgorithmExtensions.TryParse(\"{input.HashAlgorithm}\") returned false.");
            }

            var exception = Assert.Catch<Exception>(() => {

                TOTPGenerator.GenerateTOTPs(

                    input.SharedSecret,

                    input.ValidityTimeSeconds is Double validityTime
                        ? TimeSpan.FromSeconds(validityTime)
                        : null,

                    input.TotpLength is Double totpLength
                        ? (UInt32) totpLength
                        : null,

                    input.Alphabet,
                    DateTimeOffset.FromUnixTimeMilliseconds(input.UnixTimestampMillis)

                );

            });

            Assert.That(exception?.Message,
                        Does.StartWith(Vector.ExpectedError),
                        $"Expected the shared error message for vector '{Vector.Id}'!");

        }

        #endregion

    }

}
