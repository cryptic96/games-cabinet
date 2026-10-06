using Cabinet.Service.Prototype;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cabinet.Service.Pages;

/// <summary>
/// The cabinet page. It only ever exposes a sample name taken from the allowlist, so the request value is never echoed
/// back into the page.
/// </summary>
/// <param name="catalog">The prototype switch and the sample allowlist.</param>
public class IndexModel(SampleCatalog catalog) : PageModel
{
    /// <summary>Whether the invented collections and their switcher are shown.</summary>
    public bool PrototypeEnabled => catalog.Enabled;

    /// <summary>The resolved sample name; always a member of the allowlist.</summary>
    public string SampleName { get; private set; } = SampleCatalog.DefaultName;

    /// <summary>The number of items in the resolved sample.</summary>
    public int ItemCount { get; private set; }

    /// <summary>Every sample name, in the order the switcher lists them.</summary>
    public IReadOnlyList<string> SampleNames => catalog.Names;

    /// <summary>The text of a switcher link for the sample name.</summary>
    public string LabelFor(string name) => SampleCatalog.Label(name);

    /// <summary>Resolves the requested sample to a known name; an unknown or missing value becomes the default.</summary>
    public void OnGet(string? sample)
    {
        SampleName = catalog.Resolve(sample);
        ItemCount = catalog.Enabled ? catalog.ItemCount(SampleName) : 0;
    }
}
