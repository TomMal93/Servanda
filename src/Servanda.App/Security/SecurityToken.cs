using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Servanda.App.Security;

internal static class SecurityToken
{
    internal static string Create(int byteCount) =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    internal static string Fingerprint(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
