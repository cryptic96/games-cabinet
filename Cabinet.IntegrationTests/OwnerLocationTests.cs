using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private const string LocationsPath = "/owner/locations";

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

    [Fact]
    public async Task The_locations_page_does_not_exist_on_the_public_port()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(LocationsPath, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_locations_page_renders_in_the_owner_shell_with_locations_as_the_current_item()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);

        var page = await session.GetAsync(LocationsPath);

        page.Status.Should().Be(HttpStatusCode.OK);
        page.Body.Should().Contain("class=\"owner-plate\"").And.Contain("<h2>Locations</h2>").And.Contain("Say where each game is stored.");
        page.Body.Should().Contain("aria-current=\"page\">Locations</a>");
    }

    [Fact]
    public async Task The_owner_adds_a_location_and_places_a_game_with_forms_and_every_visitor_sees_it_on_the_card()
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
        using var session = new OwnerSession(factory);
        var page = await session.GetAsync(LocationsPath);

        var added = await session.PostFormAsync(
            $"{LocationsPath}?handler=AddLocation",
            [Field("name", LocationName), Field("revision", OwnerSession.FieldValue(page.Body, "revision"))]);
        var afterAdd = await session.GetAsync(LocationsPath);
        var placed = await session.PostFormAsync(
            $"{LocationsPath}?handler=Assign",
            [
                Field("gameId", game.GameId.ToString(CultureInfo.InvariantCulture)),
                Field("locationId", LocationIdOf(afterAdd.Body, LocationName)),
                Field("revision", OwnerSession.FieldValue(afterAdd.Body, "revision")),
            ]);

        added.Status.Should().Be(HttpStatusCode.Found);
        added.Location.Should().StartWith($"{LocationsPath}?done=saved#");
        placed.Status.Should().Be(HttpStatusCode.Found);
        placed.Location.Should().Be($"{LocationsPath}?done=saved#game-{game.GameId}");
        (await ReadCards(client)).Cards.Single(card => card.EntryId == game.EntryId).Location.Should().Be(LocationName);
    }

    [Fact]
    public async Task The_same_posts_asking_for_json_answer_with_the_outcome_the_revision_and_whether_undo_is_possible()
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
        using var session = new OwnerSession(factory);
        var page = await session.GetAsync(LocationsPath);

        var added = await session.PostJsonAsync(
            $"{LocationsPath}?handler=AddLocation",
            [Field("name", LocationName), Field("revision", OwnerSession.FieldValue(page.Body, "revision"))]);
        var afterAdd = await session.GetAsync(LocationsPath);
        var placed = await session.PostJsonAsync(
            $"{LocationsPath}?handler=Assign",
            [
                Field("gameId", game.GameId.ToString(CultureInfo.InvariantCulture)),
                Field("locationId", LocationIdOf(afterAdd.Body, LocationName)),
                Field("revision", added.Json.GetProperty("revision").GetInt64().ToString(CultureInfo.InvariantCulture)),
            ]);

        added.Status.Should().Be(HttpStatusCode.OK);
        added.JsonText("outcome").Should().Be("saved");
        added.Json.GetProperty("revision").GetInt64().Should().Be(1);
        added.Json.GetProperty("canUndo").GetBoolean().Should().BeTrue();
        added.JsonText("status").Should().Be("location-added");
        placed.Status.Should().Be(HttpStatusCode.OK);
        placed.JsonText("outcome").Should().Be("saved");
        placed.Json.GetProperty("revision").GetInt64().Should().Be(2);
        (await ReadCards(client)).Cards.Single(card => card.EntryId == game.EntryId).Location.Should().Be(LocationName);
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

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static string LocationIdOf(string html, string name)
    {
        var match = Regex.Match(html, "<option value=\"([^\"]+)\"[^>]*>" + Regex.Escape(name) + "</option>");

        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException($"The page offers no location called {name}.");
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
