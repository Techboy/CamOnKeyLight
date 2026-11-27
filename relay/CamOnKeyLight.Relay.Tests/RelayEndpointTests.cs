using System.Net;
using System.Text;
using CamOnKeyLight.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CamOnKeyLight.Relay.Tests
{
    public class RelayEndpointTests : IAsyncLifetime
    {
        private FakeLight light = null!;
        private RelayFactory factory = null!;
        private HttpClient client = null!;

        public async ValueTask InitializeAsync()
        {
            light = await FakeLight.StartAsync();
            factory = new RelayFactory(light.Address, "test-secret");
            client = factory.CreateClient();
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await factory.DisposeAsync();
            await light.DisposeAsync();
        }

        [Fact]
        public void DefaultPortMatchesTheExplicitPort()
        {
            LightCatalog catalog = LightCatalog.Parse("192.168.1.5, 192.168.1.6:9000");
            Assert.True(catalog.TryResolve("192.168.1.5:9123", out string url));
            Assert.Equal("http://192.168.1.5:9123/elgato/lights", url);
            Assert.False(catalog.TryResolve("10.9.9.9", out _));
            Assert.Equal(2, catalog.Count);
        }

        [Fact]
        public async Task HealthDoesNotRequireTheSecret()
        {
            HttpResponseMessage health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Contains("ok", await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

            HttpResponseMessage denied = await client.GetAsync("/v1/ping", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }

        [Fact]
        public async Task PingAcceptsOnlyTheSharedSecret()
        {
            using HttpRequestMessage wrong = new HttpRequestMessage(HttpMethod.Get, "/v1/ping");
            wrong.Headers.TryAddWithoutValidation("X-Relay-Secret", "nope");
            HttpResponseMessage rejected = await client.SendAsync(wrong, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

            using HttpRequestMessage right = Authorized(HttpMethod.Get, "/v1/ping");
            HttpResponseMessage accepted = await client.SendAsync(right, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            Assert.Contains("\"count\":1", await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }

        [Fact]
        public async Task UnknownAddressIsNotContacted()
        {
            using HttpRequestMessage request = Authorized(HttpMethod.Get, "/v1/lights?address=10.1.1.1");
            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(0, light.Calls);
        }

        [Fact]
        public async Task StatusOnOffAndTemperatureAreForwarded()
        {
            using HttpRequestMessage status = Authorized(HttpMethod.Get, "/v1/lights?address=" + Uri.EscapeDataString(light.Address));
            HttpResponseMessage statusResponse = await client.SendAsync(status, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            Assert.Contains("\"on\":1", await statusResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.Equal("GET", light.LastMethod);

            string temperature = "{\"numberOfLights\":1,\"lights\":[{\"on\":1,\"brightness\":40,\"temperature\":290}]}";
            using HttpRequestMessage put = Authorized(HttpMethod.Put, "/v1/lights?address=" + Uri.EscapeDataString(light.Address));
            put.Content = new StringContent(temperature, Encoding.UTF8, "application/json");
            HttpResponseMessage putResponse = await client.SendAsync(put, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
            Assert.Equal("PUT", light.LastMethod);
            Assert.Equal(temperature, light.LastBody);

            using HttpRequestMessage on = Authorized(HttpMethod.Post, "/v1/lights/on?address=" + Uri.EscapeDataString(light.Address));
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(on, TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(LightEndpoint.PutBody(true), light.LastBody);

            using HttpRequestMessage off = Authorized(HttpMethod.Post, "/v1/lights/off?address=" + Uri.EscapeDataString(light.Address));
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(off, TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(LightEndpoint.PutBody(false), light.LastBody);
        }

        [Fact]
        public async Task AddressOutsideTheListIsNotContacted()
        {
            using HttpRequestMessage request = Authorized(HttpMethod.Get, "/v1/lights?address=127.0.0.1:9");
            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UnreachableConfiguredLightReturns502()
        {
            RelayFactory closed = new RelayFactory("127.0.0.1:9", "test-secret");
            using HttpClient closedClient = closed.CreateClient();
            using HttpRequestMessage request = Authorized(HttpMethod.Post, "/v1/lights/off?address=127.0.0.1:9");
            HttpResponseMessage response = await closedClient.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            closed.Dispose();
        }

        private static HttpRequestMessage Authorized(HttpMethod method, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("X-Relay-Secret", "test-secret");
            return request;
        }

        private sealed class RelayFactory : WebApplicationFactory<Program>
        {
            private readonly string lights;
            private readonly string secret;

            public RelayFactory(string lights, string secret)
            {
                this.lights = lights;
                this.secret = secret;
            }

            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.ConfigureHostConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["LIGHTS"] = lights,
                        ["RELAY_SHARED_SECRET"] = secret,
                        ["DISCOVER"] = "false"
                    });
                });
                return base.CreateHost(builder);
            }
        }

        private sealed class FakeLight : IAsyncDisposable
        {
            private readonly WebApplication app;

            private FakeLight(WebApplication app, string address)
            {
                this.app = app;
                Address = address;
            }

            public string Address { get; }
            public int Calls { get; private set; }
            public string LastMethod { get; private set; } = "";
            public string LastBody { get; private set; } = "";

            public static async Task<FakeLight> StartAsync()
            {
                int port = FreePort();
                WebApplicationBuilder builder = WebApplication.CreateBuilder();
                builder.Logging.ClearProviders();
                WebApplication app = builder.Build();
                FakeLight fake = new FakeLight(app, "127.0.0.1:" + port);
                app.MapGet("/elgato/lights", () =>
                {
                    fake.Calls++;
                    fake.LastMethod = "GET";
                    fake.LastBody = "";
                    return Results.Text("{\"numberOfLights\":1,\"lights\":[{\"on\":1,\"brightness\":20}]}", "application/json");
                });
                app.MapPut("/elgato/lights", async (HttpRequest request) =>
                {
                    fake.Calls++;
                    fake.LastMethod = "PUT";
                    using StreamReader reader = new StreamReader(request.Body);
                    fake.LastBody = await reader.ReadToEndAsync();
                    return Results.Text("{\"numberOfLights\":1,\"lights\":[{\"on\":1}]}", "application/json");
                });
                app.Urls.Add("http://127.0.0.1:" + port);
                await app.StartAsync();
                return fake;
            }

            public async ValueTask DisposeAsync()
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }

            private static int FreePort()
            {
                using System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
        }
    }
}
