using System.Collections.Concurrent;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using Cabinet.Service.Prototype;

namespace Cabinet.Service.Layout;

/// <summary>A serialised layout together with the entity tag that identifies it.</summary>
/// <param name="Json">The layout as served to the page.</param>
/// <param name="ETag">The quoted entity tag, built from the layout version, the settings, the sample and the profile.</param>
public sealed record CachedLayout(string Json, string ETag)
{
    private const string ByLocationSuffix = "-by-location";

    /// <summary>The entity tag of the cabinets built per location, made from the tag of the single big cabinet of the same collection and profile.</summary>
    /// <param name="layoutTag">The quoted entity tag of the single big cabinet.</param>
    public static string ByLocationTag(string layoutTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutTag);

        return $"{layoutTag.TrimEnd('"')}{ByLocationSuffix}\"";
    }
}

/// <summary>
/// Keeps every invented-collection layout that has been asked for, built once per process. The real collection's layouts
/// live with the collection itself. Callers only pass names from the fixed sample and
/// profile allowlists, so the number of entries is bounded and a visitor cannot grow the cache. A sample's items are only
/// asked for when its layout is built, so a request answered from the cache costs no sample generation. A layout that
/// fails to build is remembered as failed, so the same request keeps failing until the process restarts; the engine only
/// fails on a broken invariant, which a test should catch first.
/// </summary>
/// <param name="options">The layout settings the cabinets are built with.</param>
/// <param name="catalog">The sample allowlist that supplies each sample's items.</param>
public sealed class LayoutCache(LayoutOptions options, SampleCatalog catalog)
{
    private readonly ConcurrentDictionary<(string Sample, string Design), Lazy<CachedLayout>> _entries = new();
    private readonly ConcurrentDictionary<(string Sample, string Design), Lazy<CachedLayout>> _byLocation = new();

    /// <summary>Returns the layout for a known sample and section design, building and serialising it on first use.</summary>
    /// <param name="sample">A name from the sample allowlist.</param>
    /// <param name="design">The section design for the requested profile.</param>
    public CachedLayout Get(string sample, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(design);

        return _entries.GetOrAdd((sample, design.Name), _ => new Lazy<CachedLayout>(() => Build(sample, design))).Value;
    }

    /// <summary>
    /// Returns the cabinets of a known sample, one per invented storage location that holds games, building and serialising
    /// them on first use. The document names the entity tag of the single sample cabinet of the same profile.
    /// </summary>
    /// <param name="sample">A name from the sample allowlist.</param>
    /// <param name="design">The section design for the requested profile.</param>
    public CachedLayout GetByLocation(string sample, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(design);

        return _byLocation.GetOrAdd((sample, design.Name), _ => new Lazy<CachedLayout>(() => BuildByLocation(sample, design))).Value;
    }

    private CachedLayout Build(string sample, SectionDesign design)
    {
        var items = catalog.ItemsOf(sample);
        var layout = CabinetLayoutEngine.Build(items, design, options);

        return new CachedLayout(LayoutJson.Serialize(layout), TagOf(sample, design));
    }

    private CachedLayout BuildByLocation(string sample, SectionDesign design)
    {
        var items = catalog.ItemsOf(sample);
        var locations = LocationLayouts.Build(
            items,
            SampleCardDetails.SnapshotFor(sample, items),
            SampleCardDetails.LocationBookFor(sample, items),
            design,
            options);
        var tag = TagOf(sample, design);

        return new CachedLayout(LocationLayouts.Serialize(tag, locations), CachedLayout.ByLocationTag(tag));
    }

    private string TagOf(string sample, SectionDesign design) =>
        $"\"{CabinetLayoutEngine.LayoutVersion}-{options.Fingerprint}-{sample}-{design.Name}\"";
}
