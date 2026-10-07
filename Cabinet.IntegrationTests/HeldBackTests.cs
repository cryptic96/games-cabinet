using System.Net;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves an empty or more-than-halved answer never replaces the shown cabinet until it is confirmed, that an empty one is
/// never confirmed, and that a first sync of an empty collection is a normal one.
/// </summary>
[Trait("Category", "Sync")]
public class HeldBackTests
{
    private const string BeingFilled = "The cabinet is being filled.";

    private static readonly IReadOnlyList<FakeBggItem> Full = SyntheticBggCollection.Create(65);

    [Fact]
    public async Task An_empty_answer_over_shown_games_is_held_back_every_time_and_a_full_answer_clears_it()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        var source = new SyncRounds.SwitchableBgg(Full);
        await using var factory = CreateFactory(source, clock, storage);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var shown = await SyncRounds.ReadLayout(client);
        var stored = await File.ReadAllBytesAsync(SnapshotPath(storage), TestContext.Current.CancellationToken);
        var syncedAt = (await SyncHarness.ReadStatus(client)).Json.GetProperty("lastSyncedUtc").GetDateTimeOffset();
        source.Serve([]);

        await SyncRounds.PressAndWait(client, clock);
        var afterFirstEmpty = await SyncHarness.ReadStatus(client);
        await SyncRounds.PressAndWait(client, clock);
        var afterSecondEmpty = await SyncHarness.ReadStatus(client);

        foreach (var status in new[] { afterFirstEmpty, afterSecondEmpty })
        {
            status.LastResult.Should().Be("heldBack");
            status.Json.GetProperty("heldBack").GetBoolean().Should().BeTrue();
            status.Json.GetProperty("lastSyncedUtc").GetDateTimeOffset().Should().Be(syncedAt);
            status.Body.Should().NotContainEquivalentOf("fingerprint").And.NotContainEquivalentOf("count");
        }

        (await SyncRounds.ReadLayout(client)).Should().BeEquivalentTo(shown);
        (await File.ReadAllBytesAsync(SnapshotPath(storage), TestContext.Current.CancellationToken)).Should().Equal(stored);
        (await File.ReadAllTextAsync(StatePath(storage), TestContext.Current.CancellationToken)).Should()
            .Contain(ShrinkGuard.Fingerprint([])).And.Contain("\"kind\":\"empty\"");

        source.Serve(Full);
        await SyncRounds.PressAndWait(client, clock);
        var recovered = await SyncHarness.ReadStatus(client);

