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

using Newtonsoft.Json;

#endregion

namespace cloud.charging.open.TOTP.Conformance.tests
{

    /// <summary>
    /// The canonical vector files: the specification's normative annex
    /// (libs/specification/TimeBasedOneTimePasswords/test-vectors/, copied to
    /// the output directory by the csproj), shared with the TypeScript
    /// harness.
    /// </summary>
    public static class VectorFiles
    {

        /// <summary>
        /// The name this harness (and its implementation under test) is known by
        /// within the vector files ("knownDeviations"/"notApplicable").
        /// </summary>
        public const String ImplementationName = "hermod";

        /// <summary>
        /// The optional capabilities Hermod provides ("requires" lists of vectors).
        /// </summary>
        public static readonly IReadOnlySet<String> Capabilities = new HashSet<String> {
            "tlsChannelBinding",
            "httpAuthentication"
        };


        public static GenerationVectorFile  Generation     { get; } = Load<GenerationVectorFile>("totp-test-vectors.json");

        public static InvalidInputFile      InvalidInputs  { get; } = Load<InvalidInputFile>   ("totp-invalid-inputs.json");

        public static HttpAuthVectorFile    HttpAuth       { get; } = Load<HttpAuthVectorFile> ("totp-http-auth-vectors.json");


        private static T Load<T>(String FileName)
        {

            var path = Path.Combine(AppContext.BaseDirectory, "TestVectors", FileName);

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path))
                       ?? throw new InvalidOperationException($"Could not parse '{path}'!");

        }

    }


    public sealed class GenerationVectorFile
    {
        public List<GenerationVector>  Vectors  { get; set; } = [];
    }

    public sealed class GenerationVector
    {

        public String          Id           { get; set; } = "";
        public String          Description  { get; set; } = "";
        public VectorInput     Input        { get; set; } = new();
        public VectorExpected  Expected     { get; set; } = new();
        public List<String>?   Requires     { get; set; }

        public override String ToString() => Id;

    }

    public sealed class VectorInput
    {

        public String   SharedSecret            { get; set; } = "";
        public Int64    UnixTimestampMillis     { get; set; }

        // Numeric inputs are kept as raw JSON numbers: invalid input vectors
        // carry values (12.5, 256, -30) that only exist to probe validation.
        public Double?  ValidityTimeSeconds     { get; set; }
        public Double?  TotpLength              { get; set; }

        public String?  Alphabet                { get; set; }
        public String?  HashAlgorithm           { get; set; }
        public String?  TlsExporterMaterialHex  { get; set; }

    }

    public sealed class VectorExpected
    {
        public String  Previous              { get; set; } = "";
        public String  Current               { get; set; } = "";
        public String  Next                  { get; set; } = "";
        public Int32   RemainingTimeSeconds  { get; set; }
    }


    public sealed class InvalidInputFile
    {
        public List<InvalidInputVector>  Vectors  { get; set; } = [];
    }


    public sealed class HttpAuthVectorFile
    {
        public List<String>                 Requires        { get; set; } = [];
        public List<HttpAuthVector>         Vectors         { get; set; } = [];
        public List<HttpAuthParseVector>    ParseVectors    { get; set; } = [];
        public List<InvalidHeaderVector>    InvalidHeaders  { get; set; } = [];
    }

    public sealed class HttpAuthVector
    {
        public String            Id           { get; set; } = "";
        public String            Description  { get; set; } = "";
        public HttpAuthTriple    Input        { get; set; } = new();
        public HttpAuthExpected  Expected     { get; set; } = new();
        public override String ToString() => Id;
    }

    public sealed class HttpAuthParseVector
    {
        public String          Id           { get; set; } = "";
        public String          Description  { get; set; } = "";
        public String          Header       { get; set; } = "";
        public HttpAuthTriple  Expected     { get; set; } = new();
        public override String ToString() => Id;
    }

    public sealed class HttpAuthTriple
    {
        public String   Login  { get; set; } = "";
        public String   Totp   { get; set; } = "";
        public Boolean  Tlscb  { get; set; }
    }

    public sealed class HttpAuthExpected
    {
        public String  Header  { get; set; } = "";
    }

    public sealed class InvalidHeaderVector
    {
        public String  Id           { get; set; } = "";
        public String  Description  { get; set; } = "";
        public String  Header       { get; set; } = "";
        public override String ToString() => Id;
    }

    public sealed class InvalidInputVector
    {

        public String         Id               { get; set; } = "";
        public String         Description      { get; set; } = "";
        public VectorInput    Input            { get; set; } = new();
        public String         ExpectedError    { get; set; } = "";
        public List<String>?  KnownDeviations  { get; set; }
        public List<String>?  NotApplicable    { get; set; }

        public override String ToString() => Id;

    }

}
