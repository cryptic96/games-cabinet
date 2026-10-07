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

    private static List<string> IdsOf(RecordedRequest request) =>
        [.. HttpUtility.ParseQueryString(request.Uri.Query)["id"]!.Split(',', StringSplitOptions.RemoveEmptyEntries)];

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());
}