        recovered.LastResult.Should().Be("unchanged");
        recovered.Json.GetProperty("heldBack").GetBoolean().Should().BeFalse();
        (await File.ReadAllTextAsync(StatePath(storage), TestContext.Current.CancellationToken)).Should().Contain("\"heldBack\":null");
    }

    [Fact]
    public async Task A_quarter_of_the_collection_is_held_back_and_accepted_when_the_next_sync_returns_the_same_entries()
    {
        var clock = SyncHarness.NewClock();
        var quarter = Quarter(0);
        var source = new SyncRounds.SwitchableBgg(Full);
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var shown = await SyncRounds.ReadLayout(client);
        source.Serve(quarter);

        await SyncRounds.PressAndWait(client, clock);
        var held = await SyncHarness.ReadStatus(client);
        var afterHold = await SyncRounds.ReadLayout(client);
        await SyncRounds.PressAndWait(client, clock);
        var accepted = await SyncHarness.ReadStatus(client);
        var redrawn = await SyncRounds.ReadLayout(client);

        held.LastResult.Should().Be("heldBack");
        held.Json.GetProperty("heldBack").GetBoolean().Should().BeTrue();
        afterHold.Should().BeEquivalentTo(shown);
        accepted.LastResult.Should().Be("changed");
        accepted.Json.GetProperty("heldBack").GetBoolean().Should().BeFalse();
        redrawn.ETag.Should().NotBe(shown.ETag);
        redrawn.Titles.Should().BeEquivalentTo(quarter.Select(item => item.Title));
    }

    [Fact]
    public async Task A_different_suspicious_set_replaces_the_held_back_record_and_needs_its_own_confirmation()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        var first = Quarter(0);
        var second = Quarter(first.Count);
        var source = new SyncRounds.SwitchableBgg(Full);
        await using var factory = CreateFactory(source, clock, storage);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var shown = await SyncRounds.ReadLayout(client);

        source.Serve(first);
        await SyncRounds.PressAndWait(client, clock);
        source.Serve(second);
        await SyncRounds.PressAndWait(client, clock);
        var afterOther = await SyncHarness.ReadStatus(client);
        var recorded = await File.ReadAllTextAsync(StatePath(storage), TestContext.Current.CancellationToken);

        afterOther.LastResult.Should().Be("heldBack");
        recorded.Should().Contain(ShrinkGuard.Fingerprint(second.Select(item => item.CollId)))
            .And.NotContain(ShrinkGuard.Fingerprint(first.Select(item => item.CollId)));
        (await SyncRounds.ReadLayout(client)).Should().BeEquivalentTo(shown);

        await SyncRounds.PressAndWait(client, clock);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
        (await SyncRounds.ReadLayout(client)).Titles.Should().BeEquivalentTo(second.Select(item => item.Title));
    }

    [Fact]
    public async Task A_failure_after_a_held_back_result_keeps_the_record_so_the_next_good_answer_still_confirms_it()
    {
        var clock = SyncHarness.NewClock();
        var quarter = Quarter(0);
        var source = new SyncRounds.SwitchableBgg(Full);
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        source.Serve(quarter);
        await SyncRounds.PressAndWait(client, clock);
        source.Fail(ScriptedResponse.Empty(HttpStatusCode.InternalServerError));

        await SyncRounds.PressAndWait(client, clock);
        var failed = await SyncHarness.ReadStatus(client);
        source.Serve(quarter);
        await SyncRounds.PressAndWait(client, clock);

        failed.LastResult.Should().Be("failed");
        failed.Json.GetProperty("heldBack").GetBoolean().Should().BeTrue();
        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
    }

    [Fact]
    public async Task The_confirmation_survives_a_restart()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        var quarter = Quarter(0);
        var source = new SyncRounds.SwitchableBgg(Full);

        await using (var first = CreateFactory(source, clock, storage))
        {
            using var client = first.CreatePublicClient();
            await SyncRounds.PressAndWait(client, clock, advance: false);
            source.Serve(quarter);
            await SyncRounds.PressAndWait(client, clock);
        }

        await using var second = CreateFactory(source, clock, storage);
        using var restarted = second.CreatePublicClient();
        await SyncRounds.PressAndWait(restarted, clock);

        (await SyncHarness.ReadStatus(restarted)).LastResult.Should().Be("changed");
        (await SyncRounds.ReadLayout(restarted)).Titles.Should().BeEquivalentTo(quarter.Select(item => item.Title));
    }

    [Fact]
    public async Task A_removal_of_less_than_half_is_accepted_at_once()
    {
        var clock = SyncHarness.NewClock();
        var kept = Full.Where(item => item.Owned).Take(40).ToList();
        var source = new SyncRounds.SwitchableBgg(Full);
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        source.Serve(kept);

        await SyncRounds.PressAndWait(client, clock);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
        (await SyncRounds.ReadLayout(client)).Titles.Should().BeEquivalentTo(kept.Select(item => item.Title));
    }

    [Fact]
    public async Task A_first_sync_of_an_empty_collection_is_stored_and_shows_the_bare_cabinet_without_the_being_filled_note()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        var source = new SyncRounds.SwitchableBgg([]);
        await using var factory = CreateFactory(source, clock, storage);
        using var client = factory.CreatePublicClient();
        (await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().Contain(BeingFilled);

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var status = await SyncHarness.ReadStatus(client);
        using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(SnapshotPath(storage), TestContext.Current.CancellationToken));

        status.LastResult.Should().Be("changed");
        status.Json.GetProperty("heldBack").GetBoolean().Should().BeFalse();
        stored.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        (await SyncRounds.ReadLayout(client)).Titles.Should().BeEmpty();
        (await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().NotContain(BeingFilled);
    }

    [Fact]
    public async Task A_first_sync_of_a_single_entry_gives_one_placement()
    {
        var clock = SyncHarness.NewClock();
        var single = SyntheticBggCollection.Create(65).Where(item => item.Owned && !item.IsExpansion).Take(1).ToList();
        var source = new SyncRounds.SwitchableBgg(single);
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);

        (await SyncRounds.ReadLayout(client)).Titles.Should().HaveCount(1);
    }

    private static List<FakeBggItem> Quarter(int skip) => [.. Full.Where(item => item.Owned).Skip(skip).Take(OwnedCount / 4)];

    private static int OwnedCount => Full.Count(item => item.Owned);

    private static string SnapshotPath(TemporaryDirectory storage) => Path.Combine(storage.FullPath, "snapshot.json");

    private static string StatePath(TemporaryDirectory storage) => Path.Combine(storage.FullPath, "sync-state.json");

    private static CabinetWebApplicationFactory CreateFactory(
        SyncRounds.SwitchableBgg source,
        TimeProvider clock,
        TemporaryDirectory storage) =>
        SyncHarness.CreateFactory(
            source.Handler,
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
}
