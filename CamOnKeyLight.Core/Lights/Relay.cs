using System.Text;
using System.Text.Json;

namespace CamOnKeyLight.Core
{
    public enum LightRoute
    {
        Direct,
        Relay
    }

    public sealed class RelayCredentials
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string SharedSecret { get; set; } = "";

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(ClientId)
            && string.IsNullOrWhiteSpace(ClientSecret)
            && string.IsNullOrWhiteSpace(SharedSecret);

        public RelayCredentials Copy()
        {
            return new RelayCredentials
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                SharedSecret = SharedSecret
            };
        }
    }

    public interface ISecretProtector
    {
        byte[] Protect(byte[] plaintext);
        byte[] Unprotect(byte[] ciphertext);
    }

    public static class RelayUrls
    {
        public static bool IsConfigured(string? raw)
        {
            return TryNormalize(raw, out string url, out _) && url.Length > 0;
        }

        public static bool TryNormalize(string? raw, out string url, out string? error)
        {
            url = "";
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
                return true;

            string text = raw.Trim();
            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                error = "The relay address must start with https:// or http://.";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "The relay address cannot include a user name, a query, or a fragment.";
                return false;
            }

            string path = uri.AbsolutePath.TrimEnd('/');
            url = uri.GetLeftPart(UriPartial.Authority) + path;
            return true;
        }

        public static string Command(string baseUrl, string address, string action)
        {
            if (action == "ping")
                return baseUrl + "/v1/ping";

            string query = "address=" + Uri.EscapeDataString(address.Trim());
            if (action == "on")
                return baseUrl + "/v1/lights/on?" + query;

            if (action == "off")
                return baseUrl + "/v1/lights/off?" + query;

            return baseUrl + "/v1/lights?" + query;
        }
    }

    public static class RelayHeaders
    {
        public const string AccessClientId = "CF-Access-Client-Id";
        public const string AccessClientSecret = "CF-Access-Client-Secret";
        public const string SharedSecret = "X-Relay-Secret";

        public static void Apply(HttpRequestMessage message, RelayCredentials credentials)
        {
            Add(message, AccessClientId, credentials.ClientId);
            Add(message, AccessClientSecret, credentials.ClientSecret);
            Add(message, SharedSecret, credentials.SharedSecret);
        }

        private static void Add(HttpRequestMessage message, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (value.Contains('\r') || value.Contains('\n'))
                throw new ArgumentException("A credential contains a line break.");

            message.Headers.TryAddWithoutValidation(name, value);
        }
    }

    public static class RelaySecretFile
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static void Save(string path, RelayCredentials credentials, ISecretProtector protector)
        {
            if (credentials.IsEmpty)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(credentials, JsonOptions);
            byte[] protectedBytes = protector.Protect(json);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, protectedBytes);
            File.Move(temporary, path, overwrite: true);
        }

        public static RelayCredentials Load(string path, ISecretProtector protector)
        {
            if (!File.Exists(path))
                return new RelayCredentials();

            byte[] protectedBytes = File.ReadAllBytes(path);
            byte[] json = protector.Unprotect(protectedBytes);
            RelayCredentials? credentials = JsonSerializer.Deserialize<RelayCredentials>(json, JsonOptions);
            if (credentials == null)
                return new RelayCredentials();

            credentials.ClientId = (credentials.ClientId ?? "").Trim();
            credentials.ClientSecret ??= "";
            credentials.SharedSecret ??= "";
            return credentials;
        }
    }
}
