using UpgradePlanner.Api.Configuration;

namespace UpgradePlanner.Tests;

/// <summary>
/// Rate-limit partitioning.
/// </summary>
/// <remarks>
/// These exist because of a production bug. The original code used the entire
/// <c>X-Forwarded-For</c> header as the partition key. Locally there is no proxy,
/// so the header is absent, the socket address is used, and rate limiting worked
/// perfectly — 130 rapid requests produced 21 rejections. Behind a real edge the
/// header is a chain containing a hop that changes between requests, so every
/// request got its own partition and the limiter never fired: 200 concurrent
/// requests against the deployed API returned 200 every time.
///
/// The lesson these tests encode is that the key must be <b>stable for one
/// client</b>, which is exactly the property a unit test can pin down and a local
/// smoke test cannot.
/// </remarks>
public class ClientKeyTests
{
    [Fact]
    public void The_originating_client_is_the_leftmost_entry_of_the_chain()
    {
        Assert.Equal("203.0.113.7", ClientKey.FromForwardedFor("203.0.113.7, 198.51.100.2, 198.51.100.9"));
    }

    [Fact]
    public void A_single_address_is_returned_as_is()
    {
        Assert.Equal("203.0.113.7", ClientKey.FromForwardedFor("203.0.113.7"));
    }

    [Fact]
    public void Whitespace_around_entries_is_trimmed()
    {
        Assert.Equal("203.0.113.7", ClientKey.FromForwardedFor("  203.0.113.7 ,  198.51.100.2 "));
    }

    [Fact]
    public void The_key_is_stable_when_only_the_proxy_hops_change()
    {
        // This is the whole point. Render's edge varies a hop per request; if that
        // changed the key, every request would land in its own bucket and the
        // limiter would silently do nothing.
        var first = ClientKey.Resolve("203.0.113.7, 198.51.100.2", "10.0.0.1");
        var second = ClientKey.Resolve("203.0.113.7, 198.51.100.99", "10.0.0.1");
        var third = ClientKey.Resolve("203.0.113.7, 10.2.3.4, 172.16.0.9", "10.0.0.1");

        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    [Fact]
    public void Different_clients_get_different_keys()
    {
        Assert.NotEqual(
            ClientKey.Resolve("203.0.113.7, 198.51.100.2", null),
            ClientKey.Resolve("203.0.113.8, 198.51.100.2", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public void An_absent_or_empty_header_yields_no_client(string? header)
    {
        Assert.Null(ClientKey.FromForwardedFor(header));
    }

    [Fact]
    public void Without_a_forwarded_header_the_socket_address_is_used()
    {
        Assert.Equal("10.0.0.1", ClientKey.Resolve(null, "10.0.0.1"));
    }

    [Fact]
    public void With_nothing_identifiable_everything_shares_one_bucket()
    {
        // Conservative on purpose: unidentifiable requests are counted together
        // rather than each being handed its own unlimited budget.
        Assert.Equal("unknown", ClientKey.Resolve(null, null));
        Assert.Equal("unknown", ClientKey.Resolve("  ", "  "));
    }

    [Fact]
    public void Resolving_never_throws_on_malformed_input()
    {
        var exception = Record.Exception(() => ClientKey.Resolve(new string(',', 5_000), null));

        Assert.Null(exception);
    }
}
