using System.Security.Cryptography;
using System.Text;
using UrlShortener.Domain.Services;
using UrlShortener.Infrastructure.Telemetry;

namespace UrlShortener.Infrastructure.Services;

public sealed class Sha256Base62Generator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly int Base = Alphabet.Length;

    public string Generate(string ownerId, string url, string nonce, int counter, int length = 6)
    {
        using var activity = ActivitySources.ShortCodeGenerator.StartActivity(nameof(Generate));

        string payload = $"{ownerId}\0{url}\0{nonce}\0{counter}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        byte[] hash = SHA256.HashData(payloadBytes);

        ulong value = BitConverter.ToUInt64(hash, 0);

        Span<char> buffer = stackalloc char[length];

        for (int i = length - 1; i >= 0; i--)
        {
            buffer[i] = Alphabet[(int)(value % (ulong)Base)];
            value /= (ulong)Base;
        }

        return new string(buffer);
    }
}
