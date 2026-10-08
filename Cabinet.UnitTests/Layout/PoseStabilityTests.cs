using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Proves that how a box stands never depends on how its picture is judged: rules that flip every verdict change the boxes
/// that are drawn, and never the pose chosen for any game.
/// </summary>
[Trait("Category", "Layout")]
public sealed class PoseStabilityTests
{
    private const string AddressPrefix = "https://cf.example.org/stability-";
    private const double MillimetresPerInch = 25.4;
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFeatures FlatFeatures = new(0.0, 1.0, 0.0, 0.0, 4);
    private static readonly ArtFeatures SlantedFeatures = new(0.5, 0.7, 0.5, 0.5, 2);

    private static readonly ArtThresholds EveryPictureFlat = ArtThresholds.Default with { DegenerateBackdropShare = 0 };
    private static readonly ArtThresholds EveryPictureThreeD = new(1.5, -1, 1.0, 0.0, 2.0);
    private static readonly ArtThresholds EveryPictureUnsure = new(1.5, -1, -1, 2.0, 2.0);

    public static TheoryData<string> Samples => new("65", "400");

    [Theory]
    [MemberData(nameof(Samples))]
    public void Flipping_every_verdict_changes_boxes_and_never_a_pose(string sample)
    {
        var snapshot = SnapshotOf(sample);
        var rulesets = new[]
        {
            ArtRules.Default,
            new ArtRules(EveryPictureFlat),
            new ArtRules(EveryPictureThreeD),
            new ArtRules(EveryPictureUnsure),
        };
        var mapped = rulesets.Select(rules => SnapshotMapper.ToCabinetItems(snapshot, rules)).ToList();

        mapped.Select(items => items.Select(item => item.Box).ToList()).Distinct(new SequenceComparer())
            .Should().HaveCountGreaterThan(1, "the verdicts shape the drawn boxes");

        foreach (var strategy in Enum.GetValues<CoverStrategy>())
        {
            var options = new LayoutOptions(25, strategy, 6, 12, true);

            foreach (var design in SectionDesigns.All)
            {
                var reference = PosesOf(mapped[0], options, design);

                foreach (var other in mapped.Skip(1))
                {
                    PosesOf(other, options, design).Should().Equal(reference, "{0} on {1}", strategy, design.Name);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Turning_boxes_by_their_pictures_never_changes_a_pose(string sample)
    {
        var snapshot = TurnedPicturesSnapshotOf(sample);
        var turning = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default);
        var upright = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default with { OrientFromCover = false });

        turning.Select(item => item.Box).Should().NotEqual(upright.Select(item => item.Box), "turning changes the drawn boxes");
        turning.Select(item => item.PoseHeightMm).Should().Equal(upright.Select(item => item.PoseHeightMm));

        foreach (var strategy in Enum.GetValues<CoverStrategy>())
        {
            var options = new LayoutOptions(25, strategy, 6, 12, true);

            foreach (var design in SectionDesigns.All)
            {
                PosesOf(turning, options, design).Should().Equal(PosesOf(upright, options, design), "{0} on {1}", strategy, design.Name);
            }
        }
    }

    private static List<(int BggId, BoxPose Pose)> PosesOf(IReadOnlyList<CabinetItem> items, LayoutOptions options, SectionDesign design) =>
        [.. items.Select(item => (item.BggId, Orientation.Decide(item, options, design, fewGames: false)))];

    private static CollectionSnapshot TurnedPicturesSnapshotOf(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var source).Should().BeTrue();

        var items = new List<SnapshotItem>();
        var images = new Dictionary<string, ImageRecord>();

        foreach (var item in source)
        {
            var address = $"{AddressPrefix}turned-{item.CollectionId}.jpg";
            var dimensions = new VersionDimensions(item.Box.WidthMm / MillimetresPerInch, item.Box.HeightMm / MillimetresPerInch, item.Box.DepthMm / MillimetresPerInch);
            var turnedFront = new ArtFile(item.Box.HeightMm, item.Box.WidthMm, "turned.webp");

            items.Add(new SnapshotItem(item.CollectionId, item.BggId, item.Title, item.Kind, null, dimensions, null, address));
            images[address] = new ImageRecord(address, ImageStatus.Ok, Moment, [turnedFront], FlatFeatures);
        }

        return new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, Moment, items, images);
    }

    private static CollectionSnapshot SnapshotOf(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var source).Should().BeTrue();

        var items = new List<SnapshotItem>();
        var images = new Dictionary<string, ImageRecord>();

        for (var index = 0; index < source.Count; index++)
        {
            var item = source[index];
            var address = $"{AddressPrefix}{item.CollectionId}.jpg";
            var dimensions = index % 4 < 2
                ? new VersionDimensions(item.Box.WidthMm / MillimetresPerInch, item.Box.HeightMm / MillimetresPerInch, item.Box.DepthMm / MillimetresPerInch)
                : null;
            var cover = index % 3 == 0 ? new ArtFile(1000, 500, "wide.webp") : new ArtFile(500, 1000, "narrow.webp");

            items.Add(new SnapshotItem(item.CollectionId, item.BggId, item.Title, item.Kind, null, dimensions, null, address));
            images[address] = new ImageRecord(
                address,
                ImageStatus.Ok,
                Moment,
                [cover],
                index % 2 == 0 ? FlatFeatures : SlantedFeatures);
        }

        return new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, Moment, items, images);
    }

    private sealed class SequenceComparer : IEqualityComparer<List<BoxDimensions>>
    {
        public bool Equals(List<BoxDimensions>? x, List<BoxDimensions>? y) => x!.SequenceEqual(y!);

        public int GetHashCode(List<BoxDimensions> obj) => obj.Aggregate(0, (hash, box) => HashCode.Combine(hash, box));
    }
}
