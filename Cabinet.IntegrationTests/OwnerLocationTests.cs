using System.Text;
using System.Text.Json;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a location written through the single owner-data writer reaches every visitor's card, changes the collection
/// version, survives a restart and a following sync, and that a damaged or newer owner-data file is never touched.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLocationTests
{
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string LocationName = "Cabinet A";

    [Fact]
    public async Task A_location_placed_through_the_writer_is_on_the_public_card_of_the_game_and_of_its_expansion()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var before = await ReadCards(client);
        var game = before.Cards.First(card => !card.IsExpansion);

        var result = await PlaceAsync(factory, game.GameId, game.Title);
        var after = await ReadCards(client);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
        after.ETag.Should().NotBe(before.ETag);
        after.Cards.Where(card => card.GameId == game.GameId).Should().OnlyContain(card => card.Location == LocationName);
        after.Cards.Where(card => card.IsExpansion && card.BaseGameIds.Contains(game.GameId))
            .Should().NotBeEmpty().And.OnlyContain(card => card.Location == LocationName);
        after.Cards.Where(card => card.GameId != game.GameId && !card.BaseGameIds.Contains(game.GameId))
            .Should().OnlyContain(card => card.Location == null);
    }

    [Fact]
    public async Task The_location_is_kept_in_its_own_file_and_never_in_the_synced_snapshot()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var game = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);

        await PlaceAsync(factory, game.GameId, game.Title);

        var ownerFile = await File.ReadAllTextAsync(Path.Combine(storage.FullPath, "owner-data.json"), TestContext.Current.CancellationToken);
        var snapshotFile = await File.ReadAllTextAsync(Path.Combine(storage.FullPath, "snapshot.json"), TestContext.Current.CancellationToken);
        ownerFile.Should().Contain("games-cabinet-owner-data").And.Contain(LocationName);
        snapshotFile.Should().NotContain(LocationName).And.NotContain("games-cabinet-owner-data");
    }

    [Fact]
    public async Task The_location_is_still_there_after_a_restart_without_a_new_sync()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        CardsReading placed;

        await using (var first = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            StorageSettings(storage)))
        {
            using var client = first.CreatePublicClient();
            await SyncRounds.PressAndWait(client, clock, advance: false);
            var game = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);
            await PlaceAsync(first, game.GameId, game.Title);
            placed = await ReadCards(client);
        }

        await using var second = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock(), StorageSettings(storage));
        using var restarted = second.CreatePublicClient();
        var afterRestart = await ReadCards(restarted);

        afterRestart.ETag.Should().Be(placed.ETag);
        afterRestart.Cards.Should().Contain(card => card.Location == LocationName);
    }

    [Fact]
    public async Task A_following_sync_keeps_the_location()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var game = (await ReadCards(client)).Cards.First(card => !card.IsExpansion);
        await PlaceAsync(factory, game.GameId, game.Title);
        var placed = await ReadCards(client);

        await SyncRounds.PressAndWait(client, clock);
        var afterSync = await ReadCards(client);

        afterSync.ETag.Should().Be(placed.ETag);
        afterSync.Cards.Single(card => card.EntryId == game.EntryId).Location.Should().Be(LocationName);
    }

    [Fact]
    public async Task A_newer_format_owner_file_is_reported_unreadable_refuses_writes_and_is_left_exactly_as_it_was()
    {
        using var storage = new TemporaryDirectory();
        var path = Path.Combine(storage.FullPath, "owner-data.json");
        var content = Encoding.UTF8.GetBytes("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":2,\"revision\":7}");
        await File.WriteAllBytesAsync(path, content, TestContext.Current.CancellationToken);
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock(), StorageSettings(storage));
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();

        var result = await service.MutateAsync(
            0,
            data => LocationEdits.Add(data, LocationName, OwnerDataService.NewId),
            TestContext.Current.CancellationToken);

        service.State.Should().BeOfType<OwnerDataState.Unreadable>();
        result.Outcome.Should().Be(OwnerOutcome.Unreadable);
        (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).Should().Equal(content);
        Directory.EnumerateFiles(storage.FullPath, "owner-data*").Should().Equal(path);
    }

    [Fact]
    public async Task A_damaged_owner_file_is_reported_unreadable_and_every_game_shows_no_location()
    {
        using var storage = new TemporaryDirectory();
        var path = Path.Combine(storage.FullPath, "owner-data.json");
        var content = Encoding.UTF8.GetBytes("{ this is not json");
        await File.WriteAllBytesAsync(path, content, TestContext.Current.CancellationToken);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            StorageSettings(storage));
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var cards = await ReadCards(client);

        cards.Cards.Should().NotBeEmpty().And.OnlyContain(card => card.Location == null);
        (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).Should().Equal(content);
    }

    private static Dictionary<string, string?> StorageSettings(TemporaryDirectory storage) =>
        new() { ["Storage:Directory"] = storage.FullPath };

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
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var cards = document.RootElement.GetProperty("cards").EnumerateArray()
            .Select(card => new CardReading(
                card.GetProperty("entryId").GetInt64(),
                card.GetProperty("gameId").GetInt32(),
                card.GetProperty("title").GetString()!,
                card.GetProperty("isExpansion").GetBoolean(),
                card.TryGetProperty("location", out var location) ? location.GetString() : null,
                card.TryGetProperty("bases", out var bases)
                    ? bases.EnumerateArray().Select(link => link.GetProperty("gameId").GetInt32()).ToList()
                    : new List<int>()))
            .ToList();

        return new CardsReading(response.Headers.ETag!.Tag, cards);
    }

    private sealed record CardReading(long EntryId, int GameId, string Title, bool IsExpansion, string? Location, IReadOnlyList<int> BaseGameIds);

    private sealed record CardsReading(string ETag, IReadOnlyList<CardReading> Cards);
}
