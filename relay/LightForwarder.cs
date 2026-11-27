using System.Text;
using CamOnKeyLight.Core;

namespace CamOnKeyLight.Relay
{
    public readonly record struct ForwardResult(int StatusCode, string Body);

    public sealed class LightForwarder
    {
        private readonly HttpClient http;
        private readonly LightCatalog catalog;

        public LightForwarder(HttpClient http, LightCatalog catalog)
        {
            this.http = http;
            this.catalog = catalog;
        }

        public Task<ForwardResult> GetAsync(string address, CancellationToken cancellationToken)
        {
            return SendAsync(address, HttpMethod.Get, null, cancellationToken);
        }

        public Task<ForwardResult> PutAsync(string address, string body, CancellationToken cancellationToken)
        {
            return SendAsync(address, HttpMethod.Put, body, cancellationToken);
        }

        private async Task<ForwardResult> SendAsync(string address, HttpMethod method, string? body, CancellationToken cancellationToken)
        {
            if (!catalog.TryResolve(address, out string url))
                return new ForwardResult(404, "{\"error\":\"unknown light\"}");

            try
            {
                using HttpRequestMessage message = new HttpRequestMessage(method, url);
                if (body != null)
                    message.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using HttpResponseMessage response = await http.SendAsync(message, cancellationToken);
                string text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (text.Length > 16384)
                    text = text[..16384];

                return new ForwardResult((int)response.StatusCode, text);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                return new ForwardResult(0, "");
            }
        }
    }
}
