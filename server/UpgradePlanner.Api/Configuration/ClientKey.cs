namespace UpgradePlanner.Api.Configuration;

/// <summary>
/// Works out which client a request came from, for rate-limit partitioning.
/// </summary>
/// <remarks>
/// <para>
/// Pure and total so it can be tested without an HTTP context — which matters,
/// because the first version of this logic was wrong in a way that was invisible
/// locally and silently disabled rate limiting in production.
/// </para>
/// <para>
/// <b>This is a fairness control, not a security control.</b> <c>X-Forwarded-For</c>
/// is client-supplied and trivially spoofed, so a determined caller can evade the
/// limit by varying it. It bounds accidental and casual load on a disposable demo;
/// it is not an access control and is not presented as one in
/// <c>docs/SECURITY.md</c>.
/// </para>
/// </remarks>
public static class ClientKey
{
    /// <summary>
    /// The originating client from an <c>X-Forwarded-For</c> value, or
    /// <see langword="null"/> if there isn't one.
    /// </summary>
    /// <remarks>
    /// The header is a chain: <c>client, proxy1, proxy2</c>, appended to by each
    /// hop. The originating client is the <b>leftmost</b> entry.
    /// <para>
    /// Taking the whole string instead is the bug this method exists to prevent.
    /// Locally there is no proxy, so the header is absent and the socket address
    /// is used — everything works. Behind a real edge the chain includes a hop
    /// that changes between requests, so every request produced a different
    /// partition key and the limiter never fired. Verified in production: 200
    /// concurrent requests, zero 429s.
    /// </para>
    /// </remarks>
    public static string? FromForwardedFor(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return null;

        foreach (var candidate in headerValue.Split(','))
        {
            var trimmed = candidate.Trim();
            if (trimmed.Length > 0) return trimmed;
        }

        return null;
    }

    /// <summary>
    /// The partition key for a request: the forwarded client when present,
    /// otherwise the socket address, otherwise a shared bucket.
    /// </summary>
    /// <remarks>
    /// Falling back to one shared <c>"unknown"</c> bucket is deliberate. If the
    /// client cannot be identified, the conservative behaviour is to count those
    /// requests together rather than to hand each one its own unlimited budget.
    /// </remarks>
    public static string Resolve(string? forwardedFor, string? remoteIpAddress)
        => FromForwardedFor(forwardedFor)
           ?? (string.IsNullOrWhiteSpace(remoteIpAddress) ? "unknown" : remoteIpAddress);
}
