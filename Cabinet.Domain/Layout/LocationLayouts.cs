using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;

namespace Cabinet.Domain.Layout;

/// <summary>One cabinet built for the games kept in one storage location, or for the games that have no location.</summary>
/// <param name="Id">The identifier of the location, or null for the games that have none.</param>
/// <param name="Name">The name of the location, or null for the games that have none.</param>
/// <param name="Layout">The cabinet that holds exactly the games of this location.</param>
public sealed record LocationLayout(string? Id, string? Name, CabinetLayout Layout);

/// <summary>One location's cabinet as the page reads it: who it is for and its sections.</summary>
/// <param name="Id">The identifier of the location; absent for the games that have none.</param>
/// <param name="Name">The name of the location; absent for the games that have none.</param>
/// <param name="Sections">The sections of the location's cabinet, in order.</param>
public sealed record LocationSections(string? Id, string? Name, IReadOnlyList<LayoutSection> Sections);

/// <summary>Everything the page draws when it shows one cabinet per location.</summary>
/// <param name="Layout">The entity tag of the single big cabinet of the same collection and profile, so the page can tell the two come from one collection version.</param>
/// <param name="LayoutVersion">The version of the layout algorithm the cabinets were built with.</param>
/// <param name="Palette">The colour table every placement's tone index points into.</param>
/// <param name="Locations">One entry per location that holds games, in the owner's order, with the games that have no location last.</param>
public sealed record LocationLayoutsDocument(
    string Layout,
    int LayoutVersion,
    IReadOnlyList<PaletteTone> Palette,
    IReadOnlyList<LocationSections> Locations);

/// <summary>
/// Builds one cabinet per storage location by running the layout engine once on the games of each location. Each location is
/// laid out from its own games alone, so adding a game to one location never moves a game in another. The locations come in the
/// owner's order, the games that have no location come last, and a location that holds no game gets no cabinet.
/// </summary>
public static class LocationLayouts
{
    /// <summary>
    /// Builds the cabinets. An expansion is paired again among the games of its own location: it stands beside its base game when
    /// that game is in the same location, and is drawn on its own labelled with the base game's name when it is not. Each
    /// cabinet applies the few-games rule to its own games. A collection whose owner has no locations gets no cabinets.
    /// </summary>
    /// <param name="items">The items of the whole collection.</param>
    /// <param name="snapshot">The stored collection the items were made from, which holds the details that pair expansions.</param>
    /// <param name="book">The owner's locations and the location of every collection entry.</param>
    /// <param name="design">The section design of the requested profile.</param>
    /// <param name="options">The layout settings the cabinets are built with.</param>
    public static IReadOnlyList<LocationLayout> Build(
        IReadOnlyList<CabinetItem> items,
        CollectionSnapshot snapshot,
        LocationBook book,
        SectionDesign design,
        LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(options);

        if (book.Order.Count == 0)
        {
            return [];
        }

        var known = book.Order.Select(location => location.Id).ToHashSet(StringComparer.Ordinal);
        var built = new List<LocationLayout>();

        foreach (var location in book.Order)
        {
            var kept = items.Where(item => string.Equals(book.LocationOf(item.CollectionId), location.Id, StringComparison.Ordinal)).ToList();

            if (kept.Count > 0)
            {
                built.Add(new LocationLayout(location.Id, location.Name, BuildOne(kept, snapshot, design, options)));
            }
        }

        var unplaced = items.Where(item => book.LocationOf(item.CollectionId) is not { } id || !known.Contains(id)).ToList();

        if (unplaced.Count > 0)
        {
            built.Add(new LocationLayout(null, null, BuildOne(unplaced, snapshot, design, options)));
        }

        return built;
    }

    /// <summary>
    /// Turns the cabinets into the document the page reads, with the same fixed options as the single cabinet so the same
    /// cabinets always give the same bytes.
    /// </summary>
    /// <param name="layoutTag">The entity tag of the single big cabinet of the same collection and profile.</param>
    /// <param name="locations">The cabinets, in the order the page shows them.</param>
    public static string Serialize(string layoutTag, IReadOnlyList<LocationLayout> locations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutTag);
        ArgumentNullException.ThrowIfNull(locations);

        var document = new LocationLayoutsDocument(
            layoutTag,
            CabinetLayoutEngine.LayoutVersion,
            SpinePalette.Tones,
            [.. locations.Select(location => new LocationSections(location.Id, location.Name, location.Layout.Sections))]);

        return JsonSerializer.Serialize(document, LayoutJson.Options);
    }

    private static CabinetLayout BuildOne(List<CabinetItem> kept, CollectionSnapshot snapshot, SectionDesign design, LayoutOptions options)
    {
        var entries = kept.Select(item => item.CollectionId).ToHashSet();
        var subset = snapshot with { Items = [.. snapshot.Items.Where(item => entries.Contains(item.CollectionId))] };
        var pairing = ExpansionPairing.Pair(subset);
        var paired = kept
            .Select(item => item.Kind == ItemKind.Expansion
                ? item with { ExpansionOf = pairing.TryGetValue(item.CollectionId, out var bases) ? bases : [] }
                : item)
            .ToList();

        return CabinetLayoutEngine.Build(paired, design, options);
    }
}
