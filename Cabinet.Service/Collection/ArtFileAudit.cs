using Cabinet.Domain.Layout;
using Cabinet.Repository.Images;

namespace Cabinet.Service.Collection;

/// <summary>
/// Finds stored pictures the collection names but the art directory no longer holds. A missing picture never reaches the
/// visitor as a fault, because the cabinet draws the generated cover instead; this audit is how the owner learns it happened.
/// Only a count is ever reported, never a file name, a title or an address.
/// </summary>
public sealed class ArtFileAudit
{
    private readonly ArtCache _cache;
    private readonly CollectionStore _store;
    private readonly ILogger<ArtFileAudit> _logger;
    private int _lastLoggedCount;

    /// <summary>Creates the audit.</summary>
    /// <param name="cache">The art directory the pictures are stored in.</param>
    /// <param name="store">The collection whose pictures are checked.</param>
    /// <param name="logger">Receives one warning whenever the count of missing pictures changes to a new non-zero value.</param>
    public ArtFileAudit(ArtCache cache, CollectionStore store, ILogger<ArtFileAudit> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = cache;
        _store = store;
        _logger = logger;
    }

    /// <summary>Counts the distinct stored pictures the current collection names that are missing on disk.</summary>
    /// <returns>The number of missing pictures; zero when every named picture is present.</returns>
    public int CountMissing()
    {
        var prefix = ArtFitting.RequestPath + "/";
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in _store.Current.Items)
        {
            if (item.Art is null)
            {
                continue;
            }

            foreach (var variant in item.Art.Variants)
            {
                var name = PlainFileName(variant.Url, prefix);

                if (name is not null)
                {
                    names.Add(name);
                }
            }
        }

        var missing = names.Count(name => !_cache.Has(name));
        Report(missing);

        return missing;
    }

    private static string? PlainFileName(string url, string prefix)
    {
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = url[prefix.Length..];

        return name.Length > 0 && name != ".." && name == Path.GetFileName(name) && name.IndexOfAny(['/', '\\']) < 0
            ? name
            : null;
    }

    private void Report(int missing)
    {
        var previous = Interlocked.Exchange(ref _lastLoggedCount, missing);

        if (missing > 0 && missing != previous)
        {
            _logger.LogWarning("The collection names {MissingCount} stored pictures that are missing on disk.", missing);
        }
    }
}
