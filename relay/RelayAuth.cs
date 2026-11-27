using System.Security.Cryptography;
using System.Text;

namespace CamOnKeyLight.Relay
{
    public sealed class RelayAuth
    {
        private readonly byte[]? expectedHash;

        public RelayAuth(string? secret)
        {
            if (!string.IsNullOrEmpty(secret))
                expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }

        public bool Allows(HttpRequest request)
        {
            if (expectedHash == null)
                return true;

            string presented = request.Headers["X-Relay-Secret"].ToString();
            byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
            return CryptographicOperations.FixedTimeEquals(expectedHash, actual);
        }
    }
}
