using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Cabinet.Domain.Cards;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using Cabinet.Service.Layout;
using Cabinet.Service.Prototype;

namespace Cabinet.Service.Cards;

/// <summary>Serialised card records together with the entity tag that identifies them.</summary>
/// <param name="Json">The cards document as served to the page.</param>
/// <param name="ETag">The quoted entity tag, derived from a hash of the serialised JSON.</param>
public sealed record CachedCards(string Json, string ETag)
{
    private const int TagHexLength = 16;
    private const string TagPrefix = "cards-";

    /// <summary>Serialises the document and derives its entity tag from the bytes, so the tag changes exactly when the cards do.</summary>
    /// <param name="document">The cards document to serialise.</param>
    public static CachedCards From(CardsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var json = CardRecords.Serialize(document);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        return new CachedCards(json, $"\"{TagPrefix}{hash[..TagHexLength]}\"");
    }
}

/// <summary>
/// Keeps the card records of every invented collection that has been asked for, built once per process. Callers only pass
/// names from the fixed sample and profile allowlists, so the number of entries is bounded and a visitor cannot grow the
/// cache. The real collection's cards live with the collection itself.
/// </summary>
/// <param name="catalog">The sample allowlist that supplies each sample's items.</param>
/// <param name="layouts">The sample layout cache, whose entity tag each cards document names.</param>
public sealed class CardCache(SampleCatalog catalog, LayoutCache layouts)
{
    private readonly ConcurrentDictionary<(string Sample, string Design), Lazy<CachedCards>> _entries = new();

    /// <summary>Returns the cards for a known sample and section design, building and serialising them on first use.</summary>
    /// <param name="sample">A name from the sample allowlist.</param>
    /// <param name="design">The section design for the requested profile.</param>
    public CachedCards Get(string sample, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(design);

        return _entries.GetOrAdd((sample, design.Name), _ => new Lazy<CachedCards>(() => Build(sample, design))).Value;
    }

    private CachedCards Build(string sample, SectionDesign design)
    {
        var items = catalog.ItemsOf(sample);
        var cards = CardRecords.Build(items, SampleCardDetails.SnapshotFor(sample, items), design);

        return CachedCards.From(new CardsDocument(layouts.Get(sample, design).ETag, cards));
    }
}
