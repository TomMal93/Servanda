using System.Security.Cryptography;

namespace Servanda.Domain.Areas;

public static class EntityId
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewUlid(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        Span<byte> value = stackalloc byte[16];
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        if (timestamp is < 0 or > 0xFFFFFFFFFFFF)
        {
            throw new InvalidOperationException("Znacznik czasu nie mieści się w formacie ULID.");
        }

        for (var index = 5; index >= 0; index--)
        {
            value[index] = (byte)timestamp;
            timestamp >>= 8;
        }

        RandomNumberGenerator.Fill(value[6..]);

        Span<char> result = stackalloc char[26];
        for (var characterIndex = 0; characterIndex < result.Length; characterIndex++)
        {
            var encoded = 0;
            for (var bit = 0; bit < 5; bit++)
            {
                var sourceBit = (characterIndex * 5) + bit - 2;
                encoded <<= 1;
                if (sourceBit >= 0)
                {
                    encoded |= (value[sourceBit / 8] >> (7 - (sourceBit % 8))) & 1;
                }
            }

            result[characterIndex] = Alphabet[encoded];
        }

        return new string(result);
    }
}
