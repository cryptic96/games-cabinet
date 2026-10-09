using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using FluentAssertions;

namespace Cabinet.UnitTests.Images;

/// <summary>
/// Proves that every invented picture keeps its verdict, and the fake collection's review cases keep their picks, when the
/// measurements are taken the way a stored picture is measured: decoded from its bytes and analysed.
/// </summary>
[Trait("Category", "Images")]
public sealed class ReviewCaseVerdictTests
{
    private static readonly ArtLimits Limits = new(12_000_000, 36_000_000, TimeSpan.FromSeconds(30));

    private static readonly IReadOnlySet<SyntheticArtKind> FlatKinds = new HashSet<SyntheticArtKind>
    {
        SyntheticArtKind.FlatCover,
        SyntheticArtKind.FlatWide,
        SyntheticArtKind.FlatNarrow,
        SyntheticArtKind.Banner,
        SyntheticArtKind.WhiteFramed,
        SyntheticArtKind.BlackFramed,
        SyntheticArtKind.AllWhite,
        SyntheticArtKind.NearBlack,
        SyntheticArtKind.MidGreen,
        SyntheticArtKind.GradientFullBleed,
        SyntheticArtKind.DarkBorderCover,
        SyntheticArtKind.CoverColouredField,
        SyntheticArtKind.CoverOnBlackIrregular,
        SyntheticArtKind.CoverOnBlackScattered,
        SyntheticArtKind.CoverColourFramed,
        SyntheticArtKind.CoverLightEdge,
    };

    public static TheoryData<SyntheticArtKind> DecodableKinds()
    {
        var kinds = new TheoryData<SyntheticArtKind>();
        foreach (var kind in SyntheticArt.All.Where(kind => kind != SyntheticArtKind.Undecodable))
        {
            kinds.Add(kind);
        }

        return kinds;
    }

    [Theory]
    [MemberData(nameof(DecodableKinds))]
    public void Every_fixture_keeps_its_verdict_through_the_stored_picture_path(SyntheticArtKind kind)
    {
        VerdictOf(kind).Should().Be(FlatKinds.Contains(kind) ? ArtVerdict.Flat : ArtVerdict.ThreeD);
    }

    [Fact]
    public void The_review_cases_of_the_fake_collection_keep_their_picks()
    {
        var expected = new Dictionary<int, ArtPick>
        {
            [7] = ArtPick.MainImage,
            [20] = ArtPick.MainImage,
            [16] = ArtPick.GeneratedCover,
            [17] = ArtPick.GeneratedCover,
        };
        foreach (var position in new[] { 0, 1, 6, 8, 10, 11, 12, 13, 15, 18, 21, 23 })
        {
            expected[position] = ArtPick.VersionImage;
        }

        var items = SyntheticBggCollection.Create(65);

        foreach (var (position, pick) in expected)
        {
            var item = items.Single(candidate => candidate.CollId == SyntheticBggCollection.FirstCollId + position);
            var version = VerdictOf(SyntheticBggCollection.ArtFor(item, version: true));
            var main = VerdictOf(SyntheticBggCollection.ArtFor(item, version: false));

            ArtChooser.Choose(version, main).Should().Be(pick, "review case {0}", position);
        }
    }

    private static ArtVerdict? VerdictOf(SyntheticArtKind? kind)
    {
        if (kind is null)
        {
            return null;
        }

        return ArtProcessor.Process(SyntheticArt.Encode(kind.Value), Limits) is ArtProcessing.Done done
            ? ArtVerdicts.Classify(done.Facts.Features, ArtThresholds.Default)
            : null;
    }
}
