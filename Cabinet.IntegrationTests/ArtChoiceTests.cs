using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Images;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves, through a booted host with scripted answers, that each game gets the picture the choice rules name, that every
/// box of the game takes the colour stored with that picture, and that a picture measured by an older analysis is measured
/// again.
/// </summary>
[Trait("Category", "Sync")]
public sealed class ArtChoiceTests
{
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private const string ImageHost = "https://example.org/images/";

    private static readonly Dictionary<string, string?> AllowExampleHost = new()
    {
        ["Images:AllowedHosts"] = "example.org",
        ["Images:MaxDownloadsPerRun"] = "1000",
    };

    private static readonly byte[] SlantedOnWhite = SyntheticArt.Encode(SyntheticArtKind.BoxOnWhite);
    private static readonly byte[] SlantedOnBlack = SyntheticArt.Encode(SyntheticArtKind.BoxOnBlack);
    private static readonly byte[] FlatCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);

    [Fact]
    public async Task A_slanted_owned_edition_gives_way_to_its_flat_main_cover_and_every_box_takes_the_chosen_pictures_colour()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(65);
        var images = ScriptPictures(items);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(items),
            clock,
            WithStorage(storage),
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var layout = await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken);
        var records = StoredRecords(storage);
        using var document = JsonDocument.Parse(layout);
        var checkedGames = new HashSet<int>();

        foreach (var placement in Placements(document).Where(placement => placement.GetProperty("kind").GetString() != "moreMarker"))
        {
            var item = items.Single(candidate => candidate.CollId == placement.GetProperty("entryId").GetInt64());
            var chosenUrl = ExpectedPictureUrl(item);

            if (chosenUrl is null)
            {
                placement.TryGetProperty("art", out _).Should().BeFalse("a game without a usable picture has no art");
                placement.TryGetProperty("colour", out _).Should().BeFalse("a game without a usable picture keeps its palette tone");

                continue;
            }

            var record = records[chosenUrl];
            placement.GetProperty("colour").GetProperty("background").GetString().Should().Be(record.GetProperty("colour").GetProperty("background").GetString());
            placement.GetProperty("colour").GetProperty("text").GetString().Should().Be(record.GetProperty("colour").GetProperty("text").GetString());

            if (placement.TryGetProperty("art", out var art))
            {
                var names = record.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("name").GetString()).ToList();
                names.Should().Contain(Path.GetFileName(art.GetProperty("url").GetString()));
                art.GetProperty("edges").GetProperty("top").GetString().Should().Be(record.GetProperty("edges").GetProperty("top").GetString());
            }

            checkedGames.Add(item.ObjectId);
        }

        checkedGames.Should().HaveCountGreaterThan(10);
        Placements(document).Any(placement => Has(placement, "art")).Should().BeTrue("some cover shows a picture");
        layout.Should().NotContain("example.org").And.NotContain("geekdo").And.NotContain("boardgamegeek");
    }

    [Fact]
    public async Task A_game_whose_only_picture_is_a_slanted_edition_uses_it_and_a_game_whose_pictures_all_fail_has_none()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(65);
        var onlySlanted = items.First(item => item.Owned && !item.IsExpansion && item.Version is not null && item.ObjectId % 3 == 1);
        var nothing = items.First(item => item.Owned && !item.IsExpansion && item.ObjectId % 3 == 2);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(items),
            clock,
            WithStorage(storage),
            ScriptPictures(items));
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var document = JsonDocument.Parse(await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken));
        var records = StoredRecords(storage);
        var own = Placements(document).Where(placement => placement.GetProperty("entryId").GetInt64() == onlySlanted.CollId).ToList();
        var bare = Placements(document).Where(placement => placement.GetProperty("entryId").GetInt64() == nothing.CollId).ToList();

        records.Should().ContainKey(VersionUrl(onlySlanted));
        own.Should().NotBeEmpty();
        own.All(placement => Has(placement, "colour")).Should().BeTrue();
        own.First().GetProperty("colour").GetProperty("background").GetString()
            .Should().Be(records[VersionUrl(onlySlanted)].GetProperty("colour").GetProperty("background").GetString());
        bare.Should().NotBeEmpty();
        bare.Any(placement => Has(placement, "art") || Has(placement, "colour")).Should().BeFalse();
    }

    [Fact]
    public async Task A_picture_stored_by_an_older_analysis_is_downloaded_and_measured_again_and_then_carries_the_current_version()
    {
        using var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var images = ScriptPictures(items);
        var clock = SyncHarness.NewClock();

        await using (var first = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, WithStorage(storage), images))
        {
            using var client = first.CreatePublicClient();
            await SyncRounds.PressAndWait(client, clock, advance: false);
        }

        var snapshotPath = Path.Combine(storage.FullPath, "snapshot.json");
        var stored = await File.ReadAllTextAsync(snapshotPath, TestContext.Current.CancellationToken);
        var current = $"\"analysisVersion\":{ArtProcessor.AnalysisVersion}";
        var older = $"\"analysisVersion\":{ArtProcessor.AnalysisVersion - 1}";
        stored.Should().Contain(current);
        await File.WriteAllTextAsync(
            snapshotPath,
            stored.Replace(current, older, StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        var secondImages = ScriptPictures(items);
        var secondClock = SyncHarness.NewClock();
        await using var second = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), secondClock, WithStorage(storage), secondImages);
        using var secondClient = second.CreatePublicClient();
        await SyncRounds.PressAndWait(secondClient, secondClock);

        secondImages.Requests.Should().NotBeEmpty("every picture measured by an older analysis is fetched again");
        StoredRecords(storage).Values
            .Where(record => record.GetProperty("status").GetString() == "ok")
            .Should().OnlyContain(record => record.GetProperty("analysisVersion").GetInt32() == ArtProcessor.AnalysisVersion);
    }

    private static Dictionary<string, string?> WithStorage(TemporaryDirectory storage) =>
        new(AllowExampleHost) { ["Storage:Directory"] = storage.FullPath };

    private static bool Has(JsonElement element, string name) => element.TryGetProperty(name, out _);

    private static string VersionUrl(FakeBggItem item) => $"{ImageHost}version-{BggXml.VersionId(item)}.jpg";

    private static string MainUrl(FakeBggItem item) => $"{ImageHost}{item.ObjectId}.jpg";

    private static string? ExpectedPictureUrl(FakeBggItem item) => (item.ObjectId % 3) switch
    {
        0 => MainUrl(item),
        1 when item.Version is not null => VersionUrl(item),
        _ => null,
    };

    private static ScriptedImageHandler ScriptPictures(IReadOnlyList<FakeBggItem> items)
    {
        var images = new ScriptedImageHandler();

        foreach (var item in items.Where(item => item.ObjectId % 3 == 0))
        {
            images.Serve(MainUrl(item), FlatCover);

            if (item.Version is not null)
            {
                images.Serve(VersionUrl(item), SlantedOnWhite);
            }
        }

        foreach (var item in items.Where(item => item.ObjectId % 3 == 1 && item.Version is not null))
        {
            images.Serve(VersionUrl(item), SlantedOnBlack);
        }

        return images;
    }

    private static Dictionary<string, JsonElement> StoredRecords(TemporaryDirectory storage)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage.FullPath, "snapshot.json")));

        return document.RootElement.GetProperty("images").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
    }

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());
}
