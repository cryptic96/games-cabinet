using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the card endpoint serves one record per owned entry, revalidates by entity tag, refuses unknown profiles and sends no cross-origin headers.</summary>
public sealed class CardsEndpointTests
{
    private const int BaseGameId = 5001;
    private const int ExpansionGameId = 5002;

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly string[] ContractFields =
    [
        "entryId", "gameId", "title", "year", "isExpansion", "ratio", "cover", "colour", "toneIndex", "patternIndex",
        "location", "minPlayers", "maxPlayers", "playTime", "minPlayTime", "maxPlayTime", "minAge", "weight", "rating",
        "designers", "mechanics", "expansions", "bases",
    ];

    [Fact]
    public async Task Cards_for_a_sample_are_json_with_a_content_derived_tag_and_the_layout_tag()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var cards = await client.GetAsync("/cabinet/cards?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var layout = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var cardsDocument = JsonDocument.Parse(await cards.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var layoutDocument = JsonDocument.Parse(await layout.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        cards.StatusCode.Should().Be(HttpStatusCode.OK);
        cards.ShouldHaveMediaType("application/json");
        cards.Headers.ETag!.Tag.Should().StartWith("\"cards-");
        cards.Headers.CacheControl!.NoCache.Should().BeTrue();
        cardsDocument.RootElement.GetProperty("layout").GetString().Should().Be(layout.Headers.ETag!.Tag);
        cardsDocument.RootElement.GetProperty("cards").GetArrayLength().Should().Be(EntryIdsOf(layoutDocument.RootElement).Count);
    }

    [Fact]
    public async Task A_repeat_request_with_the_tag_answers_not_modified()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync("/cabinet/cards?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/cards?sample=65&profile=desktop");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        second.Headers.ETag.Should().Be(first.Headers.ETag);
    }

    [Fact]
    public async Task A_different_tag_gets_the_full_response()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/cards?sample=12&profile=phone");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/cabinet/cards?sample=65&profile=tablet")]
    [InlineData("/cabinet/cards?sample=65")]
    [InlineData("/cabinet/cards?sample=65&profile=Desktop")]
    public async Task An_unknown_or_missing_profile_answers_not_found(string path)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_response_carries_no_cross_origin_header()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/cards?sample=5&profile=desktop");
        request.Headers.Add("Origin", "https://example.org");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Before_any_sync_the_cards_are_an_empty_list_with_a_tag()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/cards?profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().StartWith("\"cards-");
        document.RootElement.GetProperty("cards").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_stored_collection_is_served_with_its_details_locations_and_expansion_links()
    {
        using var storage = new TemporaryDirectory();
        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(InventedSnapshot());
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var cards = await client.GetAsync("/cabinet/cards?profile=desktop", TestContext.Current.CancellationToken);
        using var layout = await client.GetAsync("/cabinet/layout?profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await cards.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var records = document.RootElement.GetProperty("cards").EnumerateArray().ToList();
        var game = records.Single(record => record.GetProperty("gameId").GetInt32() == BaseGameId);
        var expansion = records.Single(record => record.GetProperty("gameId").GetInt32() == ExpansionGameId);

        records.Should().HaveCount(2);
        game.GetProperty("location").GetString().Should().Be("Crate 7");
        game.GetProperty("title").GetString().Should().Be("Invented Voyage");
        game.GetProperty("expansions").EnumerateArray().Select(link => link.GetProperty("gameId").GetInt32()).Should().Equal(ExpansionGameId);
        expansion.GetProperty("bases").EnumerateArray().Select(link => link.GetProperty("gameId").GetInt32()).Should().Equal(BaseGameId);
        document.RootElement.GetProperty("layout").GetString().Should().Be(layout.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task Every_record_property_is_on_the_contract_list()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/cards?sample=edge&profile=phone", TestContext.Current.CancellationToken);
        using var sample = await client.GetAsync("/cabinet/cards?sample=65&profile=desktop", TestContext.Current.CancellationToken);

        foreach (var body in new[] { response, sample })
        {
            using var document = JsonDocument.Parse(await body.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var names = document.RootElement.GetProperty("cards").EnumerateArray()
                .SelectMany(record => record.EnumerateObject().Select(property => property.Name))
                .ToHashSet();

            names.Should().NotBeEmpty();
            names.Should().BeSubsetOf(ContractFields);
        }
    }

    [Fact]
    public async Task A_sample_request_with_the_prototype_off_answers_the_synced_collection()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/cards?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        document.RootElement.GetProperty("cards").GetArrayLength().Should().Be(0);
    }

    private static CollectionSnapshot InventedSnapshot()
    {
        var details = new GameDetails(
            Moment,
            2,
            4,
            60,
            null,
            null,
            10,
            2.4,
            7.85,
            7.1,
            ["Invented Designer"],
            ["Drafting"],
            [],
            null);
        var expansionDetails = details with { ExpandsGames = [new BaseGameRef(BaseGameId, "Invented Voyage")] };

        return new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                new SnapshotItem(100, BaseGameId, "Invented Voyage", ItemKind.Base, 2012, null, "Crate 7"),
                new SnapshotItem(101, ExpansionGameId, "Invented Voyage: More Sea", ItemKind.Expansion, 2014, null, null),
            ],
            null,
            new Dictionary<int, GameDetails> { [BaseGameId] = details, [ExpansionGameId] = expansionDetails });
    }

    private static HashSet<long> EntryIdsOf(JsonElement layout) =>
        layout.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
            .Where(placement => placement.GetProperty("kind").GetString() != "moreMarker")
            .Select(placement => placement.GetProperty("entryId").GetInt64())
            .ToHashSet();
}
