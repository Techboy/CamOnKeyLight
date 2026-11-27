using System.Net;

namespace CamOnKeyLight.Relay.Tests
{
    public class HealthCheckTests
    {
        [Theory]
        [InlineData("http://0.0.0.0:8091", "http://127.0.0.1:8091/health")]
        [InlineData("http://+:8080", "http://127.0.0.1:8080/health")]
        [InlineData("http://*:8080", "http://127.0.0.1:8080/health")]
        [InlineData("http://[::]:8091", "http://127.0.0.1:8091/health")]
        [InlineData("http://127.0.0.1:8091", "http://127.0.0.1:8091/health")]
        [InlineData("HTTP://0.0.0.0:8091", "http://127.0.0.1:8091/health")]
        [InlineData("https://+:8443;http://0.0.0.0:8091", "http://127.0.0.1:8091/health")]
        [InlineData(" http://nas.local:8091 ", "http://nas.local:8091/health")]
        public void UrlsChooseTheFirstHttpAddress(string urls, string expected)
        {
            Assert.True(RelayHealthCheck.TryResolve(urls, "", "8080", out Uri probe, out _));
            Assert.Equal(expected, probe.AbsoluteUri);
        }

        [Fact]
        public void EmptyHttpPortsDoesNotHideTheConfiguredUrl()
        {
            Assert.True(RelayHealthCheck.TryResolve("http://0.0.0.0:8091", "", "8080", out Uri probe, out _));
            Assert.Equal("http://127.0.0.1:8091/health", probe.AbsoluteUri);
        }

        [Fact]
        public void HttpPortsAreUsedWhenNoUrlIsSet()
        {
            Assert.True(RelayHealthCheck.TryResolve(null, "8091;8092", "8080", out Uri probe, out _));
            Assert.Equal("http://127.0.0.1:8091/health", probe.AbsoluteUri);
        }

        [Fact]
        public void PrefixedHttpPortsAreUsedWhenHttpPortsIsUnset()
        {
            Assert.True(RelayHealthCheck.TryResolve(null, null, "8091", out Uri probe, out _));
            Assert.Equal("http://127.0.0.1:8091/health", probe.AbsoluteUri);
        }

        [Fact]
        public void DefaultPortIs8080()
        {
            Assert.True(RelayHealthCheck.TryResolve(null, null, null, out Uri probe, out _));
            Assert.Equal("http://127.0.0.1:8080/health", probe.AbsoluteUri);
        }

        [Fact]
        public void HttpsOnlyUrlsAreRejected()
        {
            Assert.False(RelayHealthCheck.TryResolve("https://+:8443", "", "", out _, out string? error));
            Assert.Contains("http", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProbeAcceptsAHealthyReply()
        {
            RecordingHandler handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using HttpClient client = new HttpClient(handler);
            int code = await RelayHealthCheck.ExecuteAsync(client, new StringWriter(), TestContext.Current.CancellationToken);
            Assert.Equal(0, code);
        }

        [Fact]
        public async Task ProbeFailsWhenThePortDoesNotAnswer()
        {
            RecordingHandler handler = new RecordingHandler(_ => throw new HttpRequestException("refused"));
            using HttpClient client = new HttpClient(handler);
            StringWriter error = new StringWriter();
            int code = await RelayHealthCheck.ExecuteAsync(client, error, TestContext.Current.CancellationToken);
            Assert.Equal(1, code);
            Assert.Contains("did not answer", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ImageHealthCheckDoesNotCallWget()
        {
            string? root = RepoRoot();
            Assert.NotNull(root);
            string docker = File.ReadAllText(Path.Combine(root!, "relay", "Dockerfile"));
            Assert.Contains("[\"dotnet\", \"CamOnKeyLight.Relay.dll\", \"--healthcheck\"]", docker, StringComparison.Ordinal);
            Assert.DoesNotContain("wget", docker, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("127.0.0.1:8080/health", docker, StringComparison.Ordinal);
        }

        private static string? RepoRoot()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CamOnKeyLight.sln")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                this.respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(respond(request));
            }
        }
    }
}
