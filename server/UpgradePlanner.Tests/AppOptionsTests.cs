using Microsoft.Extensions.Configuration;
using UpgradePlanner.Api.Configuration;

namespace UpgradePlanner.Tests;

/// <summary>
/// Configuration parsing: it must be total, and a typo must never select the
/// wrong mode.
/// </summary>
/// <remarks>
/// Demo mode is the destructive direction — it is the mode that seeds and, via
/// the reset flag, deletes. So <c>false</c> is the safe default, and every
/// unrecognised input has to land there rather than anywhere else.
/// </remarks>
public class AppOptionsTests
{
    private static AppOptions Parse(Dictionary<string, string?> settings, out List<string> warnings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        warnings = [];
        return AppOptions.Parse(configuration, warnings);
    }

    private static AppOptions Parse(Dictionary<string, string?> settings) => Parse(settings, out _);

    // --- Demo mode ------------------------------------------------------------

    [Fact]
    public void Demo_mode_is_off_when_nothing_is_configured()
    {
        Assert.False(Parse([]).DemoMode);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData(" true ")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    public void Demo_mode_understands_the_spellings_people_actually_type(string raw)
    {
        Assert.True(Parse(new() { ["DEMO_MODE"] = raw }).DemoMode);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("off")]
    [InlineData("")]
    public void Demo_mode_understands_the_negative_spellings_too(string raw)
    {
        Assert.False(Parse(new() { ["DEMO_MODE"] = raw }).DemoMode);
    }

    [Theory]
    [InlineData("ture")]     // transposition
    [InlineData("treu")]
    [InlineData("enabled")]  // plausible but wrong
    [InlineData("y")]
    [InlineData("maybe")]
    [InlineData("!@#$%")]
    public void A_typo_falls_back_to_off_and_says_so(string raw)
    {
        var options = Parse(new() { ["DEMO_MODE"] = raw }, out var warnings);

        Assert.False(options.DemoMode);
        Assert.Contains(warnings, w => w.Contains("DemoMode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_recognised_value_produces_no_warning()
    {
        Parse(new() { ["DEMO_MODE"] = "true" }, out var warnings);

        Assert.Empty(warnings);
    }

    // --- The namespace barrier ------------------------------------------------

    [Fact]
    public void Demo_mode_uses_its_own_database_whatever_configuration_says()
    {
        // The barrier that makes it structurally impossible for a demo seed to
        // touch real data: demo mode does not read the configured connection
        // string, so no environment variable can aim it at the personal database.
        var options = Parse(new()
        {
            ["DEMO_MODE"] = "true",
            ["ConnectionStrings:Default"] = "Data Source=upgradeplanner.db",
        });

        Assert.Equal(AppOptions.DemoConnectionString, options.ConnectionString);
        Assert.DoesNotContain("upgradeplanner.db", options.ConnectionString);
    }

    [Fact]
    public void Overriding_the_connection_string_in_demo_mode_produces_a_warning()
    {
        Parse(new()
        {
            ["DEMO_MODE"] = "true",
            ["ConnectionStrings:Default"] = "Data Source=somewhere-else.db",
        }, out var warnings);

        Assert.Contains(warnings, w => w.Contains("DEMO_MODE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Outside_demo_mode_the_configured_connection_string_is_honoured()
    {
        var options = Parse(new()
        {
            ["ConnectionStrings:Default"] = "Data Source=/var/data/mine.db",
        });

        Assert.Equal("Data Source=/var/data/mine.db", options.ConnectionString);
    }

    [Fact]
    public void An_absent_connection_string_falls_back_to_the_documented_default()
    {
        Assert.Equal(AppOptions.DefaultConnectionString, Parse([]).ConnectionString);
    }

    // --- Origins ---------------------------------------------------------------

    [Fact]
    public void Origins_default_to_the_local_dev_server()
    {
        Assert.Equal([AppOptions.DefaultAllowedOrigin], Parse([]).AllowedOrigins);
    }

    [Fact]
    public void Multiple_origins_are_split_and_trimmed()
    {
        var options = Parse(new()
        {
            ["ALLOWED_ORIGINS"] = "https://a.example.com , https://b.example.com",
        });

        Assert.Equal(["https://a.example.com", "https://b.example.com"], options.AllowedOrigins);
    }

    [Fact]
    public void A_trailing_slash_is_normalised_away()
    {
        // A trailing slash silently breaks CORS matching in the browser, which is
        // a miserable thing to debug from a deployed frontend.
        var options = Parse(new() { ["ALLOWED_ORIGINS"] = "https://a.example.com/" });

        Assert.Equal(["https://a.example.com"], options.AllowedOrigins);
    }

    [Fact]
    public void A_malformed_origin_is_dropped_with_a_warning_and_the_rest_survive()
    {
        var options = Parse(new()
        {
            ["ALLOWED_ORIGINS"] = "https://good.example.com,not-a-url,ftp://wrong.example.com",
        }, out var warnings);

        Assert.Equal(["https://good.example.com"], options.AllowedOrigins);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void An_entirely_malformed_origin_list_falls_back_to_the_default()
    {
        var options = Parse(new() { ["ALLOWED_ORIGINS"] = "nonsense,,,more nonsense" }, out var warnings);

        Assert.Equal([AppOptions.DefaultAllowedOrigin], options.AllowedOrigins);
        Assert.NotEmpty(warnings);
    }

    // --- Build metadata --------------------------------------------------------

    [Fact]
    public void Build_sha_is_unknown_when_nothing_injected_it()
    {
        Assert.Equal("unknown", Parse([]).BuildSha);
    }

    [Fact]
    public void Build_sha_is_carried_through_when_ci_injects_it()
    {
        Assert.Equal("abc1234", Parse(new() { ["BUILD_SHA"] = "abc1234" }).BuildSha);
    }

    // --- Totality --------------------------------------------------------------

    [Fact]
    public void Parsing_never_throws_however_bad_the_input()
    {
        var hostile = new Dictionary<string, string?>
        {
            ["DEMO_MODE"] = new string('x', 10_000),
            ["ALLOWED_ORIGINS"] = ",,,   ,,,\t\n",
            ["ConnectionStrings:Default"] = "\0\0\0",
            ["BUILD_SHA"] = "   ",
        };

        var exception = Record.Exception(() => Parse(hostile));

        Assert.Null(exception);
    }

    [Fact]
    public void An_empty_configuration_produces_a_usable_result()
    {
        var options = Parse([]);

        Assert.False(options.DemoMode);
        Assert.NotEmpty(options.AllowedOrigins);
        Assert.NotEmpty(options.ConnectionString);
        Assert.NotEmpty(options.BuildSha);
    }
}
