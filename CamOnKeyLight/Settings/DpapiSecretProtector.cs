using System.Security.Cryptography;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    internal sealed class DpapiSecretProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext)
        {
            return ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        }

        public byte[] Unprotect(byte[] ciphertext)
        {
            return ProtectedData.Unprotect(ciphertext, null, DataProtectionScope.CurrentUser);
        }
    }
}
