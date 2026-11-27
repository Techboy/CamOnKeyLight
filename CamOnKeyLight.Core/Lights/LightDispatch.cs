using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace CamOnKeyLight.Core
{
    public readonly record struct UpstreamResult(bool ConnectionFailed, int StatusCode, string Body);

    public readonly record struct LightExchange(LightRoute Route, bool ConnectionFailed, int StatusCode, string Body)
    {
        public static LightExchange From(UpstreamResult result, LightRoute route)
        {
            return new LightExchange(route, result.ConnectionFailed, result.StatusCode, result.Body);
        }

        public static LightExchange Failed(LightRoute route)
        {
            return new LightExchange(route, true, 0, "");
        }
    }

    public readonly record struct LightExchangeRequest(
        string Address,
        bool Read,
        string? Body,
        string? RelayUrl,
        RelayCredentials Credentials,
        DateTime UtcNow);

    public static class LightExchangeMeaning
    {
        public static void Describe(LightExchange exchange, string address, out LightLinkState state, out string detail)
        {
            if (exchange.ConnectionFailed || exchange.StatusCode == 502)
            {
                state = LightLinkState.Unreachable;
                detail = exchange.Route == LightRoute.Relay
                    ? LightDiagnostics.RelayUnreachable(address)
                    : LightDiagnostics.Unreachable(address);
                return;
            }

            if (exchange.Route == LightRoute.Relay && exchange.StatusCode is 401 or 403)
            {
                state = LightLinkState.Unreachable;
                detail = LightDiagnostics.RelayRefused(address);
                return;
            }

            if (exchange.StatusCode is < 200 or >= 300)
            {
                state = LightLinkState.Unreachable;
                detail = LightDiagnostics.HttpFailure(address, exchange.StatusCode);
                return;
            }

            if (!LightResponse.TryReadPower(exchange.Body, out bool on))
            {
                state = LightLinkState.UnexpectedReply;
                detail = LightDiagnostics.Unexpected(address);
                return;
            }

            state = on ? LightLinkState.On : LightLinkState.Off;
            detail = on ? LightDiagnostics.OnDetail(address) : LightDiagnostics.OffDetail(address);
        }
    }

    /// <summary>
    /// Talks to a light directly, and to the home relay when the direct attempt
    /// cannot connect. With no relay configured, only the direct attempt runs.
    /// </summary>
    public sealed class LightDispatch
    {
        private readonly LightHttpClient http;
        private readonly object gate = new object();
        private DateTime skipDirectUntilUtc = DateTime.MinValue;

        public LightDispatch(LightHttpClient http)
        {
            this.http = http;
        }

        public bool SkippingDirect(DateTime utcNow)
        {
            lock (gate)
                return utcNow < skipDirectUntilUtc;
        }

        public async Task<LightExchange> SendAsync(LightExchangeRequest request, CancellationToken cancellationToken)
        {
            HttpMethod method = request.Read ? HttpMethod.Get : HttpMethod.Put;
            if (!RelayUrls.IsConfigured(request.RelayUrl))
            {
                UpstreamResult directOnly = await http.SendDirectAsync(
                    request.Address,
                    method,
                    request.Body,
                    LightTiming.LightRequestTimeout,
                    cancellationToken);
                return LightExchange.From(directOnly, LightRoute.Direct);
            }

            bool skipDirect;
            lock (gate)
                skipDirect = request.UtcNow < skipDirectUntilUtc;

            if (!skipDirect)
            {
                UpstreamResult direct = await http.SendDirectAsync(
                    request.Address,
                    method,
                    request.Body,
                    LightTiming.DirectAttemptWhenRelayConfigured,
                    cancellationToken);
                if (!direct.ConnectionFailed)
                {
                    lock (gate)
                        skipDirectUntilUtc = DateTime.MinValue;
                    return LightExchange.From(direct, LightRoute.Direct);
                }

                lock (gate)
                    skipDirectUntilUtc = request.UtcNow.Add(LightTiming.DirectFailureMemory);
            }

            if (!RelayUrls.TryNormalize(request.RelayUrl, out string relayUrl, out _))
                return LightExchange.Failed(LightRoute.Relay);

            string action = request.Read ? "status" : "put";
            UpstreamResult viaRelay = await http.SendRelayAsync(
                relayUrl,
                request.Credentials,
                request.Address,
                action,
                request.Body,
                LightTiming.RelayRequestTimeout,
                cancellationToken);
            return LightExchange.From(viaRelay, LightRoute.Relay);
        }
    }

    public sealed class LightHttpClient : IDisposable
    {
        private readonly HttpClient directClient;
        private readonly HttpClient relayClient;

        public LightHttpClient()
            : this(CreateHandler(LightTiming.LightRequestTimeout), CreateHandler(LightTiming.RelayRequestTimeout), disposeHandlers: true)
        {
        }

        public LightHttpClient(HttpMessageHandler directHandler, HttpMessageHandler relayHandler, bool disposeHandlers)
        {
            directClient = new HttpClient(directHandler, disposeHandlers)
            {
                Timeout = LightTiming.LightRequestTimeout
            };
            relayClient = new HttpClient(relayHandler, disposeHandlers)
            {
                Timeout = LightTiming.RelayRequestTimeout + TimeSpan.FromSeconds(2)
            };
        }

        public async Task<UpstreamResult> SendDirectAsync(
            string address,
            HttpMethod method,
            string? body,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (!LightAddressRules.TryCreateUrl(address, out string url, out _))
                return new UpstreamResult(false, 400, "");

            return await SendAsync(directClient, new HttpRequestMessage(method, url), body, method, timeout, cancellationToken);
        }

        public async Task<UpstreamResult> SendRelayAsync(
            string relayUrl,
            RelayCredentials credentials,
            string address,
            string action,
            string? body,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            HttpMethod method = action == "put" ? HttpMethod.Put : HttpMethod.Get;
            HttpRequestMessage message = new HttpRequestMessage(method, RelayUrls.Command(relayUrl, address, action));
            RelayHeaders.Apply(message, credentials);
            return await SendAsync(relayClient, message, body, method, timeout, cancellationToken);
        }

        public void Dispose()
        {
            directClient.Dispose();
            relayClient.Dispose();
        }

        private static async Task<UpstreamResult> SendAsync(
            HttpClient client,
            HttpRequestMessage message,
            string? body,
            HttpMethod method,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using (message)
            {
                if (body != null && method != HttpMethod.Get)
                    message.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(timeout);
                try
                {
                    using HttpResponseMessage response = await client.SendAsync(message, timeoutSource.Token);
                    string text = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                    return new UpstreamResult(false, (int)response.StatusCode, text);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
                {
                    return new UpstreamResult(true, 0, "");
                }
            }
        }

        private static SocketsHttpHandler CreateHandler(TimeSpan connectTimeout)
        {
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = connectTimeout
            };
        }
    }

    public static class RelayProbe
    {
        public static async Task<string> DescribeAsync(
            string? rawUrl,
            RelayCredentials credentials,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            if (!RelayUrls.TryNormalize(rawUrl, out string url, out string? error) || url.Length == 0)
                return error ?? "Enter the relay address first.";

            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, RelayUrls.Command(url, "", "ping"));
                RelayHeaders.Apply(request, credentials);
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(LightTiming.RelayRequestTimeout);
                using HttpResponseMessage response = await client.SendAsync(request, timeoutSource.Token);
                string body = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return "The relay refused the credentials.";

                if (!response.IsSuccessStatusCode)
                    return "The relay returned HTTP " + (int)response.StatusCode + ".";

                int count = ReadCount(body);
                if (count < 0)
                    return "The relay answered.";

                return count == 1
                    ? "The relay answered. 1 light is configured."
                    : "The relay answered. " + count + " lights are configured.";
            }
            catch (ArgumentException)
            {
                return "A credential contains a line break.";
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                return "The relay did not answer.";
            }
        }

        private static int ReadCount(string json)
        {
            try
            {
                JsonNode? count = JsonNode.Parse(json)?["count"];
                if (count == null)
                    return -1;

                return count.GetValue<int>();
            }
            catch (System.Text.Json.JsonException)
            {
                return -1;
            }
        }
    }
}
