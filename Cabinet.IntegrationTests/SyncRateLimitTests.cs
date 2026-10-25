using System.Globalization;
using System.Net;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a single client that presses the sync button too often is refused in the answer the page already explains, that
/// the refusal is counted per client address, and that no other route is touched by the limit.
/// </summary>
[Trait("Category", "Sync")]
public class SyncRateLimitTests
{
    [Fact]
    public async Task A_press_over_the_client_limit_is_refused_like_the_shared_window_without_a_status()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), Limits(presses: 1, windowSeconds: 60));
        using var client = factory.CreatePublicClient();

        using var first = await Press(client, forwardedFor: null);
        using var firstBody = await ReadBody(first);
        using var second = await Press(client, forwardedFor: null);
        using var secondBody = await ReadBody(second);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        firstBody.RootElement.GetProperty("outcome").GetString().Should().Be("started");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        secondBody.RootElement.GetProperty("outcome").GetString().Should().Be("cooldown");
        secondBody.RootElement.TryGetProperty("status", out _).Should().BeFalse();
        second.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
        second.Headers.CacheControl!.NoStore.Should().BeTrue();
        await SyncHarness.WaitForRunToEnd(client);
    }

    [Fact]
    public async Task Presses_up_to_the_limit_reach_the_handler_and_the_next_one_is_refused_by_the_client_limit()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), Limits(presses: 2, windowSeconds: 60));
        using var client = factory.CreatePublicClient();

        using var first = await Press(client, forwardedFor: null);
        using var second = await Press(client, forwardedFor: null);
        using var secondBody = await ReadBody(second);
        using var third = await Press(client, forwardedFor: null);
        using var thirdBody = await ReadBody(third);

        ReachedHandler(second, secondBody).Should().BeTrue("the second press is inside the budget, so the handler answers it");
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        thirdBody.RootElement.TryGetProperty("status", out _).Should().BeFalse();
        await SyncHarness.WaitForRunToEnd(client);
    }

    [Fact]
    public async Task The_status_route_is_never_refused_by_the_press_limit()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), Limits(presses: 1, windowSeconds: 60));
        using var client = factory.CreatePublicClient();
        using var first = await Press(client, forwardedFor: null);
        using var second = await Press(client, forwardedFor: null);

        for (var read = 0; read < 5; read++)
        {
            using var status = await client.GetAsync(SyncHarness.StatusRoute, TestContext.Current.CancellationToken);

            status.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await SyncHarness.WaitForRunToEnd(client);
    }

    [Fact]
    public async Task Two_clients_forwarded_by_the_known_proxy_have_separate_budgets()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), Limits(presses: 1, windowSeconds: 60));
        using var client = factory.CreatePublicClient();

        using var spent = await Press(client, forwardedFor: "203.0.113.7");
        using var another = await Press(client, forwardedFor: "203.0.113.8");
        using var anotherBody = await ReadBody(another);
        using var again = await Press(client, forwardedFor: "203.0.113.7");
        using var againBody = await ReadBody(again);

        ReachedHandler(another, anotherBody).Should().BeTrue("a different client address has a budget of its own");
        again.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        againBody.RootElement.TryGetProperty("status", out _).Should().BeFalse();
        await SyncHarness.WaitForRunToEnd(client);
    }

    [Fact]
    public async Task A_peer_that_is_not_the_known_proxy_cannot_move_its_presses_into_another_budget()
    {
        await using var host = await UntrustedPeerHost.StartAsync("192.0.2.10", "192.0.2.0/24");

        var first = await host.PostLimitedAsync("198.51.100.20", "203.0.113.7");
        var forged = await host.PostLimitedAsync("198.51.100.20", "203.0.113.9");
        var proxied = await host.PostLimitedAsync("192.0.2.10", "203.0.113.7");
        var proxiedOther = await host.PostLimitedAsync("192.0.2.10", "203.0.113.8");

        first.Should().Be(StatusCodes.Status200OK);
        forged.Should().Be(StatusCodes.Status429TooManyRequests, "both requests count under the real peer");
        proxied.Should().Be(StatusCodes.Status200OK);
        proxiedOther.Should().Be(StatusCodes.Status200OK, "the known proxy's forwarded clients are counted apart");
    }

    [Fact]
    public async Task Addresses_inside_one_ipv6_prefix_share_a_budget_and_another_prefix_has_its_own()
    {
        await using var host = await UntrustedPeerHost.StartAsync("192.0.2.10", "192.0.2.0/24");

        var first = await host.PostLimitedAsync("2001:db8:1:2::1", forwardedFor: null);
        var sameBlock = await host.PostLimitedAsync("2001:db8:1:2::ffff", forwardedFor: null);
        var otherBlock = await host.PostLimitedAsync("2001:db8:1:3::1", forwardedFor: null);

        first.Should().Be(StatusCodes.Status200OK);
        sameBlock.Should().Be(StatusCodes.Status429TooManyRequests);
        otherBlock.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task A_press_is_let_through_again_once_the_window_has_passed()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), Limits(presses: 1, windowSeconds: 1));
        using var client = factory.CreatePublicClient();
        using var first = await Press(client, forwardedFor: null);
        using var refused = await Press(client, forwardedFor: null);
        using var refusedBody = await ReadBody(refused);
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refusedBody.RootElement.TryGetProperty("status", out _).Should().BeFalse();

        await SyncHarness.WaitUntil(
            async () =>
            {
                using var press = await Press(client, forwardedFor: null);
                using var body = await ReadBody(press);

                return ReachedHandler(press, body);
            },
            TimeSpan.FromSeconds(5));

        await SyncHarness.WaitForRunToEnd(client);
    }

    private static Dictionary<string, string?> Limits(int presses, int windowSeconds) =>
        new()
        {
            ["ClientLimits:SyncPressesPerWindow"] = presses.ToString(CultureInfo.InvariantCulture),
            ["ClientLimits:SyncWindowSeconds"] = windowSeconds.ToString(CultureInfo.InvariantCulture),
        };

    private static bool ReachedHandler(HttpResponseMessage response, JsonDocument body) =>
        response.StatusCode == HttpStatusCode.Conflict
        || response.StatusCode == HttpStatusCode.Accepted
        || (response.StatusCode == HttpStatusCode.TooManyRequests && body.RootElement.TryGetProperty("status", out _));

    private static async Task<HttpResponseMessage> Press(HttpClient client, string? forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SyncHarness.SyncRoute);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadBody(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
