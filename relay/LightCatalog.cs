using CamOnKeyLight.Core;

namespace CamOnKeyLight.Relay
{
    public sealed class LightCatalog
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, string> urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static LightCatalog Parse(string? raw)
        {
            LightCatalog catalog = new LightCatalog();
            if (string.IsNullOrWhiteSpace(raw))
                return catalog;

            foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                catalog.TryAdd(part);

            return catalog;
        }

        public int Count
        {
            get
            {
                lock (gate)
                    return urls.Count;
            }
        }

        public bool TryResolve(string? address, out string url)
        {
            url = "";
            if (!TryKey(address, out string key))
                return false;

            lock (gate)
                return urls.TryGetValue(key, out url!);
        }

        public bool TryAdd(string? address)
        {
            if (!TryKey(address, out string key) || !LightAddressRules.TryCreateUrl(address, out string url, out _))
                return false;

            lock (gate)
                urls[key] = url;

            return true;
        }

        private static bool TryKey(string? address, out string key)
        {
            key = "";
            if (!LightAddressRules.TryParse(address, out string host, out int port, out _))
                return false;

            key = host + ":" + port;
            return true;
        }
    }
}
