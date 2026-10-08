using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;

namespace Cabinet.UnitTests.Layout;

/// <summary>How full a cabinet is: the number of sections and, for every section but the last, its shelf rows holding nothing.</summary>
/// <param name="Sections">The number of sections.</param>
/// <param name="EmptyRows">For every section but the last, the shelf rows in which no cubby holds a placement.</param>
public sealed record DensityReport(int Sections, IReadOnlyList<int> EmptyRows)
{
    /// <summary>The largest number of empty rows in any section but the last; zero when there is only one section.</summary>
    public int MaxEmptyRows => EmptyRows.Count == 0 ? 0 : EmptyRows.Max();

    /// <summary>A one-line description for test output.</summary>
    public override string ToString() =>
        $"sections {Sections}, max empty rows {MaxEmptyRows}, empty rows per earlier section [{string.Join(", ", EmptyRows)}]";
}

/// <summary>
/// Measures how sparse a layout is and builds the collection the measure runs on besides the samples: a seeded
/// collection whose boxes follow the spread of sizes recorded for real games.
/// </summary>
public static class LayoutDensity
{
    private const ulong SeedMask = 0xB0C5A1ECUL;
    private const int BaseDefaultPercent = 30;
    private const int ExpansionDefaultPercent = 40;
    private const int ExpansionPercent = 15;
    private const int PercentRange = 100;
    private const int BaseWidthMinMm = 94;
    private const int BaseWidthMaxMm = 318;
    private const int BaseLengthMinMm = 115;
    private const int BaseLengthMaxMm = 432;
    private const int BaseDepthMinMm = 20;
    private const int BaseDepthMaxMm = 192;
    private const int ExpansionWidthMinMm = 192;
    private const int ExpansionWidthMaxMm = 295;
    private const int ExpansionLengthMinMm = 254;
    private const int ExpansionLengthMaxMm = 295;
    private const int ExpansionDepthMinMm = 39;
    private const int ExpansionDepthMaxMm = 80;
    private const int BigBoxWidthMm = 300;
    private const string SamplePrefix = "sample-";
    private const string SpikePrefix = "spike-";
    private const string MixPrefix = "mix-";

    /// <summary>Counts the sections and, for every section but the last, the shelf rows without a placement.</summary>
    /// <param name="layout">The layout to measure.</param>
    public static DensityReport Measure(CabinetLayout layout)
    {
        var empty = layout.Sections
            .SkipLast(1)
            .Select(section => section.Cubbies
                .GroupBy(cubby => cubby.YMm)
                .Count(row => row.All(cubby => cubby.Placements.Count == 0)))
            .ToList();

        return new DensityReport(layout.Sections.Count, empty);
    }

    /// <summary>
    /// Counts, for every section, the shelf rows without a placement that sit above a row that holds one: a bare row in the
    /// middle of a drawn section. The rows below the last used row of the last section are not drawn, so they never count.
    /// </summary>
    /// <param name="layout">The layout to measure.</param>
    public static IReadOnlyList<int> HollowRows(CabinetLayout layout) =>
        layout.Sections
            .Select(section =>
            {
                var rows = section.Cubbies
                    .GroupBy(cubby => cubby.YMm)
                    .OrderBy(row => row.Key)
                    .Select(row => row.Sum(cubby => cubby.Placements.Count))
                    .ToList();
                var lastUsed = rows.FindLastIndex(count => count > 0);

                return rows.Take(Math.Max(0, lastUsed)).Count(count => count == 0);
            })
            .ToList();

    /// <summary>The number of placements in every section, first to last.</summary>
    /// <param name="layout">The layout to measure.</param>
    public static IReadOnlyList<int> PlacementsPerSection(CabinetLayout layout) =>
        layout.Sections.Select(section => section.Cubbies.Sum(cubby => cubby.Placements.Count)).ToList();

    /// <summary>
    /// The collection a density test name stands for: <c>sample-65</c> and <c>sample-400</c> are the invented samples,
    /// <c>spike-65-3</c> a seeded collection with the recorded size ranges, <c>mix-400-12</c> a seeded realistic size mix;
    /// the number after the prefix is the item count and the one after it the seed.
    /// </summary>
    /// <param name="name">The test name.</param>
    public static IReadOnlyList<CabinetItem> ItemsFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.StartsWith(SamplePrefix, StringComparison.Ordinal))
        {
            SyntheticCollections.TryGetSample(name[SamplePrefix.Length..], out var sample);

            return sample;
        }

        var prefix = name.StartsWith(MixPrefix, StringComparison.Ordinal) ? MixPrefix : SpikePrefix;
        var parts = name[prefix.Length..].Split('-');
        var count = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        var seed = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

        return prefix == MixPrefix ? SyntheticCollections.SizeMix(seed, count) : SpikeShaped(seed, count);
    }

    /// <summary>
    /// How many big boxes stand in a section: a face-out or flat base game whose front, with the widest expansion layer of its
    /// family beside it, is at least 300 millimetres wide.
    /// </summary>
    /// <param name="section">The section to count in.</param>
    public static int BigBoxes(LayoutSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return section.Cubbies.Sum(cubby => cubby.Placements.Count(placement =>
            placement.Kind is PlacementKind.Cover or PlacementKind.FlatBox
            && placement.WidthMm + cubby.Placements
                .Where(other => other.Kind == PlacementKind.ExpansionLayer && other.FamilyId == placement.GameId)
                .Select(other => other.WidthMm)
                .DefaultIfEmpty(0)
                .Max() >= BigBoxWidthMm));
    }

    /// <summary>
    /// A seeded collection with a share of expansions in which thirty percent of the base games and forty percent of the
    /// expansions have the default box and the rest a box drawn from the recorded size ranges, the longer front side as
    /// the height.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <param name="count">The number of items.</param>
    public static IReadOnlyList<CabinetItem> SpikeShaped(int seed, int count)
    {
        var generator = new SplitMix64(unchecked((ulong)seed) ^ SeedMask);

        return SyntheticCollections.Random(seed, count, ExpansionPercent)
            .Select(item => item with { Box = BoxFor(generator, item.Kind) })
            .ToList();
    }

    private static BoxDimensions BoxFor(SplitMix64 generator, ItemKind kind)
    {
        var isExpansion = kind == ItemKind.Expansion;
        var usesDefault = generator.NextInt(0, PercentRange) < (isExpansion ? ExpansionDefaultPercent : BaseDefaultPercent);
        var first = isExpansion ? generator.NextInt(ExpansionWidthMinMm, ExpansionWidthMaxMm + 1) : generator.NextInt(BaseWidthMinMm, BaseWidthMaxMm + 1);
        var second = isExpansion ? generator.NextInt(ExpansionLengthMinMm, ExpansionLengthMaxMm + 1) : generator.NextInt(BaseLengthMinMm, BaseLengthMaxMm + 1);
        var depth = isExpansion ? generator.NextInt(ExpansionDepthMinMm, ExpansionDepthMaxMm + 1) : generator.NextInt(BaseDepthMinMm, BaseDepthMaxMm + 1);

        return usesDefault
            ? BoxFromVersion.DefaultFor(kind)
            : new BoxDimensions(Math.Min(first, second), Math.Max(first, second), depth);
    }
}
