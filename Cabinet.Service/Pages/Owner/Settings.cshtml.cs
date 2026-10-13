using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
using Cabinet.Repository.Storage;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;

namespace Cabinet.Service.Pages.Owner;

/// <summary>What one side of the restore comparison holds.</summary>
/// <param name="SavedAtUtc">When the data was last saved, or null when it never was.</param>
/// <param name="Figures">The counts of locations, placed games and picture choices.</param>
/// <param name="NotInCollection">How many games with decisions the collection does not list now.</param>
public sealed record RestoreSide(DateTimeOffset? SavedAtUtc, OwnerDataFigures Figures, int NotInCollection);

/// <summary>A checked file shown next to what the server holds now, waiting for the owner to restore it or keep the current data.</summary>
/// <param name="Token">The token the confirming post carries; the server made it.</param>
/// <param name="OlderFormat">The format of the file when it is older than the one the site writes, otherwise null.</param>
/// <param name="InFile">What the file holds.</param>
/// <param name="OnServer">What the server holds now.</param>
public sealed record RestorePreview(string Token, int? OlderFormat, RestoreSide InFile, RestoreSide OnServer)
{
    /// <summary>The short sentence that says what the file holds.</summary>
    public string Summary => OwnerStrings.RestoreSummary(InFile.Figures.Locations, InFile.Figures.PlacedGames, InFile.Figures.PictureChoices);
}

/// <summary>
/// The owner's settings page. It exists only on the owner port. It holds the finish of the cabinet, the backup, where the owner downloads all the owner
/// data as a file and sees how long ago that was, the restore of such a file, and the list of the latest changes with a line at the last download.
/// A restore file is untrusted: it is checked whole before anything is shown, shown next to the data on the server, and only a confirming post
/// replaces anything. The request limit of this page is higher than the owner folder's default, so a file just over the rule gets its words.
/// </summary>
[RequestSizeLimit(UploadLimitBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = UploadLimitBytes)]
public class SettingsModel : OwnerPage
{
    private const int UploadLimitBytes = 3 * 1024 * 1024;
    private const string PagePath = "/Owner/Settings";
    private const string DownloadFailedCode = "download-failed";
    private const string UnchangedCode = "unchanged";
    private const string FinishAnchor = "finish";
    private const string FileInputAnchor = "restore-file";
    private const string PreviewAnchor = "restore-preview-heading";
    private const string JsonMediaType = "application/json";
    private const string NoStore = "no-store";

