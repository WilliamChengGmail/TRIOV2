using System.Security.Cryptography;
using System.Text;

namespace PerformanceAnalysis.Core.Metadata;

public static class SessionKey
{
    /// <summary>
    /// Stable key computed from the system CSV contents. Path and folder name are intentionally excluded,
    /// so copied/moved logs still match the same metadata file.
    /// </summary>
    public static string Compute(DateTime firstTimestamp, DateTime lastTimestamp, int sampleCount, long totalLength)
    {
        var raw = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{firstTimestamp:yyyy-MM-dd HH:mm:ss}|{lastTimestamp:yyyy-MM-dd HH:mm:ss}|{sampleCount}|{totalLength}");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
