using System.Net;
using System.Text.Json;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the owner data and the sync live side by side: a sync never touches the owner file, a save made while a sync is
/// running is not lost when the sync commits, and a new host on the same state directory still has both.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerPersistenceTests
{
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string LocationName = "Cabinet A";
    private const string OwnerFileName = "owner-data.json";
    private const string RenamedTitle = "Renamed By The Sync";

    [Fact]
    public async Task A_sync_that_renames_another_game_leaves_the_owner_file_untouched_and_the_placed_game_in_its_location()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var source = new HeldBgg(items);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var placedGame = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);
        await PlaceAsync(factory, placedGame.GameId, placedGame.Title);
        var ownerFile = Path.Combine(storage.FullPath, OwnerFileName);
        var bytesBefore = await File.ReadAllBytesAsync(ownerFile, TestContext.Current.CancellationToken);
        var renamed = OtherBaseGame(items, placedGame.GameId);
        source.Serve(Rename(items, renamed.ObjectId));

        await SyncRounds.PressAndWait(client, clock);
        var after = await ReadCards(client);

        (await File.ReadAllBytesAsync(ownerFile, TestContext.Current.CancellationToken)).Should().Equal(bytesBefore);
        after.Cards.Single(card => card.EntryId == placedGame.EntryId).Location.Should().Be(LocationName);
        after.Cards.Single(card => card.GameId == renamed.ObjectId).Title.Should().Be(RenamedTitle);
    }

    [Fact]
    public async Task A_save_made_while_a_sync_is_running_survives_the_sync_commit_and_visitors_see_both_changes()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var source = new HeldBgg(items);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var placedGame = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);
        var renamed = OtherBaseGame(items, placedGame.GameId);
        source.Serve(Rename(items, renamed.ObjectId));
        source.HoldNextCollectionAnswer();

        var round = SyncRounds.PressAndWait(client, clock);
        await SyncHarness.WaitUntil(() => Task.FromResult(source.IsHolding));
        var saved = await PlaceAsync(factory, placedGame.GameId, placedGame.Title);
        source.Release();
        await round;
        var after = await ReadCards(client);

        saved.Outcome.Should().Be(OwnerOutcome.Saved);
        after.Cards.Single(card => card.GameId == renamed.ObjectId).Title.Should().Be(RenamedTitle);
        after.Cards.Single(card => card.EntryId == placedGame.EntryId).Location.Should().Be(LocationName);
    }

    [Fact]
    public async Task A_new_host_on_the_same_state_directory_shows_the_location_and_the_synced_data_without_a_new_sync()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var source = new HeldBgg(items);
        var clock = SyncHarness.NewClock();
        CardsReading before;
        int renamedGameId;

        await using (var first = SyncHarness.CreateFactory(source.Handler, clock, StorageSettings(storage)))
        {
            using var client = first.CreatePublicClient();
            await SyncRounds.PressAndWait(client, clock, advance: false);
            var placedGame = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);
            renamedGameId = OtherBaseGame(items, placedGame.GameId).ObjectId;
            source.Serve(Rename(items, renamedGameId));
            await SyncRounds.PressAndWait(client, clock);
            await PlaceAsync(first, placedGame.GameId, placedGame.Title);
            before = await ReadCards(client);
        }

        await using var second = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock(), StorageSettings(storage));
        using var restarted = second.CreatePublicClient();
        var after = await ReadCards(restarted);

        after.ETag.Should().Be(before.ETag);
        after.Cards.Should().Contain(card => card.Location == LocationName);
        after.Cards.Single(card => card.GameId == renamedGameId).Title.Should().Be(RenamedTitle);
    }

    [Fact]
    public async Task A_host_started_on_seeded_owner_data_shows_the_seeded_location_on_the_card()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var seededGame = items.First(item => !item.IsExpansion && item.Owned);
        OwnerDataSeed.Write(
            storage.FullPath,
            OwnerDataSeed.With([LocationName], new Dictionary<int, string> { [seededGame.ObjectId] = LocationName }));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var cards = await ReadCards(client);

        cards.Cards.Where(card => card.GameId == seededGame.ObjectId && !card.IsExpansion).Should().NotBeEmpty().And.OnlyContain(card => card.Location == LocationName);
    }

    [Fact]
    public async Task A_held_back_answer_never_starts_the_thirty_day_clock_of_any_game()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(65);
        var source = new HeldBgg(items);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var placedGame = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);
        await PlaceAsync(factory, placedGame.GameId, placedGame.Title);
        var ownerFile = Path.Combine(storage.FullPath, OwnerFileName);
        var bytesBefore = await File.ReadAllBytesAsync(ownerFile, TestContext.Current.CancellationToken);
        source.Serve([]);

        await SyncRounds.PressAndWait(client, clock);
        await SyncRounds.PressAndWait(client, clock);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("heldBack");
        (await File.ReadAllBytesAsync(ownerFile, TestContext.Current.CancellationToken)).Should().Equal(bytesBefore);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        ((OwnerDataState.Ready)service.State).Data.Games.Should().OnlyContain(game => game.AbsentSinceUtc == null);
    }

    private static Dictionary<string, string?> StorageSettings(TemporaryDirectory storage) =>
        new() { ["Storage:Directory"] = storage.FullPath };

    private static FakeBggItem OtherBaseGame(IReadOnlyList<FakeBggItem> items, int excludedGameId) =>
        items.First(item => !item.IsExpansion && item.Owned && item.ObjectId != excludedGameId);

    private static IReadOnlyList<FakeBggItem> Rename(IReadOnlyList<FakeBggItem> items, int gameId) =>
        [.. items.Select(item => item.ObjectId == gameId && !item.IsExpansion ? item with { Title = RenamedTitle } : item)];

    private static async Task<OwnerResult> PlaceAsync(CabinetWebApplicationFactory factory, int gameId, string title)
    {
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var added = await service.MutateAsync(
            service.Revision,
            data => LocationEdits.Add(data, LocationName, OwnerDataService.NewId),
            TestContext.Current.CancellationToken);

        added.Outcome.Should().Be(OwnerOutcome.Saved);

        return await service.MutateAsync(
            added.Data!.Revision,
            data => LocationEdits.Assign(data, gameId, data.Locations.Single(location => location.Name == LocationName).Id, title),
            TestContext.Current.CancellationToken);
    }

    private static async Task<CardsReading> ReadCards(HttpClient client)
    {
        using var response = await client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var cards = document.RootElement.GetProperty("cards").EnumerateArray()
            .Select(card => new CardReading(
                card.GetProperty("entryId").GetInt64(),
                card.GetProperty("gameId").GetInt32(),
                card.GetProperty("title").GetString()!,
                card.GetProperty("isExpansion").GetBoolean(),
                card.TryGetProperty("location", out var location) ? location.GetString() : null))
            .ToList();

        return new CardsReading(response.Headers.ETag!.Tag, cards);
    }

    private sealed record CardReading(long EntryId, int GameId, string Title, bool IsExpansion, string? Location);

    private sealed record CardsReading(string ETag, IReadOnlyList<CardReading> Cards);

    /// <summary>An invented BGG that serves a switchable collection and can hold back the answer to one collection call until released.</summary>
    private sealed class HeldBgg
    {
        private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(20);

        private readonly object _gate = new();
        private readonly ManualResetEventSlim _holding = new(false);
        private readonly ManualResetEventSlim _released = new(false);
        private IReadOnlyList<FakeBggItem> _items;
        private bool _holdNext;

        public HeldBgg(IReadOnlyList<FakeBggItem> items)
        {
            _items = items;
            Handler = new ScriptedBggHandler(Answer);
        }

        public ScriptedBggHandler Handler { get; }

        public bool IsHolding => _holding.IsSet && !_released.IsSet;

        public void Serve(IReadOnlyList<FakeBggItem> items)
        {
            lock (_gate)
            {
                _items = items;
            }
        }

        public void HoldNextCollectionAnswer()
        {
            lock (_gate)
            {
                _holdNext = true;
            }
        }

        public void Release() => _released.Set();

        private ScriptedResponse Answer(HttpRequestMessage request)
        {
            IReadOnlyList<FakeBggItem> items;
            bool hold;

            lock (_gate)
            {
                items = _items;
                hold = _holdNext && request.RequestUri!.AbsolutePath.EndsWith("/collection", StringComparison.Ordinal);
                _holdNext &= !hold;
            }

            if (hold)
            {
                _holding.Set();
                _released.Wait(HoldLimit);
            }

            return SyncRounds.Healthy(request, items);
        }
    }
}
