using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using Cabinet.FakeBgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Checks the series and family rules on many invented collections, at every cover share from none to all and on both
/// section designs: every series stands as one block or runs on from cubby to cubby in reading order without leaving a
/// cubby out, every family keeps to its own cubby and the next one on the same shelf row with every expansion counted
/// once, and nothing overlaps. The one allowed gap is a series whose games cannot stand side by side even on their own in
/// an empty cabinet, because two of them only fit cubbies that are not neighbours; such a series is laid out alone to
/// prove it, and these cases stay rare.
/// </summary>
public class SeriesInvariantTests
{
    private const int MediumMixSeeds = 30;
    private const int LargeMixSeeds = 4;
    private const int RandomSeeds = 10;
    private const int SeriesPerForcedGap = 10;

    private static readonly int[] Shares = [0, 25, 33, 50, 100];

    public static TheoryData<string, int> ProfilesAndShares
    {
        get
        {
            var data = new TheoryData<string, int>();

            foreach (var design in SectionDesigns.All)
            {
                foreach (var share in Shares)
                {
                    data.Add(design.Name, share);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ProfilesAndShares))]
    [Trait("Category", "Layout")]
    public void Series_never_skip_a_cubby_and_families_keep_to_their_limits_at_any_cover_share(string profile, int share)
    {
        SectionDesigns.TryGet(profile, out var design).Should().BeTrue();
        var options = LayoutOptions.Default with { CoverSharePercent = share };
        var seriesCount = 0;
        var forced = new List<string>();

        foreach (var (name, items) in Collections())
        {
            var layout = CabinetLayoutEngine.Build(items, design!, options);

            LayoutAssertions.AssertValid(layout, items);

            foreach (var series in LayoutAssertions.SeriesWithGaps(layout, items, design!))
            {
                var alone = LayoutAssertions.WithOwnExpansions(series, items);
                var isolated = CabinetLayoutEngine.Build(alone, design!, options with { FewGamesThreshold = 0 });

                LayoutAssertions.SeriesGaps(isolated, alone, design!).Should().NotBeEmpty(
                    "{0} on {1} at {2} percent: a series leaves a cubby out only when it cannot run on even alone in an empty cabinet, but {3}",
                    name, profile, share, string.Join("; ", LayoutAssertions.SeriesGaps(layout, items, design!)));
                forced.Add(name);
            }

            seriesCount += SeriesCount(items);
        }

        seriesCount.Should().BeGreaterThan(2 * MediumMixSeeds, "the collections hold many series");
        (forced.Count * SeriesPerForcedGap).Should().BeLessThanOrEqualTo(
            seriesCount, "a series that cannot stand side by side even alone is rare: {0} of {1} ({2})", forced.Count, seriesCount, string.Join(", ", forced));
    }

    private static int SeriesCount(IReadOnlyList<CabinetItem> items)
    {
        var topLevel = items
            .Where(item => item.Kind == ItemKind.Base || LayoutAssertions.OwnedParentOf(item, items) is null)
            .OrderBy(item => item.CollectionId)
            .ThenBy(item => item.BggId)
            .ToList();

        return SeriesGrouping.Group(topLevel).Count(group => group.Indices.Count > 1);
    }

    private static IEnumerable<(string Name, IReadOnlyList<CabinetItem> Items)> Collections()
    {
        foreach (var sample in new[] { "65", "400" })
        {
            SyntheticCollections.TryGetSample(sample, out var items);

            yield return ($"sample-{sample}", items);
        }

        foreach (var size in new[] { 65, 400 })
        {
            yield return ($"fake-{size}", FakeCollectionItems.Map(SyntheticBggCollection.Create(size)));
        }

        for (var seed = 1; seed <= MediumMixSeeds; seed++)
        {
            yield return ($"mix-65-{seed}", SyntheticCollections.SizeMix(seed, 65));
        }

        for (var seed = 1; seed <= LargeMixSeeds; seed++)
        {
            yield return ($"mix-400-{seed}", SyntheticCollections.SizeMix(seed, 400));
        }

        for (var seed = 1; seed <= RandomSeeds; seed++)
        {
            yield return ($"random-{seed}-150-20", SyntheticCollections.Random(seed, 150, 20));
        }
    }
}
