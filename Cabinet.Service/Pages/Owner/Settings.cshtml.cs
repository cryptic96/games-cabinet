using Cabinet.Domain.Owner;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;

namespace Cabinet.Service.Pages.Owner;

/// <summary>
/// The owner's settings page. It exists only on the owner port. It holds the finish of the cabinet, the backup, where the owner downloads all the owner
/// data as a file and sees how long ago that was, and the list of the latest changes with a line at the last download.
/// </summary>
public class SettingsModel : OwnerPage
{
    private const string PagePath = "/Owner/Settings";
    private const string DownloadFailedCode = "download-failed";
    private const string UnchangedCode = "unchanged";
    private const string FinishAnchor = "finish";
    private const string JsonMediaType = "application/json";
    private const string NoStore = "no-store";

    private readonly OwnerDataService _owner;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data, which also makes the download.</param>
    public SettingsModel(OwnerDataService owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <inheritdoc />
    public override string PageName => OwnerStrings.Settings;

    /// <summary>What the backup panel shows: whether the data can be read, whether there is any, and how stale the last download is.</summary>
    public BackupState Backup { get; private set; } = new(Readable: false, HasData: false, 0, null);

    /// <summary>The latest changes, newest first, as many as the page shows; the reminder counts all of them.</summary>
    public IReadOnlyList<ChangeEntry> VisibleChanges { get; private set; } = [];

    /// <summary>
    /// How many entries of <see cref="VisibleChanges"/> come before the line that marks the last download, or null when there
    /// is no such line: nothing was downloaded yet, or the download lies further back than the entries shown.
    /// </summary>
    public int? DividerAfter { get; private set; }

    /// <summary>The words for what went wrong with the post that sent the browser here, or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>The revision of the owner data the page was built from, which the finish form posts back.</summary>
    public long Revision { get; private set; }

    /// <summary>The finish in use, which the checked tile shows.</summary>
    public string Finish { get; private set; } = OwnerData.DefaultFinish;

    /// <summary>The words for the result of the finish post that sent the browser here, or null.</summary>
    public string? FinishNotice { get; private set; }

    /// <summary>Whether <see cref="FinishNotice"/> says something went wrong.</summary>
    public bool FinishNoticeIsProblem { get; private set; }

    /// <summary>Shows the page.</summary>
    /// <param name="done">The code of the post that sent the browser here; anything outside the fixed set is ignored.</param>
    public void OnGet(string? done)
    {
        Backup = _owner.Backup;
        Revision = _owner.Revision;
        Finish = _owner.State is OwnerDataState.Ready current ? Finishes.Normalize(current.Data.Finish) : OwnerData.DefaultFinish;
        FinishNotice = string.Equals(done, UnchangedCode, StringComparison.Ordinal) ? OwnerStrings.FinishSaved : OwnerResponses.NoticeFor(done);
        FinishNoticeIsProblem = OwnerResponses.IsProblem(done);

        if (_owner.State is OwnerDataState.Ready ready)
        {
            VisibleChanges = [.. ready.Data.Changes.Take(OwnerData.VisibleChanges)];
            DividerAfter = ready.Data.LastDownloadAtUtc is not null && ready.Data.ChangesSinceDownload <= VisibleChanges.Count
                ? ready.Data.ChangesSinceDownload
                : null;
        }

        Notice = string.Equals(done, DownloadFailedCode, StringComparison.Ordinal) ? OwnerStrings.DownloadFailed : null;
    }

    /// <summary>Saves the finish of the cabinet. A finish that is not on the allow-list is refused, and the finish already in use is nothing to change.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="finish">The finish the owner chose.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostFinishAsync(long revision, string? finish, CancellationToken cancellationToken)
    {
        var result = await _owner.MutateAsync(revision, data => Finishes.SetFinish(data, finish), cancellationToken);

        return OwnerResponses.For(this, result, PagePath, FinishAnchor);
    }

    /// <summary>
    /// Answers with the owner data as a downloadable file and records that a backup was made. When the file cannot be made the
    /// browser is sent back to this page with the words that say so, and nothing is recorded.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostDownloadAsync(CancellationToken cancellationToken)
    {
        var download = await _owner.RecordDownloadAsync(cancellationToken);

        if (download is null)
        {
            return RedirectToPage(PagePath, pageHandler: null, routeValues: new { done = DownloadFailedCode }, fragment: "backup");
        }

        Response.Headers.CacheControl = NoStore;

        return File(download.Content, JsonMediaType, download.FileName);
    }

    /// <summary>Answers a script with how many changes are not downloaded yet and when the last download was, so it can bring the reminder up to date.</summary>
    public IActionResult OnGetBackupState()
    {
        var backup = _owner.Backup;
        Response.Headers.CacheControl = NoStore;

        return new JsonResult(new
        {
            readable = backup.Readable,
            hasData = backup.HasData,
            changes = backup.ChangesSinceDownload,
            lastDownloadAtUtc = backup.LastDownloadAtUtc is { } at ? OwnerStrings.TimeAttribute(at) : null,
            neverDownloaded = backup.LastDownloadAtUtc is null,
        });
    }
}
