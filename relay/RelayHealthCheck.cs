namespace CamOnKeyLight.Relay
{
    /// <summary>
    /// Resolves the in-container URL for GET /health. ASPNETCORE_URLS wins, matching
    /// Kestrel. HTTP_PORTS and ASPNETCORE_HTTP_PORTS are used only when no URL is set.
    /// Wildcard bind addresses are probed on 127.0.0.1.
    /// </summary>
    public static class RelayHealthCheck
    {
        public static bool TryResolve(string? urls, string? httpPorts, string? aspNetCoreHttpPorts, out Uri probe, out string? error)
        {
            probe = null!;
            error = null;
            if (!string.IsNullOrWhiteSpace(urls))
            {
                if (!TryFirstHttpUrl(urls, out string host, out int port, out error))
                    return false;

                probe = BuildProbe(host, port);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(httpPorts))
            {
                if (!TryFirstPort(httpPorts, out int fromHttpPorts, out error))
                    return false;

                probe = BuildProbe("127.0.0.1", fromHttpPorts);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(aspNetCoreHttpPorts))
            {
                if (!TryFirstPort(aspNetCoreHttpPorts, out int fromPrefixed, out error))
                    return false;

                probe = BuildProbe("127.0.0.1", fromPrefixed);
                return true;
            }

            probe = BuildProbe("127.0.0.1", 8080);
            return true;
        }

        public static async Task<int> ExecuteAsync(HttpClient? client = null, TextWriter? error = null, CancellationToken cancellationToken = default)
        {
            if (!TryResolve(
                    Environment.GetEnvironmentVariable("ASPNETCORE_URLS"),
                    Environment.GetEnvironmentVariable("HTTP_PORTS"),
                    Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS"),
                    out Uri probe,
                    out string? problem))
            {
                (error ?? Console.Error).WriteLine(problem);
                return 1;
            }

            bool ownClient = client == null;
            client ??= new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
            try
            {
                using HttpResponseMessage response = await client.GetAsync(probe, cancellationToken);
                if ((int)response.StatusCode is >= 200 and < 300)
                    return 0;

                (error ?? Console.Error).WriteLine("Health check " + probe + " returned HTTP " + (int)response.StatusCode + ".");
                return 1;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                (error ?? Console.Error).WriteLine("Health check " + probe + " did not answer.");
                return 1;
            }
            finally
            {
                if (ownClient)
                    client.Dispose();
            }
        }

        private static Uri BuildProbe(string host, int port)
        {
            string mapped = IsWildcard(host) ? "127.0.0.1" : host;
            string authority = mapped.Contains(':') ? "[" + mapped + "]:" + port : mapped + ":" + port;
            return new Uri("http://" + authority + "/health");
        }

        private static bool IsWildcard(string host)
        {
            return host is "" or "+" or "*" or "0.0.0.0" or "::";
        }

        private static bool TryFirstHttpUrl(string urls, out string host, out int port, out string? error)
        {
            host = "";
            port = 0;
            error = null;
            foreach (string part in urls.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!part.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryParseHttp(part, out host, out port, out error))
                    return false;

                error = null;
                return true;
            }

            error = "ASPNETCORE_URLS has no http address to probe.";
            return false;
        }

        private static bool TryParseHttp(string raw, out string host, out int port, out string? error)
        {
            host = "";
            port = 80;
            error = null;
            string rest = raw["http://".Length..];
            int slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest[..slash];

            if (rest.Length == 0)
            {
                error = "ASPNETCORE_URLS contains an empty http address.";
                return false;
            }

            if (rest[0] == '[')
            {
                int end = rest.IndexOf(']');
                if (end <= 1)
                {
                    error = "ASPNETCORE_URLS contains an invalid http address.";
                    return false;
                }

                host = rest[1..end];
                if (end == rest.Length - 1)
                    return true;

                if (rest[end + 1] != ':' || !TryPort(rest[(end + 2)..], out port, out error))
                {
                    error ??= "The health check port is not a number from 1 to 65535.";
                    return false;
                }

                return true;
            }

            int colon = rest.LastIndexOf(':');
            if (colon >= 0)
            {
                host = rest[..colon];
                return TryPort(rest[(colon + 1)..], out port, out error);
            }

            host = rest;
            return true;
        }

        private static bool TryFirstPort(string raw, out int port, out string? error)
        {
            port = 0;
            error = null;
            foreach (string part in raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                return TryPort(part, out port, out error);

            error = "The health check port is empty.";
            return false;
        }

        private static bool TryPort(string text, out int port, out string? error)
        {
            port = 0;
            error = null;
            if (!int.TryParse(text, out port) || port is < 1 or > 65535)
            {
                error = "The health check port is not a number from 1 to 65535.";
                return false;
            }

            return true;
        }
    }
}
