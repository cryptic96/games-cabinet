using System.Collections.Concurrent;
using System.Globalization;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;

namespace Cabinet.Service.Prototype;

/// <summary>
/// The one switch for the invented-collection scaffolding and the list of sample names a visitor may ask for. While
/// it is disabled the page and the layout endpoint behave as if no sample exists; once the real collection is shown
/// this folder can be removed together with the switch. Each sample is generated at most once per process, the first
/// time something needs its items, so neither a page view nor a layout request pays for building it again.
/// </summary>
public sealed class SampleCatalog
{
    /// <summary>The configuration key that turns the invented collections on or off.</summary>
    public const string EnabledKey = "Prototype:Enabled";

    /// <summary>The sample shown when no valid sample is requested.</summary>
    public const string DefaultName = "65";

    private readonly Func<string, IReadOnlyList<CabinetItem>> _generate;
    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<CabinetItem>>> _items = new(StringComparer.Ordinal);

    /// <summary>Creates the catalog over the invented collections.</summary>
    /// <param name="enabled">Whether the invented collections and their switcher are available.</param>
    public SampleCatalog(bool enabled)
        : this(enabled, GenerateSynthetic)
    {
    }

    /// <summary>Creates the catalog with the function that generates a sample's items from its name.</summary>
    /// <param name="enabled">Whether the invented collections and their switcher are available.</param>
    /// <param name="generate">Generates the items of a known sample; it is called at most once per name.</param>
    public SampleCatalog(bool enabled, Func<string, IReadOnlyList<CabinetItem>> generate)
    {
        ArgumentNullException.ThrowIfNull(generate);

        Enabled = enabled;
        _generate = generate;
    }

    /// <summary>Whether the invented collections and their switcher are available.</summary>
    public bool Enabled { get; }

    /// <summary>Every sample name, in the order the switcher lists them.</summary>
    public IReadOnlyList<string> Names => SyntheticCollections.SampleNames;

    /// <summary>Whether the name is exactly one of the sample names; case and surrounding spaces matter.</summary>
    public bool IsKnown(string? name) => name is not null && Names.Contains(name, StringComparer.Ordinal);

    /// <summary>The requested sample when it is known, otherwise the default sample. Unknown input is never returned.</summary>
    public string Resolve(string? requested) => IsKnown(requested) ? requested! : DefaultName;

    /// <summary>
    /// The items of a known sample, generated on first use and kept for the life of the process. Only allowlisted names
    /// are accepted, so the number of kept samples is bounded.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not on the sample allowlist.</exception>
    public IReadOnlyList<CabinetItem> ItemsOf(string name)
    {
        if (!IsKnown(name))
        {
            throw new ArgumentException("The sample is not on the allowlist.", nameof(name));
        }

        return _items.GetOrAdd(name, key => new Lazy<IReadOnlyList<CabinetItem>>(() => _generate(key))).Value;
    }

    /// <summary>The number of items in a known sample; an unknown name counts as the default sample.</summary>
    public int ItemCount(string? name) => ItemsOf(Resolve(name)).Count;

    /// <summary>
    /// Reads the switch from configuration. A missing key means off; true and false in any case are accepted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is present but is neither true nor false.</exception>
    public static SampleCatalog FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var text = configuration[EnabledKey];

        if (text is null)
        {
            return new SampleCatalog(false);
        }

        if (!bool.TryParse(text.Trim(), out var enabled))
        {
            throw new InvalidOperationException($"{EnabledKey} must be true or false.");
        }

        return new SampleCatalog(enabled);
    }

    /// <summary>The text of a switcher link: the sample name, or a readable label for the edge-case sample.</summary>
    public static string Label(string name) =>
        int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? name : "Edge cases";

    private static IReadOnlyList<CabinetItem> GenerateSynthetic(string name) =>
        SyntheticCollections.TryGetSample(name, out var items)
            ? items
            : throw new ArgumentException("The sample is not on the allowlist.", nameof(name));
}
