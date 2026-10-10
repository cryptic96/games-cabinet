using System.Globalization;
using Cabinet.Service.Collection;
using Cabinet.Service.Language;
using Cabinet.Service.Prototype;
using Cabinet.Service.Sync;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cabinet.Service.Pages;

/// <summary>
/// The cabinet page. It shows the synced collection unless the catalog honours the requested sample. It only ever
/// exposes a sample name taken from the allowlist, so the request value is never echoed back into the page.
/// </summary>
/// <param name="catalog">The prototype switch and the sample allowlist.</param>
/// <param name="store">The synced collection the page reflects.</param>
/// <param name="statusService">Tells the page when the collection was last synced, for the status line.</param>
public class IndexModel(SampleCatalog catalog, CollectionStore store, SyncStatusService statusService) : PageModel
{
    /// <summary>The language the page is written in, chosen once for this request.</summary>
    public SiteLanguage Language { get; private set; } = SiteLanguage.English;

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

    /// <summary>What the status line shows: when the collection was last synced and the state around it, read when the page was requested.</summary>
    public CabinetStatus Status { get; private set; } = default!;

    /// <summary>Whether the status line and its notes are shown; they belong to the synced collection, never to a sample.</summary>
    public bool ShowSyncBlock => !ShowingSample;

    /// <summary>Whether a sync has ever succeeded, so the line can name a time.</summary>
    public bool HasSyncTime => Status.LastSyncedUtc is not null;

    /// <summary>The last good sync as an ISO 8601 UTC time, or empty before the first sync.</summary>
    public string LastSyncedIso => Iso(Status.LastSyncedUtc);

    /// <summary>The server clock as an ISO 8601 UTC time.</summary>
    public string ServerTimeIso => Iso(Status.ServerTimeUtc);

    /// <summary>When the sync button may be used again as an ISO 8601 UTC time, or empty when it may be used now.</summary>
    public string CooldownEndsIso => Iso(Status.CooldownEndsUtc);

    /// <summary>The identifier of the collection version shown, or empty before the first sync.</summary>
    public string SnapshotVersion => Status.SnapshotVersion ?? string.Empty;

    /// <summary>The first-paint text of the relative-time button, for example <c>Synced 12 minutes ago</c>.</summary>
    public string RelativeText => Status.LastSyncedUtc is { } last
        ? SyncStatusText.Relative(Status.ServerTimeUtc - last)
        : SyncStatusText.NeverSynced;

    /// <summary>The last good sync written out in UTC, or empty before the first sync.</summary>
    public string ExactText => Status.LastSyncedUtc is { } last ? SyncStatusText.ExactUtc(last) : string.Empty;

    /// <summary>Whether the note about showing an older sync is visible on first paint.</summary>
    public bool StaleNoteVisible => SyncStatusText.IsStale(
        Status.LastSyncedUtc,
        Status.ServerTimeUtc,
        TimeSpan.FromSeconds(Status.StaleAfterSeconds),
        Status.HeldBack);

    /// <summary>The first-paint sentence of the older-sync note; empty while the note is hidden.</summary>
    public string StaleNoteText => !StaleNoteVisible
        ? string.Empty
        : Status.HeldBack
            ? SyncStatusText.StaleHeldBack(ExactText)
            : SyncStatusText.StaleRecent(ExactText);

    /// <summary>Every sample name, in the order the switcher lists them.</summary>
    public IReadOnlyList<string> SampleNames => catalog.Names;

    /// <summary>The text of a switcher link for the sample name.</summary>
    public string LabelFor(string name) => SampleCatalog.Label(name);

    /// <summary>
    /// The address a language toggle item points at. It carries the sample name only while a sample is shown, and never anything
    /// the visitor typed.
    /// </summary>
    /// <param name="code">The language code the item switches to.</param>
    public string LanguageHref(string code) =>
        ShowingSample
            ? $"{LanguageEndpoint.RouteBase}/{code}?sample={Uri.EscapeDataString(SampleName)}"
            : $"{LanguageEndpoint.RouteBase}/{code}";

    /// <summary>Shows the requested sample when the catalog honours it; any other value shows the synced collection.</summary>
    public void OnGet(string? sample)
    {
        Language = SiteLanguage.Resolve(HttpContext);
        Response.Headers.ContentLanguage = Language.Code;
        Response.Headers.Append("Vary", "Accept-Language, Cookie");
        ShowingSample = catalog.TryResolve(sample, out var name);
        SampleName = ShowingSample ? name : string.Empty;
        ItemCount = ShowingSample ? catalog.ItemCount(name) : 0;
        Status = statusService.Current();
    }

    private static string Iso(DateTimeOffset? moment) =>
        moment is { } value ? Iso(value) : string.Empty;

    private static string Iso(DateTimeOffset moment) =>
        moment.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
