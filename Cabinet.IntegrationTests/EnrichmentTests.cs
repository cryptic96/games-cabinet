using System.Globalization;
using System.Text.Json;
using System.Web;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the details step of a sync: what the details say about expansions moves them beside their owned base games, the
/// calls stay polite and few, and trouble with details never touches the collection that is shown.
/// </summary>
[Trait("Category", "Sync")]
public sealed class EnrichmentTests
{
    private const long ExpansionOfOwnedBaseEntry = SyntheticBggCollection.FirstCollId + 3;
    private const long ExpansionOfMissingBaseEntry = SyntheticBggCollection.FirstCollId + 5;
    private const int OwnedBaseGameId = SyntheticBggCollection.FirstObjectId;
    private const string LayoutPath = "/cabinet/layout?profile=desktop";

    [Fact]
    public async Task An_owned_expansion_stands_beside_its_owned_base_game_and_an_orphan_is_labelled_with_its_base()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(handler, clock);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var layout = JsonDocument.Parse(await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken));
        var placements = Placements(layout).ToList();

        var beside = placements.Single(placement => placement.GetProperty("entryId").GetInt64() == ExpansionOfOwnedBaseEntry);
        beside.GetProperty("kind").GetString().Should().BeOneOf("expansionLayer", "expansionSpine");
        beside.GetProperty("familyId").GetInt32().Should().Be(OwnedBaseGameId);

        var orphan = placements.Single(placement => placement.GetProperty("entryId").GetInt64() == ExpansionOfMissingBaseEntry);
        orphan.GetProperty("kind").GetString().Should().Be("orphanExpansion");
        orphan.GetProperty("baseTitle").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Every_details_call_names_at_most_twenty_games_asks_only_for_statistics_and_sends_the_token_only_to_the_api_host()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(handler, clock);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var things = handler.ThingRequests();

        things.Should().NotBeEmpty();
        things.Should().OnlyContain(request => IdsOf(request).Count >= 1 && IdsOf(request).Count <= 20);
        things.Should().OnlyContain(request => HttpUtility.ParseQueryString(request.Uri.Query)["stats"] == "1");
        things.Should().OnlyContain(request => HttpUtility.ParseQueryString(request.Uri.Query).AllKeys.OrderBy(key => key).SequenceEqual(new[] { "id", "stats" }));
        handler.Requests.Should().OnlyContain(request =>
            request.Uri.Host == BggOptions.ApiHost
            && request.AuthorizationScheme == "Bearer"
            && request.AuthorizationParameter == SyncHarness.Token);
    }

    [Fact]
    public async Task A_second_sync_with_unchanged_answers_and_fresh_details_sends_no_details_call()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(handler, clock);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var thingsAfterFirst = handler.ThingRequests().Count;

        await SyncRounds.PressAndWait(client, clock);

        handler.CollectionRequests().Should().HaveCount(4);
        thingsAfterFirst.Should().Be(1);
        handler.ThingRequests().Should().HaveCount(thingsAfterFirst);
    }

    [Fact]
    public async Task A_game_new_to_the_collection_gets_its_details_in_the_sync_that_first_sees_it_and_only_those()
    {
        using var storage = new TemporaryDirectory();
        var source = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var before = source.Handler.ThingRequests().Count;
        var known = StoredGameIds(storage);

        source.Serve(SyntheticBggCollection.Create(65));
        await SyncRounds.PressAndWait(client, clock);

        var asked = source.Handler.ThingRequests().Skip(before).SelectMany(GameIdsOf).ToList();
        var after = StoredGameIds(storage);
        asked.Should().NotBeEmpty();
        asked.Intersect(known).Should().BeEmpty();
        after.Should().BeEquivalentTo([.. known, .. asked]);
    }

    [Fact]
    public async Task Once_the_details_are_a_week_old_each_run_refreshes_one_call_of_the_longest_known_games()
    {
        using var storage = new TemporaryDirectory();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var known = StoredGameIds(storage).Order().ToList();
        var before = handler.ThingRequests().Count;
        clock.Advance(TimeSpan.FromDays(7));

        await SyncRounds.PressAndWait(client, clock);
        var firstRefresh = handler.ThingRequests().Skip(before).ToList();
        await SyncRounds.PressAndWait(client, clock);
        var secondRefresh = handler.ThingRequests().Skip(before + 1).ToList();

        firstRefresh.Should().HaveCount(1);
        secondRefresh.Should().HaveCount(1);
        GameIdsOf(firstRefresh[0]).Should().Equal(known.Take(20));
        GameIdsOf(secondRefresh[0]).Should().Equal(known.Skip(20).Take(20));
    }

    [Fact]
    public async Task Details_that_fail_leave_the_collection_shown_and_are_asked_for_again_on_the_next_run()
    {
        using var storage = new TemporaryDirectory();
        var failing = true;
        var items = SyntheticBggCollection.Create(5);
        var handler = new ScriptedBggHandler(request =>
            failing && request.RequestUri!.AbsolutePath.EndsWith("/thing", StringComparison.Ordinal)
                ? ScriptedResponse.Empty(System.Net.HttpStatusCode.ServiceUnavailable)
                : SyncRounds.Healthy(request, items));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false, moveClockWhileWaiting: true);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
        (await SyncRounds.ReadLayout(client)).Titles.Should().NotBeEmpty();
        StoredGameIds(storage).Should().BeEmpty();
        handler.ThingRequests().Should().HaveCount(2, "one attempt and its single retry");

        failing = false;
        await SyncRounds.PressAndWait(client, clock);

        StoredGameIds(storage).Should().NotBeEmpty();
        handler.ThingRequests().Should().HaveCount(3);
    }

    [Fact]
    public async Task A_details_failure_keeps_the_details_already_known_untouched()
    {
        using var storage = new TemporaryDirectory();
        var failing = false;
        var items = SyntheticBggCollection.Create(5);
        var handler = new ScriptedBggHandler(request =>
            failing && request.RequestUri!.AbsolutePath.EndsWith("/thing", StringComparison.Ordinal)
                ? ScriptedResponse.Empty(System.Net.HttpStatusCode.ServiceUnavailable)
                : SyncRounds.Healthy(request, items));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var gamesBefore = StoredGamesText(storage);
        clock.Advance(TimeSpan.FromDays(8));
        failing = true;

        await SyncRounds.PressAndWait(client, clock, moveClockWhileWaiting: true);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("unchanged");
        StoredGamesText(storage).Should().Be(gamesBefore);
    }

    [Fact]
    public async Task A_run_past_its_extras_deadline_starts_no_new_details_call_and_the_next_run_goes_on()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(65);
        var clock = SyncHarness.NewClock();
        var handler = new ScriptedBggHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/thing", StringComparison.Ordinal))
            {
                clock.Advance(TimeSpan.FromMinutes(7));
            }

            return SyncRounds.Healthy(request, items);
        });
        await using var factory = SyncHarness.CreateFactory(handler, clock, StorageSettings(storage));
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);

        handler.ThingRequests().Should().ContainSingle();
        var afterFirst = StoredGameIds(storage);
        afterFirst.Should().HaveCount(20);

        await SyncRounds.PressAndWait(client, clock);

        StoredGameIds(storage).Should().HaveCount(40).And.Contain(afterFirst);
    }

    private static Dictionary<string, string?> StorageSettings(TemporaryDirectory storage) =>
        new() { ["Storage:Directory"] = storage.FullPath };

    private static List<int> StoredGameIds(TemporaryDirectory storage)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage.FullPath, "snapshot.json")));

        return document.RootElement.TryGetProperty("games", out var games) && games.ValueKind == JsonValueKind.Object
            ? [.. games.EnumerateObject().Select(property => int.Parse(property.Name, CultureInfo.InvariantCulture))]
            : [];
    }

    private static string StoredGamesText(TemporaryDirectory storage)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage.FullPath, "snapshot.json")));

        return document.RootElement.GetProperty("games").GetRawText();
    }

    private static List<string> IdsOf(RecordedRequest request) =>
        [.. HttpUtility.ParseQueryString(request.Uri.Query)["id"]!.Split(',', StringSplitOptions.RemoveEmptyEntries)];

    private static List<int> GameIdsOf(RecordedRequest request) =>
        [.. IdsOf(request).Select(id => int.Parse(id, CultureInfo.InvariantCulture))];

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());
}
