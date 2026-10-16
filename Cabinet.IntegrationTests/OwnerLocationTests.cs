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
        var content = Encoding.UTF8.GetBytes("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":4,\"revision\":7}");
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
        added.Location.Should().StartWith($"{LocationsPath}?done=saved");
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

    [Fact]
    public async Task A_json_post_from_a_stale_page_is_answered_409_out_of_date_and_changes_nothing()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);

        var stale = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "0")]);

        stale.Status.Should().Be(HttpStatusCode.Conflict);
        stale.JsonText("outcome").Should().Be("outOfDate");
        stale.Json.GetProperty("revision").GetInt64().Should().Be(1);
        (await session.GetAsync(LocationsPath)).Body.Should().Contain(LocationName).And.NotContain("Shelf B");
    }

    [Fact]
    public async Task A_form_post_without_a_token_is_answered_400_and_changes_nothing()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.GetAsync(LocationsPath);

        var form = await session.PostFormAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")], withToken: false);
        var json = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")], withToken: false);

        form.Status.Should().Be(HttpStatusCode.BadRequest);
        form.Body.Should().BeEmpty();
        json.Status.Should().Be(HttpStatusCode.BadRequest);
        (await session.GetAsync(LocationsPath)).Body.Should().NotContain(LocationName);
        factory.ServingServices.GetRequiredService<OwnerDataService>().Revision.Should().Be(0);
    }

    [Fact]
    public async Task Adding_a_name_that_already_exists_is_answered_422_with_the_duplicate_message()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);

        var duplicate = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "  cabinet a "), Field("revision", "1")]);
        var empty = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "   "), Field("revision", "1")]);

        duplicate.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        duplicate.JsonText("outcome").Should().Be("refused");
        duplicate.JsonText("status").Should().Be("name-taken");
        duplicate.JsonText("message").Should().Be("You already have a location with that name.");
        empty.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        empty.JsonText("message").Should().Be("Give the location a name.");
    }

    [Fact]
    public async Task A_refused_form_post_goes_back_to_the_page_with_a_fixed_code_and_the_page_says_why()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.PostFormAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);

        var duplicate = await session.PostFormAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "1")]);
        var page = await session.GetAsync(duplicate.Location!);

        duplicate.Status.Should().Be(HttpStatusCode.Found);
        duplicate.Location.Should().Be($"{LocationsPath}?done=refused-name-taken#new-location");
        page.Body.Should().Contain("You already have a location with that name.").And.Contain("role=\"alert\"");
    }

    [Fact]
    public async Task A_code_outside_the_fixed_set_is_ignored_and_never_echoed()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);

        var page = await session.GetAsync($"{LocationsPath}?done=refused-%3Cscript%3Ealert(1)%3C/script%3E");
        var other = await session.GetAsync($"{LocationsPath}?done=https://example.org/");

        page.Body.Should().NotContain("<script>alert").And.NotContain("owner-notice");
        other.Body.Should().NotContain("example.org").And.NotContain("owner-notice");
    }

    [Fact]
    public async Task Undo_takes_back_the_latest_change_and_a_stale_undo_is_answered_409()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "1")]);

        var stale = await session.PostJsonAsync($"{LocationsPath}?handler=Undo", [Field("revision", "1")]);
        var undone = await session.PostJsonAsync($"{LocationsPath}?handler=Undo", [Field("revision", "2")]);
        var again = await session.PostJsonAsync($"{LocationsPath}?handler=Undo", [Field("revision", "3")]);
        var page = await session.GetAsync(LocationsPath);

        stale.Status.Should().Be(HttpStatusCode.Conflict);
        stale.JsonText("outcome").Should().Be("outOfDate");
        stale.JsonText("status").Should().Be("cant-undo");
        undone.Status.Should().Be(HttpStatusCode.OK);
        undone.JsonText("outcome").Should().Be("saved");
        undone.JsonText("status").Should().Be("undone");
        undone.Json.GetProperty("revision").GetInt64().Should().Be(3);
        undone.Json.GetProperty("canUndo").GetBoolean().Should().BeFalse();
        again.Status.Should().Be(HttpStatusCode.Conflict);
        page.Body.Should().Contain(LocationName).And.NotContain("Shelf B");
    }

    [Fact]
    public async Task The_page_offers_undo_right_after_a_saved_change_and_the_form_post_takes_the_change_back()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        var added = await session.PostFormAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);

        var page = await session.GetAsync(added.Location!);
        var plain = await session.GetAsync(LocationsPath);
        var undone = await session.PostFormAsync($"{LocationsPath}?handler=Undo", [Field("revision", OwnerSession.FieldValue(page.Body, "revision"))]);

        page.Body.Should().Contain("handler=Undo");
        plain.Body.Should().NotContain("handler=Undo");
        undone.Status.Should().Be(HttpStatusCode.Found);
        undone.Location.Should().StartWith($"{LocationsPath}?done=undone#");
        (await session.GetAsync(undone.Location!)).Body.Should().NotContain(LocationName).And.Contain("Undone.");
    }

    [Fact]
    public async Task While_the_owner_file_is_unreadable_a_post_is_answered_409_and_the_file_is_left_as_it_was()
    {
        using var storage = new TemporaryDirectory();
        var path = Path.Combine(storage.FullPath, "owner-data.json");
        var content = Encoding.UTF8.GetBytes("{ this is not json");
        await File.WriteAllBytesAsync(path, content, TestContext.Current.CancellationToken);
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock(), StorageSettings(storage));
        using var session = new OwnerSession(factory);

        var page = await session.GetAsync(LocationsPath);
        var json = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);
        var form = await session.PostFormAsync($"{LocationsPath}?handler=AddLocation", [Field("name", LocationName), Field("revision", "0")]);

        page.Body.Should().Contain("couldn&#x27;t be read").And.NotContain("handler=AddLocation");
        json.Status.Should().Be(HttpStatusCode.Conflict);
        json.JsonText("outcome").Should().Be("unreadable");
        form.Location.Should().Be($"{LocationsPath}?done=unreadable#new-location");
        (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).Should().Equal(content);
        Directory.EnumerateFiles(storage.FullPath, "owner-data*").Should().Equal(path);
    }

    [Fact]
    public async Task A_game_that_is_not_in_the_collection_or_a_location_that_is_not_in_the_list_is_refused()
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

        var unknownGame = await session.PostJsonAsync($"{LocationsPath}?handler=Assign", [Field("gameId", "987654"), Field("locationId", ""), Field("revision", "0")]);
        var unknownLocation = await session.PostJsonAsync(
            $"{LocationsPath}?handler=Assign",
            [Field("gameId", game.GameId.ToString(CultureInfo.InvariantCulture)), Field("locationId", "nope"), Field("revision", "0")]);

        unknownGame.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownGame.JsonText("status").Should().Be("unknown-game");
        unknownLocation.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownLocation.JsonText("status").Should().Be("unknown-location");
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
