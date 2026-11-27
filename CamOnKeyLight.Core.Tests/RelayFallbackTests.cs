using System.Net;
using System.Text;
using CamOnKeyLight.Core;

namespace CamOnKeyLight.Core.Tests
{
    public class RelayFallbackTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void DirectTimeoutStaysFiveSecondsUntilARelayIsConfigured()
        {
            Assert.Equal(TimeSpan.FromSeconds(5), LightTiming.LightRequestTimeout);
            Assert.True(LightTiming.DirectAttemptWhenRelayConfigured < TimeSpan.FromSeconds(1));
            Assert.True(LightTiming.RelayRequestTimeout < LightTiming.LightRequestTimeout);
        }

        [Fact]
        public async Task DirectSuccessDoesNotCallTheRelay()
        {
            ScriptHandler handler = new ScriptHandler
            {
                Respond = (_, _) => Task.FromResult(LightOk(on: true))
            };
            using Harness harness = new Harness(handler);
            LightExchange exchange = await harness.Dispatch.SendAsync(Request("https://lights.example.com"), TestContext.Current.CancellationToken);

            Assert.Equal(LightRoute.Direct, exchange.Route);
            Assert.False(exchange.ConnectionFailed);
            Assert.Single(handler.Seen);
            Assert.Equal("192.168.1.5", handler.Seen[0].Url.Host);
            Assert.False(harness.Dispatch.SkippingDirect(Now));
        }

        [Fact]
        public async Task HttpErrorFromTheLightIsNotAReasonToUseTheRelay()
        {
            ScriptHandler handler = new ScriptHandler
            {
                Respond = (request, _) => Task.FromResult(request.RequestUri!.Host == "192.168.1.5"
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : LightOk(on: false))
            };
            using Harness harness = new Harness(handler);
            LightExchange exchange = await harness.Dispatch.SendAsync(Request("https://lights.example.com"), TestContext.Current.CancellationToken);

            Assert.Equal(LightRoute.Direct, exchange.Route);
            Assert.Equal(500, exchange.StatusCode);
            Assert.Single(handler.Seen);
            Assert.False(harness.Dispatch.SkippingDirect(Now.AddSeconds(1)));
        }

        [Fact]
        public async Task ConnectionFailureFallsBackAndRemembers()
        {
            ScriptHandler handler = new ScriptHandler
            {
                Respond = (request, _) =>
                {
                    if (request.RequestUri!.Host == "192.168.1.5")
                        throw new HttpRequestException("refused");

                    return Task.FromResult(LightOk(on: true));
                }
            };
            using Harness harness = new Harness(handler);
            RelayCredentials credentials = Credentials();
            LightExchange exchange = await harness.Dispatch.SendAsync(Request("https://lights.example.com", credentials), TestContext.Current.CancellationToken);

            Assert.Equal(LightRoute.Relay, exchange.Route);
            Assert.False(exchange.ConnectionFailed);
            Assert.Equal(200, exchange.StatusCode);
            Assert.True(harness.Dispatch.SkippingDirect(Now.AddSeconds(10)));
            Seen relay = handler.Seen[1];
            Assert.Equal("/v1/lights", relay.Url.AbsolutePath);
            Assert.Equal("address=192.168.1.5", relay.Url.Query.TrimStart('?'));
            Assert.Equal("client-id", relay.Headers[RelayHeaders.AccessClientId]);
            Assert.Equal("client-secret", relay.Headers[RelayHeaders.AccessClientSecret]);
            Assert.Equal("shared-secret", relay.Headers[RelayHeaders.SharedSecret]);
            Assert.False(handler.Seen[0].Headers.ContainsKey(RelayHeaders.AccessClientId));

            int before = handler.Seen.Count;
            LightExchange again = await harness.Dispatch.SendAsync(Request("https://lights.example.com", credentials, Now.AddSeconds(10)), TestContext.Current.CancellationToken);
            Assert.Equal(LightRoute.Relay, again.Route);
            Assert.DoesNotContain(handler.Seen.Skip(before), call => call.Url.Host == "192.168.1.5");

            await harness.Dispatch.SendAsync(Request("https://lights.example.com", credentials, Now.Add(LightTiming.DirectFailureMemory).AddSeconds(1)), TestContext.Current.CancellationToken);
            Assert.Contains(handler.Seen, call => call.Url.Host == "192.168.1.5");
        }

