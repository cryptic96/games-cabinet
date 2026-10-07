using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves which stored picture an item gets and that the collection version follows it.</summary>
[Trait("Category", "Snapshot")]
public sealed class SnapshotMapperArtTests
{
    private const string VersionUrl = "https://cf.example.org/version.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_version_picture_wins_and_its_sizes_are_listed_widest_first_as_paths_on_the_site()
    {
        var snapshot = Snapshot(
            Item(VersionUrl, MainUrl),
            Ok(MainUrl, "1111111111111111", 480),
            Ok(VersionUrl, "2222222222222222", 240, 480));

        var art = SnapshotMapper.ToCabinetItems(snapshot).Single().Art;

        art.Should().NotBeNull();
        art!.Variants.Select(variant => variant.Url).Should().Equal(
            "/art/2222222222222222-480.webp",
            "/art/2222222222222222-240.webp");
        art.Variants.Select(variant => variant.Width).Should().Equal(480, 240);
    }

    [Fact]
    public void The_main_picture_is_used_when_the_version_picture_is_missing_or_did_not_work()
    {
        var withoutVersion = Snapshot(Item(null, MainUrl), Ok(MainUrl, "1111111111111111", 480));
        var failedVersion = Snapshot(
            Item(VersionUrl, MainUrl),
            new ImageRecord(VersionUrl, ImageStatus.Failed, Moment),
            Ok(MainUrl, "1111111111111111", 480));

        SnapshotMapper.ToCabinetItems(withoutVersion).Single().Art!.Variants[0].Url.Should().Be("/art/1111111111111111-480.webp");
        SnapshotMapper.ToCabinetItems(failedVersion).Single().Art!.Variants[0].Url.Should().Be("/art/1111111111111111-480.webp");
    }

    [Fact]
    public void An_item_with_no_stored_picture_has_no_art()
    {
        SnapshotMapper.ToCabinetItems(Snapshot(Item(VersionUrl, MainUrl))).Single().Art.Should().BeNull();
        SnapshotMapper.ToCabinetItems(Snapshot(Item(null, null))).Single().Art.Should().BeNull();
        SnapshotMapper.ToCabinetItems(Snapshot(Item(VersionUrl, null), new ImageRecord(VersionUrl, ImageStatus.Ok, Moment, []))).Single().Art.Should().BeNull();
    }

    [Fact]
    public void The_collection_version_changes_when_a_picture_arrives_or_is_replaced_and_not_otherwise()
    {
        var without = Version(Snapshot(Item(VersionUrl, null)));
        var first = Version(Snapshot(Item(VersionUrl, null), Ok(VersionUrl, "1111111111111111", 240)));
        var same = Version(Snapshot(Item(VersionUrl, null), Ok(VersionUrl, "1111111111111111", 240)));
        var replaced = Version(Snapshot(Item(VersionUrl, null), Ok(VersionUrl, "3333333333333333", 240)));
        var failedRecordOnly = Version(Snapshot(Item(VersionUrl, null), new ImageRecord(VersionUrl, ImageStatus.Failed, Moment)));

        first.Should().NotBe(without);
        same.Should().Be(first);
        replaced.Should().NotBe(first);
        failedRecordOnly.Should().Be(without);
    }

    private static string Version(CollectionSnapshot snapshot) => SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(snapshot));

    private static SnapshotItem Item(string? versionImage, string? mainImage) =>
        new(1, 2, "Invented Game", ItemKind.Base, null, null, null, versionImage, mainImage);

    private static ImageRecord Ok(string address, string hash, params int[] widths) =>
        new(address, ImageStatus.Ok, Moment, [.. widths.Select(width => new ArtFile(width, width * 4 / 3, $"{hash}-{width}.webp"))]);

    private static CollectionSnapshot Snapshot(SnapshotItem item, params ImageRecord[] records) =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [item], records.ToDictionary(record => record.SourceUrl, StringComparer.Ordinal));
}
