using System.Text.Json;
using System.Web;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a sync carries every owned copy, expansion, box size and, when it is switched on, location from a BGG-shaped
/// source into the cabinet, and that no location ever reaches a visitor.
/// </summary>
[Trait("Category", "Sync")]
public class CollectionFidelityTests
{
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private const string SyncPath = "/cabinet/sync";

    [Fact]
    public async Task Two_copies_of_one_game_are_two_boxes_and_an_unowned_entry_is_not_drawn()
    {
        var items = SyntheticBggCollection.Create(65);
        var handler = ScriptedBggHandler.ForCollection(items);
        using var storage = new TemporaryDirectory();
        await using var factory = CreateFactory(handler, storage, includePrivateInfo: false);

        var placements = await SyncAndReadPlacements(factory);

        var copies = placements.Where(placement => placement.GameId == SyntheticBggCollection.FirstObjectId).ToList();
        copies.Should().HaveCount(2);
        copies.Select(placement => placement.EntryId).Should().OnlyHaveUniqueItems();
        var unowned = items.Single(item => !item.Owned);
        placements.Should().NotContain(placement => placement.EntryId == unowned.CollId);
        placements.Select(placement => placement.EntryId).Should().BeEquivalentTo(items.Where(item => item.Owned).Select(item => item.CollId));
    }

    [Fact]
    public async Task Every_expansion_is_drawn_as_an_expansion()
    {
        var items = SyntheticBggCollection.Create(65);
        using var storage = new TemporaryDirectory();
        await using var factory = CreateFactory(ScriptedBggHandler.ForCollection(items), storage, includePrivateInfo: false);

        var placements = await SyncAndReadPlacements(factory);

        var expansionIds = items.Where(item => item.IsExpansion && item.Owned).Select(item => item.CollId).ToList();
        expansionIds.Should().NotBeEmpty();
        foreach (var id in expansionIds)
        {
            placements.Single(placement => placement.EntryId == id).IsExpansion.Should().BeTrue();
        }

        placements.Where(placement => !expansionIds.Contains(placement.EntryId)).Should().OnlyContain(placement => !placement.IsExpansion);
    }

    [Fact]
    public async Task With_private_info_on_both_calls_ask_for_it_and_the_snapshot_keeps_locations_but_no_visitor_sees_them()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65));
        using var storage = new TemporaryDirectory();
        await using var factory = CreateFactory(handler, storage, includePrivateInfo: true);
        using var client = factory.CreatePublicClient();

        var layoutJson = await SyncAndReadLayout(factory);
        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        handler.CollectionRequests().Should().HaveCount(2);
        handler.CollectionRequests().Should().OnlyContain(request => HttpUtility.ParseQueryString(request.Uri.Query)["showprivate"] == "1");
        handler.ThingRequests().Should().OnlyContain(request => HttpUtility.ParseQueryString(request.Uri.Query)["showprivate"] == null);
        var snapshot = await File.ReadAllTextAsync(Path.Combine(storage.FullPath, "snapshot.json"), TestContext.Current.CancellationToken);
        snapshot.Should().Contain("\"location\":\"Shelf A\"").And.Contain("\"location\":\"Shelf B\"");
        layoutJson.Should().NotContain("Shelf A").And.NotContain("Shelf B");
        page.Should().NotContain("Shelf A").And.NotContain("Shelf B");
    }

    [Fact]
    public async Task With_private_info_off_no_call_asks_for_it_and_no_location_is_stored()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65));
        using var storage = new TemporaryDirectory();
        await using var factory = CreateFactory(handler, storage, includePrivateInfo: false);

        var layoutJson = await SyncAndReadLayout(factory);

        handler.CollectionRequests().Should().HaveCount(2);
        handler.Requests.Should().OnlyContain(request => HttpUtility.ParseQueryString(request.Uri.Query)["showprivate"] == null);
        var snapshot = await File.ReadAllTextAsync(Path.Combine(storage.FullPath, "snapshot.json"), TestContext.Current.CancellationToken);
        snapshot.Should().NotContain("Shelf A").And.NotContain("Shelf B");
        layoutJson.Should().NotContain("Shelf A");
    }

    [Fact]
    public async Task A_stored_box_size_comes_from_the_owned_version()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        using var storage = new TemporaryDirectory();
        await using var factory = CreateFactory(handler, storage, includePrivateInfo: false);

        await SyncAndReadLayout(factory);

        var snapshot = await File.ReadAllTextAsync(Path.Combine(storage.FullPath, "snapshot.json"), TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(snapshot);
        var first = document.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("collectionId").GetInt64() == SyntheticBggCollection.FirstCollId);
        var dimensions = first.GetProperty("dimensions");

        dimensions.GetProperty("width").GetDouble().Should().Be(6.3);
        dimensions.GetProperty("length").GetDouble().Should().Be(8.27);
        dimensions.GetProperty("depth").GetDouble().Should().Be(2.09);
    }

    private static CabinetWebApplicationFactory CreateFactory(ScriptedBggHandler handler, TemporaryDirectory storage, bool includePrivateInfo) =>
        new(
            new Dictionary<string, string?>
            {
                ["Storage:Directory"] = storage.FullPath,
                ["Bgg:Username"] = "sentinel-user-name",
                ["Bgg:Token"] = "sentinel-token-value",
                ["Bgg:IncludePrivateInfo"] = includePrivateInfo ? "true" : "false",
            },
            services =>
            {
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddHttpClient<IEnrichmentSource, BggThingClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            });

    private static async Task<List<PlacementView>> SyncAndReadPlacements(CabinetWebApplicationFactory factory)
    {
        using var document = JsonDocument.Parse(await SyncAndReadLayout(factory));

        return
        [
            .. document.RootElement.GetProperty("sections").EnumerateArray()
                .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
                .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
                .Select(placement => new PlacementView(
                    placement.GetProperty("gameId").GetInt32(),
                    placement.GetProperty("entryId").GetInt64(),
                    placement.TryGetProperty("isExpansion", out var flag) && flag.ValueKind == JsonValueKind.True)),
        ];
    }

    private static async Task<string> SyncAndReadLayout(CabinetWebApplicationFactory factory)
    {
        using var client = factory.CreatePublicClient();
        var initial = await ReadLayout(client);

        using var press = await client.PostAsync(SyncPath, content: null, TestContext.Current.CancellationToken);
        press.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);

        var changed = initial;
        await SyncHarness.WaitUntil(async () =>
        {
            changed = await ReadLayout(client);

            return changed.ETag != initial.ETag;
        });

        return changed.Json;
    }

    private static async Task<(string? ETag, string Json)> ReadLayout(HttpClient client)
    {
        using var response = await client.GetAsync(LayoutPath, TestContext.Current.CancellationToken);

        return (response.Headers.ETag?.Tag, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private sealed record PlacementView(int GameId, long EntryId, bool IsExpansion);
}
