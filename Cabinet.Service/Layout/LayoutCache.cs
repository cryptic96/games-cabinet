using System.Collections.Concurrent;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;

namespace Cabinet.Service.Layout;

/// <summary>A serialised layout together with the entity tag that identifies it.</summary>
/// <param name="Json">The layout as served to the page.</param>
/// <param name="ETag">The quoted entity tag, built from the layout version, the settings, the sample and the profile.</param>
public sealed record CachedLayout(string Json, string ETag);

/// <summary>
/// Keeps every layout that has been asked for, built once per process. Callers only pass names from the fixed sample and
/// profile allowlists, so the number of entries is bounded and a visitor cannot grow the cache.
/// </summary>
/// <param name="options">The layout settings the cabinets are built with.</param>
public sealed class LayoutCache(LayoutOptions options)
{
    private readonly ConcurrentDictionary<(string Sample, string Design), Lazy<CachedLayout>> _entries = new();

    /// <summary>Returns the layout for a known sample and section design, building and serialising it on first use.</summary>
    /// <param name="sample">A name from the sample allowlist.</param>
    /// <param name="design">The section design for the requested profile.</param>
    public CachedLayout Get(string sample, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(design);

        return _entries.GetOrAdd((sample, design.Name), _ => new Lazy<CachedLayout>(() => Build(sample, design))).Value;
    }

    private CachedLayout Build(string sample, SectionDesign design)
    {
        if (!SyntheticCollections.TryGetSample(sample, out var items))
        {
            throw new ArgumentException("The sample is not on the allowlist.", nameof(sample));
        }

        var layout = CabinetLayoutEngine.Build(items, design, options);
        var eTag = $"\"{CabinetLayoutEngine.LayoutVersion}-{options.Fingerprint}-{sample}-{design.Name}\"";

        return new CachedLayout(LayoutJson.Serialize(layout), eTag);
    }
}
