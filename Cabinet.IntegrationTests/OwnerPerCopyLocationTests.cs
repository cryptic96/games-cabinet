using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the locations page lists every copy of a game as its own row with its own picker, that a post moves only the copy it
/// names, and that a collection entry that does not belong to the posted game is refused.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerPerCopyLocationTests
{
    private const int QuarryGameId = 5101;
    private const int HarbourGameId = 5102;
    private const string CabinetId = "a1b2c3d4";
    private const string ShelfId = "e5f6a7b8";
    private const string LocationsPath = "/owner/locations";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly long[] QuarryCopies = [7001, 7002, 7003];
    private const long HarbourCopy = 7101;

    [Fact]
    public async Task Posting_a_copy_places_only_that_copy()
    {
        using var storage = Seed();
        await using var factory = FactoryOn(storage);
        using var client = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);
        var page = await session.GetAsync(LocationsPath);

        var placed = await session.PostFormAsync(
            $"{LocationsPath}?handler=Assign",
            [
                Field("gameId", QuarryGameId),
                Field("collectionId", QuarryCopies[1]),
                Field("locationId", ShelfId),
                Field("revision", OwnerSession.FieldValue(page.Body, "revision")),
            ]);
        var locations = await ReadLocations(client);

        placed.Status.Should().Be(HttpStatusCode.Found);
        placed.Location.Should().Be($"{LocationsPath}?done=saved#game-{QuarryGameId}-{QuarryCopies[1]}");
        locations[QuarryCopies[0]].Should().Be("Cabinet A");
        locations[QuarryCopies[1]].Should().Be("Shelf B");
        locations[QuarryCopies[2]].Should().Be("Cabinet A");
    }

    [Fact]
    public async Task A_foreign_or_missing_collection_id_is_refused()
    {
        using var storage = Seed();
        await using var factory = FactoryOn(storage);
        using var session = new OwnerSession(factory);
        var owner = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var before = owner.Revision;

        var foreign = await session.PostJsonAsync(
            $"{LocationsPath}?handler=Assign",
            [Field("gameId", QuarryGameId), Field("collectionId", HarbourCopy), Field("locationId", ShelfId), Field("revision", before)]);
        var missing = await session.PostJsonAsync(
            $"{LocationsPath}?handler=Assign",
            [Field("gameId", QuarryGameId), Field("locationId", ShelfId), Field("revision", before)]);

        foreign.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        foreign.JsonText("status").Should().Be("unknown-game");
        missing.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        missing.JsonText("status").Should().Be("unknown-game");
        owner.Revision.Should().Be(before);
    }

    [Fact]
    public async Task Each_copy_has_its_own_row_tag_and_picker_name()
    {
        using var storage = Seed();
        await using var factory = FactoryOn(storage);
        using var session = new OwnerSession(factory);

        var page = await session.GetAsync(LocationsPath);

        Regex.Matches(page.Body, "<li class=\"game-row\"").Should().HaveCount(4);
        Regex.Matches(page.Body, "<span class=\"pic-tag pic-tag-plain\" dir=\"auto\">([^<]+)</span>")
            .Select(match => match.Groups[1].Value)
            .Should().Equal("Copy 1 of 3", "Copy 2 of 3", "Copy 3 of 3");
        page.Body.Should().Contain("aria-label=\"Location for Quiet Quarry, copy 2 of 3\"");
        page.Body.Should().Contain("aria-label=\"Location for Hollow Harbour\"");
        page.Body.Should().NotContain("Copy 1 of 1");
        Regex.Matches(page.Body, "name=\"collectionId\" value=\"(\\d+)\"")
            .Select(match => match.Groups[1].Value)
            .Should().BeEquivalentTo(["7001", "7002", "7003", "7101"]);
    }

    [Fact]
    public async Task The_cabinet_page_counts_copies_that_are_not_placed()
    {
        using var storage = Seed();
        await using var factory = FactoryOn(storage);
        using var session = new OwnerSession(factory);
        var before = await session.GetAsync("/");
        var page = await session.GetAsync(LocationsPath);

        var cleared = await session.PostFormAsync(
            $"{LocationsPath}?handler=Assign",
            [
                Field("gameId", QuarryGameId),
                Field("collectionId", QuarryCopies[2]),
                Field("locationId", string.Empty),
                Field("revision", OwnerSession.FieldValue(page.Body, "revision")),
            ]);
        var after = await session.GetAsync("/");

        cleared.Status.Should().Be(HttpStatusCode.Found);
        before.Body.Should().Contain("1 game is not placed yet.");
        after.Body.Should().Contain("2 games are not placed yet.");
    }

    private static TemporaryDirectory Seed()
    {
        var storage = new TemporaryDirectory();
        var items = QuarryCopies
            .Select(copy => new SnapshotItem(copy, QuarryGameId, "Quiet Quarry", ItemKind.Base, 2015, null, null))
            .Append(new SnapshotItem(HarbourCopy, HarbourGameId, "Hollow Harbour", ItemKind.Base, 2018, null, null));

        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [.. items],
            null,
            null));
        new OwnerDataStore(storage.FullPath, NullLogger<OwnerDataStore>.Instance).Save(OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetId, "Cabinet A"), new OwnerLocation(ShelfId, "Shelf B")],
            Games = [new OwnerGame(QuarryGameId, CabinetId, null, null)],
        });

        return storage;
    }

    private static CabinetWebApplicationFactory FactoryOn(TemporaryDirectory storage) =>
        new(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });

    private static KeyValuePair<string, string> Field(string name, long value) =>
        new(name, value.ToString(CultureInfo.InvariantCulture));

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static async Task<Dictionary<long, string?>> ReadLocations(HttpClient client)
    {
        using var response = await client.GetAsync("/cabinet/cards?profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(card => card.GetProperty("entryId").GetInt64(), card => card.TryGetProperty("location", out var location) ? location.GetString() : null);
    }
}
