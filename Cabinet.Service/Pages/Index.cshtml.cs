using Cabinet.Service.Collection;
using Cabinet.Service.Prototype;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cabinet.Service.Pages;

/// <summary>
/// The cabinet page. It shows the synced collection unless the catalog honours the requested sample. It only ever
/// exposes a sample name taken from the allowlist, so the request value is never echoed back into the page.
/// </summary>
/// <param name="catalog">The prototype switch and the sample allowlist.</param>
/// <param name="store">The synced collection the page reflects.</param>
public class IndexModel(SampleCatalog catalog, CollectionStore store) : PageModel
{
    /// <summary>Whether the invented collections and their switcher are available.</summary>
    public bool PrototypeEnabled => catalog.Enabled;

    /// <summary>Whether an invented collection is shown instead of the synced one.</summary>
    public bool ShowingSample { get; private set; }

    /// <summary>The honoured sample name from the allowlist; empty while the synced collection is shown.</summary>
    public string SampleName { get; private set; } = string.Empty;

    /// <summary>The number of items in the shown sample; zero while the synced collection is shown.</summary>
    public int ItemCount { get; private set; }

    /// <summary>Whether a collection has ever been synced; the being-filled message shows until one has.</summary>
    public bool HasSynced => store.Current.HasSynced;

    /// <summary>Every sample name, in the order the switcher lists them.</summary>
    public IReadOnlyList<string> SampleNames => catalog.Names;

    /// <summary>The text of a switcher link for the sample name.</summary>
    public string LabelFor(string name) => SampleCatalog.Label(name);

    /// <summary>Shows the requested sample when the catalog honours it; any other value shows the synced collection.</summary>
    public void OnGet(string? sample)
    {
        ShowingSample = catalog.TryResolve(sample, out var name);
        SampleName = ShowingSample ? name : string.Empty;
        ItemCount = ShowingSample ? catalog.ItemCount(name) : 0;
    }
}
