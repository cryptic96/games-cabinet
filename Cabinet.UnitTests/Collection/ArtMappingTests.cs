using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>
/// Pins, over hand-built collections, which stored picture each game shows, which colour its boxes take and which edge
/// colours its cover is filled with, and that the choice follows the rules and not the stored records.
/// </summary>
[Trait("Category", "Enrichment")]
public sealed class ArtMappingTests
{
    private const string VersionUrl = "https://cf.example.org/version.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const string DetailsMainUrl = "https://cf.example.org/details-main.jpg";
    private const string VersionHash = "aaaaaaaaaaaaaaaa";
    private const string MainHash = "bbbbbbbbbbbbbbbb";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFeatures FlatFeatures = new(0.0, 1.0, 0.0, 0.0, 4);
    private static readonly ArtFeatures SlantedFeatures = new(0.59, 0.78, 0.83, 0.72, 0);
    private static readonly ArtFeatures BorderlineFeatures = new(0.01, 0.975, 0.14, 0.0, 3);
    private static readonly PaletteTone VersionColour = SpineColour.PairFor(new RgbColour(200, 30, 40));
    private static readonly PaletteTone MainColour = SpineColour.PairFor(new RgbColour(30, 90, 200));
    private static readonly ArtEdges VersionEdges = new("#111111", "#222222", "#333333", "#444444");
    private static readonly ArtEdges MainEdges = new("#aa0000", "#00aa00", "#0000aa", "#aaaa00");

    public static TheoryData<string, ArtFeatures?, ArtFeatures?, string?> ChooserRows => new()
    {
        { "version flat", FlatFeatures, SlantedFeatures, VersionHash },
        { "version slanted with a flat main picture", SlantedFeatures, FlatFeatures, MainHash },
        { "both slanted", SlantedFeatures, SlantedFeatures, VersionHash },
        { "version slanted with no main picture", SlantedFeatures, null, VersionHash },
        { "no version picture with a main picture", null, SlantedFeatures, MainHash },
        { "no picture at all", null, null, null },
    };

