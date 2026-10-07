using System.Collections.Concurrent;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Service.Layout;

namespace Cabinet.Service.Collection;

/// <summary>
/// One immutable view of the owned collection: the items to draw, which collection version they came from, and when they
/// were captured. A new view replaces the old one as a whole, so a reader that holds one never sees a half-updated
/// collection. The layouts built from it are kept with it and disappear with it.
/// </summary>
public sealed class CollectionState
{
    private readonly ConcurrentDictionary<string, Lazy<CachedLayout>> _layouts = new(StringComparer.Ordinal);

    /// <summary>Creates a view of a collection that has been synced.</summary>
    /// <param name="items">The items to draw.</param>
    /// <param name="version">The identifier of the collection version; it changes whenever the collection does.</param>
    /// <param name="capturedAtUtc">When the collection was read from its source.</param>
    public CollectionState(IReadOnlyList<CabinetItem> items, string version, DateTimeOffset capturedAtUtc)
        : this(items, version, (DateTimeOffset?)capturedAtUtc, null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
    }

    private CollectionState(IReadOnlyList<CabinetItem> items, string? version, DateTimeOffset? capturedAtUtc, CollectionSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items;
        Version = version;
        CapturedAtUtc = capturedAtUtc;
        Snapshot = snapshot;
    }

    /// <summary>Creates the view of a stored collection: its items are mapped in a fixed order and its version is derived from them.</summary>
    /// <param name="snapshot">The stored collection.</param>
    public static CollectionState FromSnapshot(CollectionSnapshot snapshot) => FromSnapshot(snapshot, ArtRules.Default);

    /// <summary>
    /// Creates the view of a stored collection as <see cref="FromSnapshot(CollectionSnapshot)"/> does, choosing each game's
    /// picture with the given rules; the rules are part of the version, so a change of rules is a new collection version.
    /// </summary>
    /// <param name="snapshot">The stored collection.</param>
    /// <param name="rules">The rules that choose each game's picture.</param>
    public static CollectionState FromSnapshot(CollectionSnapshot snapshot, ArtRules rules)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);

        var items = SnapshotMapper.ToCabinetItems(snapshot, rules);

        return new CollectionState(items, SnapshotMapper.Version(items, rules), snapshot.CapturedAtUtc, snapshot);
    }

    /// <summary>The view before any collection has been synced: no items and no version.</summary>
    public static CollectionState Empty { get; } = new([], null, null, null);

    /// <summary>The items to draw.</summary>
    public IReadOnlyList<CabinetItem> Items { get; }

    /// <summary>The stored collection this view was made from, or null for a view that was not made from one.</summary>
    public CollectionSnapshot? Snapshot { get; }

    /// <summary>The collection version, or null before the first sync.</summary>
    public string? Version { get; }

    /// <summary>When the collection was read from its source, or null before the first sync.</summary>
    public DateTimeOffset? CapturedAtUtc { get; }

    /// <summary>Whether a collection has ever been synced, including a synced collection that holds no games.</summary>
    public bool HasSynced => Version is not null;

    /// <summary>
    /// The serialised layout of this collection for one section design, built on first use and kept with the view, so each
    /// design is laid out at most once per collection version however many visitors ask.
    /// </summary>
    /// <param name="design">The section design for the requested profile.</param>
    /// <param name="options">The layout settings the cabinet is built with.</param>
    public CachedLayout LayoutFor(SectionDesign design, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(options);

        return _layouts.GetOrAdd(design.Name, _ => new Lazy<CachedLayout>(() => Build(design, options))).Value;
    }

    private CachedLayout Build(SectionDesign design, LayoutOptions options)
    {
        var layout = CabinetLayoutEngine.Build(Items, design, options);
        var eTag = $"\"{CabinetLayoutEngine.LayoutVersion}-{options.Fingerprint}-collection-{Version ?? "empty"}-{design.Name}\"";

        return new CachedLayout(LayoutJson.Serialize(layout), eTag);
    }
}

/// <summary>
/// Holds the collection the page and the layout endpoint read. It starts empty; whatever fills it replaces the view as a
/// whole, and readers always see either the old view or the new one.
/// </summary>
public sealed class CollectionStore
{
    private CollectionState _current = CollectionState.Empty;

    /// <summary>The view readers should use right now.</summary>
    public CollectionState Current => Volatile.Read(ref _current);

    /// <summary>Makes a new view the current one.</summary>
    /// <param name="next">The view that replaces the current one.</param>
    public void Replace(CollectionState next)
    {
        ArgumentNullException.ThrowIfNull(next);

        Volatile.Write(ref _current, next);
    }
}
