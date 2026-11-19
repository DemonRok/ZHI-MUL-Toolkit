using System.Security.Cryptography;

namespace ZHIMulToolkit.Core.Updates;

public static class Sha256Checksum
{
    public static string? ReadExpectedHash(string manifest, string fileName)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        foreach (var line in manifest.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0].Length != 64 || !parts[0].All(Uri.IsHexDigit))
            {
                continue;
            }

            var listedName = Path.GetFileName(parts[1].TrimStart('*'));
            if (string.Equals(listedName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0].ToUpperInvariant();
            }
        }

        return null;
    }

    public static async Task<bool> VerifyFileAsync(string filePath, string expectedHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);
        if (expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit)) return false;

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(actualHash, Convert.FromHexString(expectedHash));
    }
}
