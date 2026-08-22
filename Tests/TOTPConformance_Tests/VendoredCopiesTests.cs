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

#endregion

namespace cloud.charging.open.TOTP.Conformance.tests
{

    /// <summary>
    /// Both implementation repositories carry VENDORED COPIES of the canonical
    /// vector files, so that their own test suites run against the very same
    /// vectors without a submodule (which would be cyclic - this repository
    /// already contains both implementations as submodules).
    ///
    /// These tests fail when a vendored copy drifts from the canonical file.
    /// In CI that guards the pinned submodules; in the nightly run - which
    /// advances the submodules to their upstream HEADs - it guards the
    /// upstream branches. Fix drift by regenerating:
    /// python tools/generate-test-vectors.py mirrors the canonical files into
    /// the checked-out submodules; then commit the implementation repository.
    /// </summary>
    [TestFixture]
    public class VendoredCopiesTests
    {

        #region Data

        private static readonly String[] fileNames = [
            "totp-test-vectors.json",
            "totp-invalid-inputs.json"
        ];

        private static readonly (String Name, String RelativePath)[] vendors = [
            ("hermod",   Path.Combine("libs", "Hermod",  "HermodTests", "TOTP", "TestVectors")),
            ("totp.ts",  Path.Combine("libs", "TOTP.TS", "test",        "vectors"))
        ];


        /// <summary>
        /// The repository root, found by walking up from the test assembly to
        /// the solution file - stable however the test host is started.
        /// </summary>
        private static DirectoryInfo RepositoryRoot()
        {

            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null &&
                  !File.Exists(Path.Combine(directory.FullName, "TOTPConformanceTests.slnx")))
            {
                directory = directory.Parent!;
            }

            return directory
                       ?? throw new InvalidOperationException("Could not locate the repository root!");

        }

        public static IEnumerable<TestCaseData> VendoredFiles()

            => from vendor   in vendors
               from fileName in fileNames
               select new TestCaseData(vendor.RelativePath, fileName)
                          .SetArgDisplayNames($"{vendor.Name}: {fileName}");

        #endregion


        #region VendoredCopyMatchesCanonicalFile (per implementation and file)

        [TestCaseSource(nameof(VendoredFiles))]
        public void VendoredCopyMatchesCanonicalFile(String  VendorPath,
                                                     String  FileName)
        {

            var root       = RepositoryRoot().FullName;
            var canonical  = Path.Combine(root, "TestVectors", FileName);
            var vendored   = Path.Combine(root, VendorPath,    FileName);

            Assert.That(File.Exists(vendored),
                        Is.True,
                        $"Missing vendored copy '{vendored}'!");

            // Compared after newline normalization: the byte-level guarantee for the
            // canonical files lives in this repository's .gitattributes, but the
            // implementation repositories may check the copies out with CRLF.
            static String Normalized(String path)
                => File.ReadAllText(path).Replace("\r\n", "\n");

            Assert.That(Normalized(vendored),
                        Is.EqualTo(Normalized(canonical)),
                        $"'{vendored}' has drifted from the canonical file - " +
                         "regenerate with: python tools/generate-test-vectors.py");

        }

        #endregion

    }

}
