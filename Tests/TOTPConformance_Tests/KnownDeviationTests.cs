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
    /// Characterization tests for the documented deviations of Hermod from the
    /// specification - see docs/spec-deviations.md.
    ///
    /// These tests pin Hermod's CURRENT behaviour, so that a fix does not go
    /// unnoticed: when one of them starts to fail, Hermod has changed - then
    /// update docs/spec-deviations.md and delete the test here.
    ///
    /// The input validation gaps that used to live here (validity time,
    /// pre-epoch timestamps) were fixed in Hermod on 2026-08-22; their vectors
    /// run as regular InvalidInputVectorTests now.
    /// </summary>
    [TestFixture]
    [Category("KnownDeviation")]
    public class KnownDeviationTests
    {

        private const String sharedSecret = "secure!Charging!";


        #region LastSlotOfTheCalendar_EndTimeOverflows

        /// <summary>
        /// Found by the "timestamp-year-9999" vector (which therefore uses
        /// 23:59:28): the very last slot of the calendar ends at
        /// 10000-01-01T00:00:00Z ((S + 1) * V, spec section 4.1), which
        /// Hermod's DateTimeOffset EndTime cannot represent, so it throws instead of
        /// returning the - perfectly derivable - tokens. TOTP.ts returns no
        /// end time and is unaffected. Purely theoretical.
        /// </summary>
        [Test]
        public void LastSlotOfTheCalendar_EndTimeOverflows()
        {

            Assert.Throws<ArgumentOutOfRangeException>(
                () => TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null,
                                                  DateTimeOffset.FromUnixTimeSeconds(253402300799))
            );

        }

        #endregion

    }

}
