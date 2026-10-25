using System.Globalization;
using System.Net;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

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
