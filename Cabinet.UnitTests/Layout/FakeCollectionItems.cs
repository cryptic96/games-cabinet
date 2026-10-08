using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Turns the invented BGG collections of the fake into the items the layout engine takes, so the layout tests can run on
/// the collections a local run shows. Only owned entries are kept; sizes come from the invented versions, with the
/// engine's own default for an entry without one.
/// </summary>
internal static class FakeCollectionItems
{
    private const double MillimetresPerInch = 25.4;

    /// <summary>Maps the owned entries of a fake collection, in collection order.</summary>
    /// <param name="entries">The fake collection.</param>
    public static IReadOnlyList<CabinetItem> Map(IReadOnlyList<FakeBggItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var titles = entries
            .GroupBy(entry => entry.ObjectId)
            .ToDictionary(group => group.Key, group => group.First().Title);

        return
        [
            .. entries
                .Where(entry => entry.Owned)
                .Select(entry => new CabinetItem(
                    entry.ObjectId,
                    entry.CollId,
                    entry.Title,
                    entry.IsExpansion ? ItemKind.Expansion : ItemKind.Base,
                    BoxOf(entry),
                    BasesOf(entry, titles))),
        ];
    }

    private static BoxDimensions BoxOf(FakeBggItem entry)
    {
        var kind = entry.IsExpansion ? ItemKind.Expansion : ItemKind.Base;

        if (entry.Version is not { } version || version.Width <= 0 || version.Length <= 0 || version.Depth <= 0)
        {
            return Cabinet.Domain.Collection.SnapshotMapper.DefaultBox(kind);
        }

        return new BoxDimensions(
            (int)Math.Round(version.Width * MillimetresPerInch),
            (int)Math.Round(version.Length * MillimetresPerInch),
            (int)Math.Round(version.Depth * MillimetresPerInch));
    }

    private static List<BaseGameRef> BasesOf(FakeBggItem entry, Dictionary<int, string> titles)
    {
        if (!entry.IsExpansion || entry.BaseObjectId is not { } first)
        {
            return [];
        }

        return
        [
            .. new[] { first }.Concat(entry.AlsoExpands ?? [])
                .Select(id => new BaseGameRef(id, titles.GetValueOrDefault(id, "Invented Absent Base"))),
        ];
    }
}
