namespace UpgradePlanner.Api.Configuration;

/// <summary>
/// Every setting that differs between development, production and the public
/// demo, resolved once at startup.
/// </summary>
/// <remarks>
/// <see cref="Parse"/> is deliberately <b>pure and total</b>: it reads only the
/// <see cref="IConfiguration"/> handed to it, never throws, and turns any
/// unrecognised input into a documented default plus a warning. A typo in an
/// environment variable must degrade the app, never crash it at startup and
/// never silently select the wrong mode.
/// </remarks>
public sealed record AppOptions
{
    /// <summary>Used when <c>ALLOWED_ORIGINS</c> is absent — the local Vite dev server.</summary>
    public const string DefaultAllowedOrigin = "http://localhost:5176";

    /// <summary>The real, personal database. Demo mode must never open this file.</summary>
    public const string DefaultConnectionString = "Data Source=upgradeplanner.db";

    /// <summary>
    /// Demo mode's database, hard-coded rather than configurable. This is the
    /// namespace barrier that makes it structurally impossible for a demo seed
    /// to touch personal data: demo mode does not read the connection string at
    /// all, so no environment variable can point it at the real file.
    /// </summary>
    public const string DemoConnectionString = "Data Source=demo.db";

    /// <summary>
    /// When true the app serves disposable, generated fixture data and seeds
    /// itself on startup if its database is empty.
    /// </summary>
    public required bool DemoMode { get; init; }

    /// <summary>Browser origins permitted to call this API cross-origin.</summary>
    public required IReadOnlyList<string> AllowedOrigins { get; init; }

    /// <summary>
    /// The SQLite connection string actually used. In demo mode this is always
    /// <see cref="DemoConnectionString"/>, whatever configuration says.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>Commit the running build came from; <c>"unknown"</c> outside CI.</summary>
    public required string BuildSha { get; init; }

    public static AppOptions Parse(IConfiguration configuration, IList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(warnings);

        var demoMode = ParseBool(configuration["DEMO_MODE"], nameof(DemoMode), warnings);

        var configuredConnection = configuration.GetConnectionString("Default");
        var connectionString = demoMode
            ? DemoConnectionString
            : string.IsNullOrWhiteSpace(configuredConnection)
                ? DefaultConnectionString
                : configuredConnection;

        if (demoMode && !string.IsNullOrWhiteSpace(configuredConnection)
                     && configuredConnection != DemoConnectionString)
        {
            warnings.Add(
                "DEMO_MODE is on, so the configured connection string was ignored and "
                + $"'{DemoConnectionString}' used instead. Demo data never shares a database with real data.");
        }

        return new AppOptions
        {
            DemoMode = demoMode,
            AllowedOrigins = ParseOrigins(configuration["ALLOWED_ORIGINS"], warnings),
            ConnectionString = connectionString,
            BuildSha = string.IsNullOrWhiteSpace(configuration["BUILD_SHA"]) ? "unknown" : configuration["BUILD_SHA"]!,
        };
    }

    /// <summary>
    /// Accepts the spellings a human actually types. Anything else falls back to
    /// <c>false</c> with a warning, because every flag here is written so that
    /// <c>false</c> is the conservative direction.
    /// </summary>
    private static bool ParseBool(string? raw, string name, IList<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "true" or "1" or "yes" or "on":
                return true;
            case "false" or "0" or "no" or "off":
                return false;
            default:
                warnings.Add($"{name}: could not understand '{raw}'. Falling back to false.");
                return false;
        }
    }

    private static IReadOnlyList<string> ParseOrigins(string? raw, IList<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [DefaultAllowedOrigin];
        }

        var parsed = new List<string>();
        foreach (var candidate in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A trailing slash silently breaks CORS matching, so normalise it away
            // rather than letting it fail mysteriously in the browser.
            var trimmed = candidate.TrimEnd('/');

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                parsed.Add(trimmed);
            }
            else
            {
                warnings.Add($"ALLOWED_ORIGINS: ignoring '{candidate}' — not an absolute http(s) origin.");
            }
        }

        if (parsed.Count == 0)
        {
            warnings.Add($"ALLOWED_ORIGINS: no usable origin found. Falling back to {DefaultAllowedOrigin}.");
            return [DefaultAllowedOrigin];
        }

        return parsed;
    }
}
