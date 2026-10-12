using System;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Sanctuary.WebAPI.Helpers;

/// <summary>
/// Verifies the HMAC-SHA256 launcher token sent by official Clover
/// launchers (and trusted server-to-server callers, like the website's
/// own backend) on /login and /register, so a bare script calling these
/// endpoints directly can't get back a usable session.
///
/// Scheme: the caller sends X-Clover-Launcher-Timestamp (unix seconds)
/// and X-Clover-Launcher-Signature (hex HMAC-SHA256 of the timestamp,
/// keyed with the shared secret). The server accepts anything within a
/// 60-second window of its own clock and recomputes the signature itself
/// rather than trusting anything the caller computed beyond that.
/// </summary>
public static class LauncherTokenValidator
{
    private const int MaxClockSkewSeconds = 60;

    public static bool IsValid(HttpContext context, string? sharedSecret, ILogger logger)
    {
        if (string.IsNullOrEmpty(sharedSecret))
        {
            logger.LogWarning("LauncherSharedSecret is not configured - launcher token check is disabled.");
            return true;
        }

        if (!context.Request.Headers.TryGetValue("X-Clover-Launcher-Timestamp", out var timestampHeader)
            || !long.TryParse(timestampHeader, out var timestamp))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (Math.Abs(now - timestamp) > MaxClockSkewSeconds)
            return false;

        if (!context.Request.Headers.TryGetValue("X-Clover-Launcher-Signature", out var signatureHeader))
            return false;

        byte[] providedSignature;

        try
        {
            providedSignature = Convert.FromHexString(signatureHeader.ToString());
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
        var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp.ToString()));

        return providedSignature.Length == expectedSignature.Length
            && CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature);
    }
}
