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
    /// Structural properties of the frozen token format that hold for ALL
    /// inputs, beyond the pinned example values of the vector files.
    /// The TypeScript harness asserts the same properties against TOTP.ts.
    /// </summary>
    [TestFixture]
    public class FormatPropertyTests
    {

        private const           String          sharedSecret  = "secure!Charging!";
        private static readonly DateTimeOffset  timestamp     = DateTimeOffset.FromUnixTimeMilliseconds(1716423785000);


        #region DefaultHashAlgorithm_IsHMACSHA256

        [Test]
        public void DefaultHashAlgorithm_IsHMACSHA256()
        {

            var (_, byDefault,  _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp);
            var (_, explicitly, _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp, null, TOTPHashAlgorithm.SHA256);

            Assert.That(byDefault, Is.EqualTo(explicitly));

        }

        #endregion

        #region SingularAndPluralAPI_Agree

        [Test]
        public void SingularAndPluralAPI_Agree()
        {

            var (current1, remainingTime1, endTime1)    = TOTPGenerator.GenerateTOTP (sharedSecret, null, null, null, timestamp);
            var (_, current2, _, remainingTime2, endTime2) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp);

            Assert.Multiple(() => {
                Assert.That(current1,        Is.EqualTo(current2));
                Assert.That(remainingTime1,  Is.EqualTo(remainingTime2));
                Assert.That(endTime1,        Is.EqualTo(endTime2));
            });

        }

        #endregion

        #region EmptyTLSExporterMaterial_EqualsNoMaterial

        /// <summary>
        /// An empty exporter material array means "no channel binding", not
        /// "an HMAC with an empty key".
        /// </summary>
        [Test]
        public void EmptyTLSExporterMaterial_EqualsNoMaterial()
        {

            var (_, withoutMaterial, _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp, null);
            var (_, withEmpty,       _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp, []);

            Assert.That(withEmpty, Is.EqualTo(withoutMaterial));

        }

        #endregion

        #region ChannelBinding_ChangesTheToken

        [Test]
        public void ChannelBinding_ChangesTheToken()
        {

            var material1 = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
            var material2 = Convert.FromHexString("ff0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1eff");

            var (_, unbound, _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp);
            var (_, bound1,  _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp, material1);
            var (_, bound2,  _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp, material2);

            Assert.Multiple(() => {
                Assert.That(bound1, Is.Not.EqualTo(unbound), "Binding must change the token.");
                Assert.That(bound2, Is.Not.EqualTo(bound1),  "Different TLS sessions must yield different tokens.");
            });

        }

        #endregion

        #region SlotChain_PreviousAndNextOverlapAcrossSlots

        /// <summary>
        /// The acceptance window works because "next" of slot n equals
        /// "current" of slot n+1 and "previous" of slot n+2.
        /// </summary>
        [Test]
        public void SlotChain_PreviousAndNextOverlapAcrossSlots()
        {

            var (_, _, next0, _, _)          = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp);
            var (_, current1, _, _, _)       = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp + TimeSpan.FromSeconds(30));
            var (previous2, _, _, _, _)      = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp + TimeSpan.FromSeconds(60));

            Assert.Multiple(() => {
                Assert.That(current1,   Is.EqualTo(next0));
                Assert.That(previous2,  Is.EqualTo(next0));
            });

        }

        #endregion

        #region UnknownHashAlgorithmEnumValue_Throws

        [Test]
        public void UnknownHashAlgorithmEnumValue_Throws()
        {

            var exception = Assert.Throws<ArgumentException>(
                                () => TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp, null, (TOTPHashAlgorithm) 42)
                            );

            Assert.That(exception?.Message, Does.StartWith("The hash algorithm must be one of: sha256, sha384, sha512!"));

        }

        #endregion

        #region TLSExporterLabel_IsFrozen

        /// <summary>
        /// The TLS exporter label is part of the wire contract of the channel
        /// binding extension (spec section 6).
        /// </summary>
        [Test]
        public void TLSExporterLabel_IsFrozen()
        {
            Assert.That(TOTPGenerator.TLSExporterLabel,
                        Is.EqualTo("EXPORTER-Time-Based-One-Time-Password-v1"));
        }

        #endregion

    }

}