    private readonly OwnerDataService _owner;
    private readonly CollectionStore _collection;
    private readonly StagedItems<RestoreCandidate> _staged;
    private readonly ArtSourcePolicy _policy;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data, which also makes the download and the restore.</param>
    /// <param name="collection">The collection visitors see, which says which games are owned now.</param>
    /// <param name="staged">Holds the checked files that wait for the owner's confirmation.</param>
    /// <param name="policy">Says which addresses a linked picture may be fetched from, which every link of a restored file must meet.</param>
    public SettingsModel(OwnerDataService owner, CollectionStore collection, StagedItems<RestoreCandidate> staged, ArtSourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(policy);

        _owner = owner;
        _collection = collection;
        _staged = staged;
        _policy = policy;
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

    /// <summary>The words for why the file chosen for a restore was refused, or null.</summary>
    public string? RestoreNotice { get; private set; }

    /// <summary>The checked file next to what the server holds now, or null when no check waits.</summary>
    public RestorePreview? Preview { get; private set; }

    /// <summary>The line that says what a restore brought back, or null when none just happened.</summary>
    public string? RestoredText { get; private set; }

    /// <summary>The line that says how many linked pictures are being fetched again after a restore, or null when there are none.</summary>
    public string? RestoredFetchingText { get; private set; }

    /// <summary>Shows the page.</summary>
    /// <param name="done">The code of the post that sent the browser here; anything outside the fixed set is ignored.</param>
    /// <param name="restore">The token of a checked file to show next to the data on the server; anything that is not a token of a held file is ignored.</param>
    public void OnGet(string? done, string? restore)
    {
        Populate(done);
        Preview = PreviewOf(restore);
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

    /// <summary>
    /// Checks the file the owner chose for a restore, whole, and changes nothing. The upload is read once into a buffer that the size rule
    /// bounds, and the name the browser sent for it is never used. A file that passes is held and shown next to the data on the server; a
    /// file that does not is refused with its own words. A script is answered with JSON, a form without script with the page.
    /// </summary>
    /// <param name="ownerDataFile">The file field of the form.</param>
    /// <param name="cancellationToken">Cancels the read of the upload.</param>
    public async Task<IActionResult> OnPostCheckFileAsync(IFormFile? ownerDataFile, CancellationToken cancellationToken)
    {
        var check = await CheckUploadAsync(ownerDataFile, cancellationToken);

        if (!check.IsOk)
        {
            var words = OwnerStrings.RestoreProblemText(check);

            if (OwnerResponses.AsksForJson(Request))
            {
                return new JsonResult(new { outcome = "refused", message = words }) { StatusCode = StatusCodes.Status422UnprocessableEntity };
            }

            Populate(done: null);
            RestoreNotice = words;
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

            return Page();
        }

        var token = _staged.Stage(new RestoreCandidate(check.Data!, check.FormatVersion, RestoreSource.File));

        if (OwnerResponses.AsksForJson(Request))
        {
            return new JsonResult(new { outcome = "checked", token });
        }

        return RedirectToPage(PagePath, pageHandler: null, routeValues: new { restore = token }, fragment: PreviewAnchor);
    }

    /// <summary>
    /// Checks one of the earlier versions the server keeps, whole, and changes nothing. The version is read by its place in the server's own
    /// list, never by a name from the request, and goes through the same check and staging as a chosen file; the owner then confirms it
    /// with the same restore. A place that is not in the list, or a copy the check cannot accept, is refused; a copy that is not an owner-data file at all reads as damaged, since the server made it.
    /// </summary>
    /// <param name="index">The place of the version in the list the recovery panel shows; anything that is not a whole number reads as no place.</param>
    public IActionResult OnPostCheckVersion(int? index)
    {
        var bytes = index is { } place ? _owner.ReadEarlierVersion(place) : null;
        var check = bytes is null ? RestoreCheck.Problem(RestoreProblem.Damaged) : OwnerDataExport.Check(bytes, _policy);

        if (check.Refusal == RestoreProblem.NotOwnerData)
        {
            check = RestoreCheck.Problem(RestoreProblem.Damaged);
        }

        if (!check.IsOk)
        {
            Populate(done: null);
            RestoreNotice = OwnerStrings.RestoreProblemText(check);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

            return Page();
        }

        var token = _staged.Stage(new RestoreCandidate(check.Data!, check.FormatVersion, RestoreSource.EarlierVersion));

        return RedirectToPage(PagePath, pageHandler: null, routeValues: new { restore = token }, fragment: PreviewAnchor);
    }

    /// <summary>
    /// Replaces all the owner data with the checked file the token names. The data that is there is kept as an earlier version first.
    /// A token that names nothing held, because the check ran out or never happened, restores nothing.
    /// </summary>
    /// <param name="token">The token the check answered with.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostRestoreAsync(string? token, CancellationToken cancellationToken)
    {
        if (!_staged.TryGet(token, out var candidate))
        {
            Populate(done: null);
            RestoreNotice = OwnerStrings.RestoreExpired;
            Response.StatusCode = StatusCodes.Status409Conflict;

            return Page();
        }

        var result = await _owner.RestoreAsync(candidate.Data, candidate.Source, OwnedGameIds(), cancellationToken);

        if (result.Outcome != OwnerOutcome.Saved)
        {
            Populate(done: null);
            RestoreNotice = OwnerStrings.RestoreFailed;
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            Preview = PreviewOf(token);

            return Page();
        }

        _staged.Remove(token);
        Populate(done: null);

        var restored = OwnerDataFigures.From(candidate.Data);
        var links = candidate.Data.Games.Count(game => game.Picture?.Link is not null);
        RestoredText = OwnerStrings.RestoreDone(OwnerStrings.RestoreSummary(restored.Locations, restored.PlacedGames, restored.PictureChoices));
        RestoredFetchingText = links > 0 ? OwnerStrings.FetchingPictures(links) : null;

        return Page();
    }

    /// <summary>Throws a check away and leaves everything as it is.</summary>
    /// <param name="token">The token the check answered with.</param>
    public IActionResult OnPostDiscard(string? token)
    {
        _staged.Remove(token);

        return RedirectToPage(PagePath, pageHandler: null, routeValues: null, fragment: FileInputAnchor);
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

    private void Populate(string? done)
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

    private async Task<RestoreCheck> CheckUploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return RestoreCheck.Problem(RestoreProblem.NoFile);
        }

        if (file.Length > OwnerDataExport.MaxRestoreBytes)
        {
            return RestoreCheck.Problem(RestoreProblem.TooLarge, sizeBytes: file.Length);
        }

        var buffer = new byte[file.Length];
        var filled = 0;

        await using var stream = file.OpenReadStream();

        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);

            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return OwnerDataExport.Check(buffer[..filled], _policy);
    }

    private RestorePreview? PreviewOf(string? token)
    {
        if (!_staged.TryGet(token, out var candidate))
        {
            return null;
        }

        var owned = OwnedGameIds();
        var current = _owner.State is OwnerDataState.Ready ready ? ready.Data : null;

        return new RestorePreview(
            token!,
            candidate.FormatVersion < OwnerData.CurrentSchemaVersion ? candidate.FormatVersion : null,
            SideOf(candidate.Data, owned),
            current is null ? new RestoreSide(null, default, 0) : SideOf(current, owned));
    }

    private static RestoreSide SideOf(OwnerData data, IReadOnlySet<int>? owned) =>
        new(
            data.SavedAtUtc,
            OwnerDataFigures.From(data),
            owned is null ? 0 : data.Games.Count(game => !owned.Contains(game.GameId)));

    private HashSet<int>? OwnedGameIds() =>
        _collection.Current.Snapshot is { } snapshot ? [.. snapshot.Items.Select(item => item.GameId)] : null;
}