        [Fact]
        public async Task DirectAttemptFailsFastEnoughToSwitch()
        {
            ScriptHandler handler = new ScriptHandler
            {
                Respond = async (request, cancellationToken) =>
                {
                    if (request.RequestUri!.Host == "192.168.1.5")
                        await Task.Delay(Timeout.Infinite, cancellationToken);

                    return LightOk(on: false);
                }
            };
            using Harness harness = new Harness(handler);
            DateTime started = DateTime.UtcNow;
            LightExchange exchange = await harness.Dispatch.SendAsync(Request("https://lights.example.com"), TestContext.Current.CancellationToken);
            TimeSpan elapsed = DateTime.UtcNow - started;

            Assert.Equal(LightRoute.Relay, exchange.Route);
            Assert.True(elapsed < TimeSpan.FromSeconds(3), "fallback took " + elapsed);
        }

        [Fact]
        public async Task WithoutARelayTheDirectAttemptIsTheOnlyAttempt()
        {
            ScriptHandler handler = new ScriptHandler
            {
                Respond = (_, _) => throw new HttpRequestException("refused")
            };
            using Harness harness = new Harness(handler);
            LightExchange exchange = await harness.Dispatch.SendAsync(Request(""), TestContext.Current.CancellationToken);

            Assert.Equal(LightRoute.Direct, exchange.Route);
            Assert.True(exchange.ConnectionFailed);
            Assert.Single(handler.Seen);
            Assert.False(harness.Dispatch.SkippingDirect(Now));
            LightExchangeMeaning.Describe(exchange, "192.168.1.5", out LightLinkState state, out string detail);
            Assert.Equal(LightLinkState.Unreachable, state);
            Assert.Equal(LightDiagnostics.Unreachable("192.168.1.5"), detail);
        }