    [Theory]
    [MemberData(nameof(ChooserRows))]
    public void Each_row_of_the_choice_table_gives_the_expected_picture_and_colour(
        string row,
        ArtFeatures? version,
        ArtFeatures? main,
        string? expectedHash)
    {
        var item = Item(VersionUrl, MainUrl);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, version, VersionColour, VersionEdges), Recorded(MainUrl, MainHash, main, MainColour, MainEdges));

        var mapped = SnapshotMapper.ToCabinetItems(snapshot).Single();

        if (expectedHash is null)
        {
            mapped.Art.Should().BeNull(row);
            mapped.Colour.Should().BeNull(row);

            return;
        }

        mapped.Art!.Variants[0].Url.Should().Be($"/art/{expectedHash}-480.webp", row);
        mapped.Colour.Should().Be(expectedHash == VersionHash ? VersionColour : MainColour, row);
        mapped.Art.Edges.Should().Be(expectedHash == VersionHash ? VersionEdges : MainEdges, row);
    }

    [Fact]
    public void One_address_for_both_pictures_is_one_record_chosen_as_the_version_picture()
    {
        var item = Item(VersionUrl, VersionUrl);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, SlantedFeatures, VersionColour, VersionEdges));

        var mapped = SnapshotMapper.ToCabinetItems(snapshot).Single();

        mapped.Art!.Variants[0].Url.Should().Be($"/art/{VersionHash}-480.webp");
        mapped.Colour.Should().Be(VersionColour);
    }

    [Fact]
    public void A_stored_picture_without_measurements_counts_as_not_usable()
    {
        var item = Item(VersionUrl, MainUrl);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, null, VersionColour, VersionEdges), Recorded(MainUrl, MainHash, FlatFeatures, MainColour, MainEdges));

        var mapped = SnapshotMapper.ToCabinetItems(snapshot).Single();

        mapped.Art!.Variants[0].Url.Should().Be($"/art/{MainHash}-480.webp");
        SnapshotMapper.ToCabinetItems(Snapshot(item, null, Recorded(VersionUrl, VersionHash, null, VersionColour, VersionEdges))).Single().Art.Should().BeNull();
    }

    [Fact]
    public void The_main_picture_is_the_one_the_details_name_and_the_collection_picture_until_they_arrive()
    {
        var item = Item(null, MainUrl);
        var details = Details(DetailsMainUrl);
        var records = new[]
        {
            Recorded(MainUrl, VersionHash, FlatFeatures, VersionColour, VersionEdges),
            Recorded(DetailsMainUrl, MainHash, FlatFeatures, MainColour, MainEdges),
        };

        SnapshotMapper.ToCabinetItems(Snapshot(item, null, records)).Single().Art!.Variants[0].Url.Should().Be($"/art/{VersionHash}-480.webp");
        SnapshotMapper.ToCabinetItems(Snapshot(item, details, records)).Single().Art!.Variants[0].Url.Should().Be($"/art/{MainHash}-480.webp");
    }

    [Theory]
    [InlineData("#AABBCC", "#ffffff")]
    [InlineData("#abc", "#ffffff")]
    [InlineData("#f0f0f0", "#ffffff")]
    [InlineData("#1a1a1a", "#2a1a10")]
    [InlineData("red", "#ffffff")]
    public void A_stored_pair_that_is_not_valid_gives_no_colour_but_the_picture_is_still_used(string background, string text)
    {
        var item = Item(VersionUrl, null);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, FlatFeatures, new PaletteTone(background, text), VersionEdges));

        var mapped = SnapshotMapper.ToCabinetItems(snapshot).Single();

        mapped.Colour.Should().BeNull();
        mapped.Art.Should().NotBeNull();
    }

    [Theory]
    [InlineData("#AA0000", "#00aa00", "#0000aa", "#aaaa00")]
    [InlineData("#aa0000", "#0a0", "#0000aa", "#aaaa00")]
    [InlineData("#aa0000", "#00aa00", "blue", "#aaaa00")]
    [InlineData("#aa0000", "#00aa00", "#0000aa", "")]
    public void Edge_colours_with_any_invalid_value_leave_the_picture_without_edges(string top, string right, string bottom, string left)
    {
        var item = Item(VersionUrl, null);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, FlatFeatures, VersionColour, new ArtEdges(top, right, bottom, left)));

        var mapped = SnapshotMapper.ToCabinetItems(snapshot).Single();

        mapped.Art.Should().NotBeNull();
        mapped.Art!.Edges.Should().BeNull();
    }

    [Fact]
    public void Stricter_thresholds_move_a_borderline_edition_to_the_main_picture_without_any_stored_change()
    {
        var item = Item(VersionUrl, MainUrl);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, BorderlineFeatures, VersionColour, VersionEdges), Recorded(MainUrl, MainHash, FlatFeatures, MainColour, MainEdges));
        var strict = new ArtRules(ArtThresholds.Default with { FlatMinFill = 0.99 });

        var relaxed = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default);
        var stricter = SnapshotMapper.ToCabinetItems(snapshot, strict);

        relaxed.Single().Art!.Variants[0].Url.Should().Be($"/art/{VersionHash}-480.webp");
        stricter.Single().Art!.Variants[0].Url.Should().Be($"/art/{MainHash}-480.webp");
        SnapshotMapper.Version(relaxed, ArtRules.Default).Should().NotBe(SnapshotMapper.Version(stricter, strict));
    }

    [Fact]
    public void The_version_is_equal_for_equal_rules_and_differs_when_only_a_rule_differs()
    {
        var item = Item(VersionUrl, null);
        var snapshot = Snapshot(item, null, Recorded(VersionUrl, VersionHash, FlatFeatures, VersionColour, VersionEdges));
        var items = SnapshotMapper.ToCabinetItems(snapshot);
        var other = new ArtRules(ArtThresholds.Default with { ThreeDMinCorner = 0.5 });

        SnapshotMapper.Version(items, ArtRules.Default).Should().Be(SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(snapshot), ArtRules.Default));
        SnapshotMapper.Version(items, ArtRules.Default).Should().NotBe(SnapshotMapper.Version(items, other));
    }

    [Fact]
    public void The_version_changes_with_the_colour_pair_and_the_edge_colours_and_not_with_unrelated_record_details()
    {
        var item = Item(VersionUrl, null);
        string VersionWith(PaletteTone colour, ArtEdges edges, DateTimeOffset attemptedAt) => SnapshotMapper.Version(
            SnapshotMapper.ToCabinetItems(Snapshot(item, null, Recorded(VersionUrl, VersionHash, FlatFeatures, colour, edges) with { AttemptedAtUtc = attemptedAt })));

        var baseline = VersionWith(VersionColour, VersionEdges, Moment);

        VersionWith(MainColour, VersionEdges, Moment).Should().NotBe(baseline);
        VersionWith(VersionColour, MainEdges, Moment).Should().NotBe(baseline);
        VersionWith(VersionColour, VersionEdges, Moment.AddDays(3)).Should().Be(baseline);
    }

    [Fact]
    public void Every_kind_of_box_except_the_marker_carries_the_colour_of_its_game()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var coloured = items.Select(item => item with { Colour = ColourFor(item.BggId) }).ToList();
        var options = LayoutOptions.Default with { CoverSharePercent = 30 };

        var layout = CabinetLayoutEngine.Build(coloured, SectionDesigns.Desktop, options);
        var placements = layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).ToList();

        placements.Select(placement => placement.Kind).Distinct().Should().Contain(
        [
            PlacementKind.Cover,
            PlacementKind.Spine,
            PlacementKind.ExpansionLayer,
            PlacementKind.ExpansionSpine,
            PlacementKind.MoreMarker,
        ]);
        placements.Where(placement => placement.Kind != PlacementKind.MoreMarker)
            .Should().OnlyContain(placement => placement.Colour == ColourFor(placement.GameId));
        placements.Where(placement => placement.Kind == PlacementKind.MoreMarker).Should().OnlyContain(placement => placement.Colour == null);
    }

    [Fact]
    public void A_game_without_a_colour_gives_its_boxes_none()
    {
        SyntheticCollections.TryGetSample("65", out var items);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);

        layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements)
            .Should().OnlyContain(placement => placement.Colour == null);
    }

    private static PaletteTone ColourFor(int bggId) => SpineColour.PairFor(new RgbColour((byte)(bggId % 200), (byte)((bggId * 7) % 200), (byte)((bggId * 13) % 200)));

    private static SnapshotItem Item(string? versionImage, string? mainImage) =>
        new(1, 2, "Invented Game", ItemKind.Base, null, null, null, versionImage, mainImage);

    private static GameDetails Details(string? mainImage) =>
        new(Moment, null, null, null, null, null, null, null, null, null, [], [], [], mainImage);

    private static ImageRecord Recorded(string address, string hash, ArtFeatures? features, PaletteTone colour, ArtEdges edges) =>
        new(address, ImageStatus.Ok, Moment, [new ArtFile(480, 640, $"{hash}-480.webp")], features, "#808080", colour, edges, 1);

    private static CollectionSnapshot Snapshot(SnapshotItem item, GameDetails? details, params ImageRecord[] records) =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [item],
            records.ToDictionary(record => record.SourceUrl, StringComparer.Ordinal),
            details is null ? null : new Dictionary<int, GameDetails> { [item.GameId] = details });
}
