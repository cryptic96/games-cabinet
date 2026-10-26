using System.Net;
using System.Threading.RateLimiting;
using Cabinet.Service.Limits;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Limits;

/// <summary>
/// Verifies which requests on the live route count as a new connection: a negotiate in any spelling endpoint routing
/// accepts, and any request that carries no connection identifier; never a request that carries one, and nothing off the
/// live route. Verifies the windows count them per address and site-wide, and that a request refused by one window spends
/// nothing of the other.
/// </summary>
public class LiveConnectWindowTests
{
    [Theory]
    [InlineData("POST", "/cabinet/live/negotiate", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live/negotiate/", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live/negotiate//", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live/NEGOTIATE", "?negotiateVersion=1", true)]
    [InlineData("POST", "/Cabinet/Live/Negotiate/", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live//negotiate", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live//negotiate/", "", true)]
    [InlineData("GET", "/cabinet/live/negotiate/", "", true)]
    [InlineData("OPTIONS", "/cabinet/live/negotiate/", "", true)]
    [InlineData("POST", "/cabinet/live/negotiate/", "?id=abc", true)]
    [InlineData("GET", "/cabinet/live", "", true)]
    [InlineData("GET", "/cabinet/live/", "", true)]
    [InlineData("POST", "/cabinet/live", "", true)]
    [InlineData("DELETE", "/cabinet/live", "", true)]
    [InlineData("PUT", "/cabinet/live/", "", true)]
    [InlineData("GET", "/cabinet/live", "?id=abc", false)]
    [InlineData("GET", "/cabinet/live/", "?ID=abc", false)]
    [InlineData("POST", "/cabinet/live", "?id=abc", false)]
    [InlineData("DELETE", "/cabinet/live", "?id=abc", false)]
    [InlineData("GET", "/", "", false)]
    [InlineData("POST", "/negotiate", "", false)]
    [InlineData("GET", "/cabinet/livelier", "", false)]
    [InlineData("GET", "/cabinet", "", false)]
    public void A_request_counts_as_a_new_connection_by_what_it_asks_for_not_by_how_its_path_is_spelled(
        string method, string path, string query, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);

        LiveConnectWindow.CountsAsNewConnection(context.Request).Should().Be(expected);
    }

    [Fact]
    public void An_address_refused_by_the_site_keeps_its_own_budget_for_when_the_site_window_resets()
    {
        var clock = new FakeTimeProvider();
        using var limiter = LiveConnectWindow.CreateLimiter(Options(perAddress: 1, siteWide: 1, window: TimeSpan.FromSeconds(60)), clock);

        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue("the first request spends the site's one permit");
        clock.Advance(TimeSpan.FromSeconds(30));
        Acquire(limiter, "203.0.113.2").IsAcquired.Should().BeFalse("the site's budget is spent");
        Acquire(limiter, "203.0.113.2").IsAcquired.Should().BeFalse("the site's budget is still spent");
        clock.Advance(TimeSpan.FromSeconds(31));

        Acquire(limiter, "203.0.113.2").IsAcquired.Should().BeTrue("the refusals by the site spent nothing of the address's own window");
    }

    [Fact]
    public void An_address_refused_by_its_own_window_spends_nothing_of_the_site_budget()
    {
        var clock = new FakeTimeProvider();
        using var limiter = LiveConnectWindow.CreateLimiter(Options(perAddress: 1, siteWide: 2, window: TimeSpan.FromSeconds(60)), clock);

        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeFalse();
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeFalse();

        Acquire(limiter, "203.0.113.2").IsAcquired.Should().BeTrue("the refused requests took nothing from the site's budget");
    }

    [Fact]
    public void A_refusal_names_the_wait_until_the_window_that_refused_it_resets()
    {
        var clock = new FakeTimeProvider();
        using var limiter = LiveConnectWindow.CreateLimiter(Options(perAddress: 1, siteWide: 10, window: TimeSpan.FromSeconds(60)), clock);
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(20));

        var refused = Acquire(limiter, "203.0.113.1");

        refused.IsAcquired.Should().BeFalse();
        refused.TryGetMetadata(MetadataName.RetryAfter, out var wait).Should().BeTrue();
        wait.Should().Be(TimeSpan.FromSeconds(40));
    }

    [Fact]
    public void Requests_that_are_not_new_connections_spend_nothing_and_are_never_refused()
    {
        var clock = new FakeTimeProvider();
        using var limiter = LiveConnectWindow.CreateLimiter(Options(perAddress: 1, siteWide: 1, window: TimeSpan.FromSeconds(60)), clock);
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();

        for (var count = 0; count < 5; count++)
        {
            Acquire(limiter, "203.0.113.1", "/", string.Empty).IsAcquired.Should().BeTrue();
            Acquire(limiter, "203.0.113.1", "/cabinet/live", "?id=abc").IsAcquired.Should().BeTrue();
        }
    }

    [Fact]
    public void A_window_that_has_expired_admits_a_full_budget_again()
    {
        var clock = new FakeTimeProvider();
        using var limiter = LiveConnectWindow.CreateLimiter(Options(perAddress: 2, siteWide: 2, window: TimeSpan.FromSeconds(60)), clock);
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeFalse();

        clock.Advance(TimeSpan.FromSeconds(61));

        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeTrue();
        Acquire(limiter, "203.0.113.1").IsAcquired.Should().BeFalse();
    }

    private static ClientLimitOptions Options(int perAddress, int siteWide, TimeSpan window) =>
        new(5, TimeSpan.FromSeconds(60), 20, perAddress, window, siteWide);

    private static RateLimitLease Acquire(PartitionedRateLimiter<HttpContext> limiter, string address, string path = "/cabinet/live/negotiate", string query = "?negotiateVersion=1")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return limiter.AttemptAcquire(context);
    }
}
