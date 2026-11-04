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

using System.Text;
using System.Net.Sockets;
using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace cloud.charging.open.TOTP.Conformance.tests
{

    /// <summary>
    /// Runs Hermod's server-side verification against the canonical
    /// verification vectors in totp-http-verification-vectors.json of the
    /// specification's annex - sections 5.3, 6.2 and 8 of
    /// totp-http-authentication.md: accepted, rejected (401) or malformed
    /// (400 or 401).
    ///
    /// Not a model of the verifier, the real one: a live WebSocketServer
    /// holding the vector file's logins in ClientTOTPConfig, its clock moved
    /// to the vector file's instant (Styx's Timestamp time travel), asked
    /// for an upgrade over plain TCP with each vector's Authorization header
    /// written byte for byte. Without TLS there is no exporter material, as
    /// the vector file assumes.
    /// </summary>
    [TestFixture, NonParallelizable]
    public class HttpVerificationVectorTests
    {

        #region Data

        private WebSocketServer?          server;
        private ConcurrentQueue<String?>  acceptedLogins  = new();

        #endregion

        #region (static) Vector sources

        public static IEnumerable<TestCaseData> Vectors()
            => VectorFiles.Verification.Vectors.Select(vector => new TestCaseData(vector).SetArgDisplayNames(vector.Id));

        #endregion


        #region Start/stop the verifier

        [OneTimeSetUp]
        public void StartVerifier()
        {

            server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);

            foreach (var (login, config) in VectorFiles.Verification.Verifier.Logins)
            {

                TOTPHashAlgorithm? hashAlgorithm = null;

                if (config.HashAlgorithm is not null)
                {
                    Assert.That(TOTPHashAlgorithmExtensions.TryParse(config.HashAlgorithm, out var parsed),
                                Is.True,
                                $"Unknown hash algorithm '{config.HashAlgorithm}'!");
                    hashAlgorithm = parsed;
                }

                server.ClientTOTPConfig[login] = new TOTPConfig(
                                                     config.SharedSecret,
                                                     config.ValidityTimeSeconds.HasValue
                                                         ? TimeSpan.FromSeconds(config.ValidityTimeSeconds.Value)
                                                         : null,
                                                     config.TotpLength,
                                                     config.Alphabet,
                                                     UseTLSExporterMaterial: false,
                                                     HashAlgorithm:          hashAlgorithm
                                                 );

            }

            // Accepted comes before the 101 goes out, so the login is in
            // here once the status line has arrived.
            server.OnWebSocketConnectionAccepted += (timestamp, webSocketServer, connection, sharedSubprotocols,
                                                     selectedSubprotocol, eventTrackingId, cancellationToken) => {
                acceptedLogins.Enqueue(connection.Login);
                return Task.CompletedTask;
            };

        }

        [OneTimeTearDown]
        public async Task StopVerifier()
        {

            Timestamp.Reset();

            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }

        }

        [TearDown]
        public void ReturnToNormalTime()
        {
            Timestamp.Reset();
        }

        #endregion


        #region (private) MoveClockTo(UnixTimestampMillis)

        /// <summary>
        /// Set the verifier's clock to the given instant. The vector file keeps
        /// its instant well before the end of its slot for every login, so the
        /// milliseconds the request takes afterwards do not matter.
        /// </summary>
        private static void MoveClockTo(Int64 UnixTimestampMillis)
        {

            Timestamp.Reset();

            var offset = DateTimeOffset.FromUnixTimeMilliseconds(UnixTimestampMillis) - Timestamp.Now;

            if (offset < TimeSpan.Zero)
                Timestamp.TravelBackInTime   (offset);
            else
                Timestamp.TravelForwardInTime(offset);

        }

        #endregion

        #region (private) UpgradeAsync(Authorization)

        /// <summary>
        /// Ask for a WebSocket upgrade with the given Authorization header, or
        /// none, and return the status code of the answer.
        /// </summary>
        private async Task<Int32> UpgradeAsync(String? Authorization)
        {

            var port = server!.IPPort.ToUInt16();

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", port);

            var stream  = tcpClient.GetStream();
            var request = new StringBuilder()
                              .Append("GET / HTTP/1.1\r\n")
                              .Append($"Host: 127.0.0.1:{port}\r\n")
                              .Append("Upgrade: websocket\r\n")
                              .Append("Connection: Upgrade\r\n")
                              .Append("Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n")
                              .Append("Sec-WebSocket-Version: 13\r\n");

            if (Authorization is not null)
                request.Append($"Authorization: {Authorization}\r\n");

            request.Append("\r\n");

            await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()));

            // Only the status line matters: "HTTP/1.1 101 Switching Protocols".
            var buffer    = new Byte[1024];
            var received  = new StringBuilder();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            while (!received.ToString().Contains("\r\n"))
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                    break;
                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            var statusLine = received.ToString().Split("\r\n")[0];
            var parts      = statusLine.Split(' ');

            Assert.That(parts, Has.Length.GreaterThanOrEqualTo(2), $"No HTTP status line: '{statusLine}'!");

            return Int32.Parse(parts[1]);

        }

        #endregion


        #region FileIsLoadedAndApplicable

        [Test]
        public void FileIsLoadedAndApplicable()
        {

            Assert.Multiple(() => {

                Assert.That(VectorFiles.Verification.Requires,
                            Is.SubsetOf(VectorFiles.Capabilities),
                            "This harness lacks a capability the vector file requires!");

                Assert.That(VectorFiles.Verification.Verifier.Logins, Has.Count.GreaterThanOrEqualTo(2));
                Assert.That(VectorFiles.Verification.Vectors,         Has.Count.GreaterThanOrEqualTo(20));

                Assert.That(VectorFiles.Verification.Vectors.Select(vector => vector.Expected.Outcome),
                            Is.All.AnyOf("accepted", "rejected", "malformed"));

            });

        }

        #endregion

        #region VerifierAnswers (vector-driven)

        [TestCaseSource(nameof(Vectors))]
        public async Task VerifierAnswers(VerificationVector Vector)
        {

            acceptedLogins.Clear();

            MoveClockTo(VectorFiles.Verification.Verifier.UnixTimestampMillis);

            var status = await UpgradeAsync(Vector.Authorization);

            switch (Vector.Expected.Outcome)
            {

                case "accepted":
                    Assert.That(status, Is.EqualTo(101), "accepted: 101 Switching Protocols");
                    Assert.That(acceptedLogins.TryDequeue(out var login), Is.True, "No connection was accepted!");
                    Assert.That(login, Is.EqualTo(Vector.Expected.Login), "The authenticated login");
                    break;

                case "rejected":
                    Assert.That(status, Is.EqualTo(401), "rejected: 401 Unauthorized");
                    break;

                case "malformed":
                    Assert.That(status, Is.AnyOf(400, 401), "malformed: 400 Bad Request, or 401");
                    break;

                default:
                    Assert.Fail($"Unknown outcome '{Vector.Expected.Outcome}'!");
                    break;

            }

        }

        #endregion

    }

}
