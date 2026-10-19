using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Samples;
using Cabinet.Service.Prototype;

namespace Cabinet.Service.Search;

/// <summary>Serialised other titles together with the entity tag that identifies them.</summary>
/// <param name="Json">The names document as served to the page.</param>
/// <param name="ETag">The quoted entity tag, derived from a hash of the serialised JSON.</param>
public sealed record CachedNames(string Json, string ETag)
{
    private const int TagHexLength = 16;
    private const string TagPrefix = "names-";

    /// <summary>Serialises the document and derives its entity tag from the bytes, so the tag changes exactly when the names do.</summary>
    /// <param name="document">The names document to serialise.</param>
    public static CachedNames From(SearchNamesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var json = SearchNames.Serialize(document);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        return new CachedNames(json, $"\"{TagPrefix}{hash[..TagHexLength]}\"");
    }
}

/// <summary>
/// Keeps the other titles of every invented collection that has been asked for, built once per process. Callers only pass
/// names from the fixed sample allowlist, so the number of entries is bounded and a visitor cannot grow the cache. The real
/// collection's names live with the collection itself. A sample's names that fail to build are remembered as failed until the
/// process restarts: a sample never changes, so building again would fail the same way.
/// </summary>
/// <param name="catalog">The sample allowlist that supplies each sample's items.</param>
public sealed class SearchNamesCache(SampleCatalog catalog)
{
    private readonly ConcurrentDictionary<string, Lazy<CachedNames>> _entries = new(StringComparer.Ordinal);

    /// <summary>Returns the other titles of a known sample, building and serialising them on first use.</summary>
    /// <param name="sample">A name from the sample allowlist.</param>
    public CachedNames Get(string sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return _entries.GetOrAdd(sample, _ => new Lazy<CachedNames>(() => Build(sample))).Value;
    }

    private CachedNames Build(string sample)
    {
        var items = catalog.ItemsOf(sample);

        return CachedNames.From(SearchNames.Build(SampleCardDetails.SnapshotFor(sample, items)));
    }
}
