using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Collection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the names endpoint serves the other titles of owned games and nothing else, revalidates by entity tag, answers
/// the invented collections, follows the collection when it changes, and sends no cross-origin headers.
/// </summary>
public sealed class SearchNamesEndpointTests
{
    private const int OwnedGameId = 5001;
    private const int OtherOwnedGameId = 5003;
    private const int UnownedGameId = 5009;
    private const string Route = "/cabinet/search-names";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task The_synced_collection_lists_names_for_owned_games_only()
    {
        using var storage = new TemporaryDirectory();
        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(InventedSnapshot(["De Stille Groeve", "Invented Voyage", "Café Élan Noir"]));
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var names = document.RootElement.GetProperty("names");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("application/json");
        response.Headers.ETag!.Tag.Should().StartWith("\"names-");
        document.RootElement.EnumerateObject().Select(member => member.Name).Should().Equal("names");
        names.EnumerateObject().Select(member => member.Name).Should().Equal(OwnedGameId.ToString(), OtherOwnedGameId.ToString());
        names.GetProperty(OwnedGameId.ToString()).EnumerateArray().Select(name => name.GetString()).Should().Equal("De Stille Groeve", "Café Élan Noir");
        names.GetProperty(OtherOwnedGameId.ToString()).EnumerateArray().Select(name => name.GetString()).Should().Equal("Kasteel van Mist");
        body.Should().NotContain("Not Owned Alias").And.NotContain("Invented Designer").And.NotContain("Drafting").And.NotContain("example.com");
    }

    [Fact]
    public async Task The_response_is_revalidated_and_a_repeat_with_the_tag_answers_not_modified()
    {
        using var storage = new TemporaryDirectory();
        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(InventedSnapshot(["De Stille Groeve"]));
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        first.Headers.CacheControl!.NoCache.Should().BeTrue();
        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        second.Headers.ETag.Should().Be(first.Headers.ETag);
        second.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task A_different_tag_gets_the_full_response()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, Route + "?sample=65");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_sample_answers_its_own_names_and_a_repeat_costs_a_304()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(Route + "?sample=65", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var names = document.RootElement.GetProperty("names").EnumerateObject().ToList();
        using var request = new HttpRequestMessage(HttpMethod.Get, Route + "?sample=65");
        request.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        using var repeat = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        names.Should().NotBeEmpty();
        names.Should().OnlyContain(member => member.Value.GetArrayLength() > 0 && member.Value.EnumerateArray().All(name => !string.IsNullOrWhiteSpace(name.GetString())));
        names.SelectMany(member => member.Value.EnumerateArray()).Select(name => name.GetString()).Should().Contain("De Stille Groeve");
        repeat.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task A_sample_that_is_not_honoured_gets_the_synced_collection_and_is_not_echoed()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(Route + "?sample=%3Cscript%3Eevil", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("{\"names\":{}}");
    }

    [Fact]
    public async Task The_response_carries_no_cross_origin_header()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, Route + "?sample=5");
        request.Headers.Add("Origin", "https://example.org");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [Fact]
    public async Task Before_the_first_sync_the_names_are_an_empty_object_with_a_tag()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().StartWith("\"names-");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("{\"names\":{}}");
    }

    [Fact]
    public async Task A_sync_that_only_changes_the_other_titles_gives_a_new_tag_without_a_restart()
    {
        using var storage = new TemporaryDirectory();
        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(InventedSnapshot(["De Stille Groeve"]));
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        using var before = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        factory.ServingServices.GetRequiredService<CollectionStore>().Replace(CollectionState.FromSnapshot(InventedSnapshot(["Een Andere Titel"])));
        using var after = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await after.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        after.Headers.ETag.Should().NotBe(before.Headers.ETag);
        document.RootElement.GetProperty("names").GetProperty(OwnedGameId.ToString()).EnumerateArray().Select(name => name.GetString()).Should().Equal("Een Andere Titel");
    }

    [Fact]
    public async Task Asking_for_names_never_reaches_the_data_source()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        foreach (var path in new[] { Route, Route + "?sample=65", Route + "?sample=12" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        factory.NetworkGuard.Refused.Should().BeEmpty();
    }

    private static CollectionSnapshot InventedSnapshot(IReadOnlyList<string> ownedNames)
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
            "https://example.com/original.jpg",
            AlternateNames: ownedNames);
        var otherOwned = details with { AlternateNames = ["Kasteel van Mist"] };
        var unowned = details with { AlternateNames = ["Not Owned Alias"] };

        return new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                new SnapshotItem(100, OwnedGameId, "Invented Voyage", ItemKind.Base, 2012, null, "Crate 7"),
                new SnapshotItem(101, OtherOwnedGameId, "Salt Road", ItemKind.Base, 2014, null, null),
            ],
            null,
            new Dictionary<int, GameDetails> { [OwnedGameId] = details, [OtherOwnedGameId] = otherOwned, [UnownedGameId] = unowned });
    }
}
