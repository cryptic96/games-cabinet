using System.Globalization;
using System.Net;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a press on the sync button opens one window shared by every visitor, that the window survives a restart, and
/// that nothing a visitor sends changes what BGG is asked.
/// </summary>
[Trait("Category", "Sync")]
public class SyncNowTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task The_first_press_is_accepted_and_reports_the_sync_as_running_or_already_finished()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        using var body = await ReadBody(press);

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        body.RootElement.GetProperty("outcome").GetString().Should().Be("started");
        var status = body.RootElement.GetProperty("status");
        var running = status.GetProperty("running").GetBoolean();
        var finished = status.GetProperty("lastResult").ValueKind != JsonValueKind.Null;

        (running || finished).Should().BeTrue("the answer reports the state as it is, and a sync that has already ended is not reported as running");
        await SyncHarness.WaitForRunToEnd(client);
    }

    [Fact]
    public async Task A_second_press_inside_the_window_is_refused_with_the_remaining_seconds()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        using var first = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitForRunToEnd(client);

        using var second = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        using var body = await ReadBody(second);

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter!.Delta.Should().Be(Cooldown);
        body.RootElement.GetProperty("outcome").GetString().Should().Be("cooldown");
        body.RootElement.GetProperty("status").GetProperty("cooldownEndsUtc").GetDateTimeOffset()
            .Should().Be(SyncHarness.StartTime + Cooldown);
        handler.CollectionRequests().Should().HaveCount(2);
        handler.ThingRequests().Should().ContainSingle();
    }

    [Fact]
    public async Task The_window_survives_a_restart()
    {
        using var storage = new TemporaryDirectory();
        var settings = new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath };

        await using (var first = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3)), SyncHarness.NewClock(), settings))
        {
            using var client = first.CreatePublicClient();
            using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
            await SyncHarness.WaitForRunToEnd(client);
        }

        var handler = new ScriptedBggHandler();
        await using var second = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), settings);
        using var restarted = second.CreatePublicClient();
        using var again = await restarted.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(storage.FullPath, "sync-state.json")).Should().BeTrue();
        again.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_press_after_the_window_is_accepted()
    {
        var clock = SyncHarness.NewClock();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, clock);
        using var client = factory.CreatePublicClient();
        using var first = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitForRunToEnd(client);

        clock.Advance(Cooldown);
        using var later = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        later.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await SyncHarness.WaitUntil(() => Task.FromResult(handler.CollectionRequests().Count == 4));
    }

    [Fact]
    public async Task A_failed_sync_still_uses_up_the_window()
    {
        var handler = SyncHarness.Refusing();
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        using var first = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitForRunToEnd(client);

        using var second = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("failed");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_press_with_a_query_and_a_body_asks_BGG_only_for_the_configured_username()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "other-person", ["own"] = "0" });

        using var press = await client.PostAsync($"{SyncHarness.SyncRoute}?username=other-person&own=0", form, TestContext.Current.CancellationToken);
        await SyncHarness.WaitForRunToEnd(client);

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        handler.CollectionRequests().Should().NotBeEmpty();
        handler.CollectionRequests().Should().OnlyContain(request =>
            request.Uri.Query.Contains("username=sentinel-user-name") && !request.Uri.Query.Contains("other-person"));
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(10.4, 590)]
    [InlineData(195, 405)]
    [InlineData(599, 1)]
    [InlineData(599.6, 1)]
    public async Task A_refused_press_is_told_to_wait_the_remaining_time_in_whole_seconds_rounded_up(double secondsIntoWindow, int expectedSeconds)
    {
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3)), clock);
        using var client = factory.CreatePublicClient();
        using var first = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitForRunToEnd(client);

        clock.Advance(TimeSpan.FromSeconds(secondsIntoWindow));
        using var refused = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.NonValidated["Retry-After"].ToString()
            .Should().Be(expectedSeconds.ToString(CultureInfo.InvariantCulture), "the header carries the raw whole number of seconds");
    }

    private static async Task<JsonDocument> ReadBody(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