        [Fact]
        public void RelayStatusTextNamesThePathAndGivesNoNetworkAdvice()
        {
            LightStatusBoard board = new LightStatusBoard();
            board.SyncAddresses(new[] { "192.168.1.5" });
            int attempt = board.BeginBackgroundRefresh("192.168.1.5");
            Assert.True(board.TryComplete("192.168.1.5", attempt, LightLinkState.On, "on", LightRoute.Relay));
            LightRow row = Assert.Single(board.Rows);
            Assert.Equal("On via relay", row.StatusText);
            Assert.Equal(LightRoute.Relay, row.Route);
            Assert.Equal("Lights are ON via relay", board.Summary);
            Assert.True(board.UsesRelay);

            string[] messages =
            {
                LightDiagnostics.RelayUnreachable("192.168.1.5"),
                LightDiagnostics.RelayRefused("192.168.1.5"),
                row.StatusText,
                board.Summary
            };
            foreach (string message in messages)
            {
                Assert.DoesNotContain("VPN", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("route", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("firewall", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("proxy", message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void SecretsStayOutOfTheSettingsJson()
        {
            string secret = "super-secret-value";
            AppUserSettings settings = AppUserSettings.CreateDefault();
            settings.RelayUrl = "https://lights.example.com";
            string json = SettingsJson.Serialize(settings);
            Assert.Contains("https://lights.example.com", json, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
            Assert.DoesNotContain("clientSecret", json, StringComparison.OrdinalIgnoreCase);

            AppUserSettings? older = SettingsJson.Deserialize("{\"lights\":[],\"cameraOffDelayMs\":0}");
            Assert.NotNull(older);
            Assert.Equal("", older!.RelayUrl);

            string path = Path.Combine(Path.GetTempPath(), "relay-secrets-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                RelayCredentials credentials = new RelayCredentials
                {
                    ClientId = "client-id",
                    ClientSecret = secret,
                    SharedSecret = "shared"
                };
                RelaySecretFile.Save(path, credentials, new TestProtector());
                byte[] stored = File.ReadAllBytes(path);
                Assert.True(stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)) < 0);
                RelayCredentials loaded = RelaySecretFile.Load(path, new TestProtector());
                Assert.Equal(secret, loaded.ClientSecret);
                Assert.Equal("shared", loaded.SharedSecret);
                RelaySecretFile.Save(path, new RelayCredentials(), new TestProtector());
                Assert.False(File.Exists(path));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public async Task ProbeReportsTheConfiguredLightCount()
        {
            ScriptHandler handler = new ScriptHandler
            {
                Respond = (_, _) =>
                {
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new StringContent("{\"ok\":true,\"count\":2}", Encoding.UTF8, "application/json");
                    return Task.FromResult(response);
                }
            };
            using HttpClient client = new HttpClient(handler, disposeHandler: false);
            string message = await RelayProbe.DescribeAsync("https://lights.example.com/", Credentials(), client, TestContext.Current.CancellationToken);
            Assert.Equal("The relay answered. 2 lights are configured.", message);
            Assert.Equal("https://lights.example.com/v1/ping", handler.Seen[0].Url.ToString());
            Assert.Equal("shared-secret", handler.Seen[0].Headers[RelayHeaders.SharedSecret]);
        }

        [Theory]
        [InlineData("", true, "")]
        [InlineData("https://lights.example.com/", true, "https://lights.example.com")]
        [InlineData("http://nas.local:8080", true, "http://nas.local:8080")]
        [InlineData("lights.example.com", false, "")]
        [InlineData("https://user:secret@lights.example.com", false, "")]
        public void RelayAddressIsNormalized(string raw, bool ok, string expected)
        {
            Assert.Equal(ok, RelayUrls.TryNormalize(raw, out string url, out _));
            Assert.Equal(expected, url);
        }

        private sealed class Harness : IDisposable
        {
            private readonly LightHttpClient http;

            public Harness(ScriptHandler handler)
            {
                http = new LightHttpClient(handler, handler, disposeHandlers: false);
                Dispatch = new LightDispatch(http);
            }

            public LightDispatch Dispatch { get; }

            public void Dispose()
            {
                http.Dispose();
            }
        }

        private static LightExchangeRequest Request(string relayUrl, RelayCredentials? credentials = null, DateTime? utcNow = null)
        {
            return new LightExchangeRequest(
                "192.168.1.5",
                Read: true,
                Body: null,
                RelayUrl: relayUrl,
                Credentials: credentials ?? Credentials(),
                UtcNow: utcNow ?? Now);
        }

        private static RelayCredentials Credentials()
        {
            return new RelayCredentials
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                SharedSecret = "shared-secret"
            };
        }

        private static HttpResponseMessage LightOk(bool on)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(
                "{\"numberOfLights\":1,\"lights\":[{\"on\":" + (on ? "1" : "0") + "}]}",
                Encoding.UTF8,
                "application/json");
            return response;
        }

        private sealed class ScriptHandler : HttpMessageHandler
        {
            public List<Seen> Seen { get; } = new List<Seen>();
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                    headers[header.Key] = string.Join(",", header.Value);

                Seen.Add(new Seen(request.RequestUri!, headers, body));
                if (Respond == null)
                    return new HttpResponseMessage(HttpStatusCode.OK);

                return await Respond(request, cancellationToken);
            }
        }

        private sealed record Seen(Uri Url, IReadOnlyDictionary<string, string> Headers, string? Body);

        private sealed class TestProtector : ISecretProtector
        {
            public byte[] Protect(byte[] plaintext)
            {
                byte[] copy = (byte[])plaintext.Clone();
                for (int i = 0; i < copy.Length; i++)
                    copy[i] ^= 0x5A;

                return copy;
            }

            public byte[] Unprotect(byte[] ciphertext)
            {
                return Protect(ciphertext);
            }
        }
    }
}
