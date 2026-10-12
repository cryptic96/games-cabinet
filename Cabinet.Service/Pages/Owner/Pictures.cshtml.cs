using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;

namespace Cabinet.Service.Pages.Owner;

/// <summary>Which games the pictures page lists.</summary>
public enum PicturesView
{
    /// <summary>The games the owner has not dealt with and that need a look, the ones with a mark that asks for attention first.</summary>
    WorthALook,

    /// <summary>Every game by title.</summary>
    All,
}

/// <summary>
/// The owner's pictures page: the games worth a look, or every game, each with the four pictures it can be drawn from, shown
/// as they would look. Every post goes through the single owner-data writer and is answered by <see cref="OwnerResponses"/>, so
/// the page works without scripts.
/// </summary>
public class PicturesModel : OwnerPage
{
    private const string PagePath = "/Owner/Pictures";
    private const string NoticeAnchor = "owner-notice";
    private const string AllViewValue = "all";

    private readonly OwnerDataService _owner;
    private readonly CollectionStore _collection;
    private readonly ArtRules _rules;
    private readonly TimeProvider _time;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data.</param>
    /// <param name="collection">The collection visitors see, which lists the games.</param>
    /// <param name="rules">The rules that decide what the automatic pick draws.</param>
    /// <param name="time">The clock that decides whether a linked picture counts as dead.</param>
    public PicturesModel(OwnerDataService owner, CollectionStore collection, ArtRules rules, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(time);

        _owner = owner;
        _collection = collection;
        _rules = rules;
        _time = time;
    }

    /// <inheritdoc />
    public override string PageName => OwnerStrings.Pictures;

    /// <summary>Whether the owner data could be read; the page offers no form while it cannot.</summary>
    public bool Readable { get; private set; }

    /// <summary>The revision the forms were built from.</summary>
    public long Revision { get; private set; }

    /// <summary>Which games the page lists.</summary>
    public PicturesView View { get; private set; }

    /// <summary>The games the page lists, in the order the view gives.</summary>
    public IReadOnlyList<PictureRow> Games { get; private set; } = [];

    /// <summary>How many games the page's two views and its counter are made from.</summary>
    public PictureCounts Counts { get; private set; } = new(0, 0, 0);

    /// <summary>The words for the outcome of the post that sent the browser here, or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether <see cref="Notice"/> reports a problem.</summary>
    public bool NoticeIsProblem { get; private set; }

    /// <summary>The revision of the change the Undo form would take back, or null when there is none to offer.</summary>
    public long? UndoRevision { get; private set; }

    /// <summary>The value of the <c>view</c> query that selects the view of every game.</summary>
    public static string AllViewQuery => AllViewValue;

    /// <summary>The name the radios of one game share, which is also the field a form posted without script carries.</summary>
    /// <param name="gameId">The game.</param>
    public static string GroupName(int gameId) => $"pic-{gameId}";

    /// <summary>Shows the page.</summary>
    /// <param name="done">The code of the post that sent the browser here; anything outside the fixed set is ignored.</param>
    /// <param name="view">The view to show; <c>all</c> is the only value that is not the default, and anything else is the default.</param>
    public void OnGet(string? done, string? view)
    {
        View = ViewOf(view);
        Load();
        Notice = OwnerResponses.NoticeFor(done);
        NoticeIsProblem = Notice is not null && OwnerResponses.IsProblem(done);
        UndoRevision = done == "saved" ? _owner.UndoableRevision : null;
    }

    /// <summary>Sets which picture one game is drawn from.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="choice">The kind of picture, written as its name; anything else is refused. A form posted without script names the field after the game instead.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostChooseAsync(long revision, int gameId, string? choice, string? view, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var now = _time.GetUtcNow();
        var kind = ParseChoice(choice ?? (Request.HasFormContentType ? Request.Form[GroupName(gameId)].FirstOrDefault() : null));

        var result = await _owner.MutateAsync(
            revision,
            data => kind is { } known ? ChooseInSnapshot(data, snapshot, gameId, known, now) : OwnerChange.Refused(data, "no-picture"),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId), extra: StateAfter(result, gameId), view: ViewQueryOf(view));
    }

    /// <summary>Records that one game's pictures look right.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostLooksRightAsync(long revision, int gameId, string? view, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var now = _time.GetUtcNow();

        var result = await _owner.MutateAsync(
            revision,
            data => LooksRightInSnapshot(data, snapshot, gameId, now),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId), extra: StateAfter(result, gameId), view: ViewQueryOf(view));
    }

    /// <summary>Takes back the latest change, when nothing was written after it.</summary>
    /// <param name="revision">The revision the change produced, which the Undo form carries.</param>
    /// <param name="gameId">The game the change was about, so the answer can tell how that game stands now; missing when not known.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostUndoAsync(long revision, int? gameId, CancellationToken cancellationToken)
    {
        var result = await _owner.UndoAsync(revision, cancellationToken);

        return OwnerResponses.For(this, result, PagePath, NoticeAnchor, extra: StateAfter(result, gameId));
    }

    private static PicturesView ViewOf(string? view) =>
        string.Equals(view, AllViewValue, StringComparison.Ordinal) ? PicturesView.All : PicturesView.WorthALook;

    private static string? ViewQueryOf(string? view) => ViewOf(view) == PicturesView.All ? AllViewValue : null;

    private static PictureChoice? ParseChoice(string? text) =>
        text is not null && Enum.GetNames<PictureChoice>().Contains(text, StringComparer.Ordinal)
            ? Enum.Parse<PictureChoice>(text)
            : null;

    private static OwnerChange ChooseInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int gameId, PictureChoice choice, DateTimeOffset now)
    {
        var item = snapshot?.Items.FirstOrDefault(candidate => candidate.GameId == gameId);

        if (snapshot is null || item is null)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        return PictureEdits.Choose(data, gameId, choice, PictureRows.TitleOf(item), item.VersionImageUrl, SnapshotMapper.MainPictureUrl(item, snapshot), now);
    }

    private static OwnerChange LooksRightInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int gameId, DateTimeOffset now)
    {
        var item = snapshot?.Items.FirstOrDefault(candidate => candidate.GameId == gameId);

        if (snapshot is null || item is null)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        return PictureEdits.LooksRight(data, gameId, PictureRows.TitleOf(item), item.VersionImageUrl, SnapshotMapper.MainPictureUrl(item, snapshot), now);
    }

    private static string RowAnchor(int gameId) => $"game-{gameId}";

    private Dictionary<string, object?> StateAfter(OwnerResult result, int? gameId)
    {
        var state = new Dictionary<string, object?>();

        if (result.Data is null || _collection.Current.Snapshot is not { } snapshot)
        {
            return state;
        }

        var rows = PictureRows.Build(snapshot, _rules, result.Data, _time.GetUtcNow());
        var counts = PictureRows.CountsOf(rows);

        state["toCheck"] = counts.ToCheck;
        state["candidates"] = counts.Candidates;
        state["total"] = counts.Total;

        if (gameId is { } id && rows.FirstOrDefault(row => row.GameId == id) is { } row)
        {
            state["resolved"] = row.Look.Resolved;
        }

        return state;
    }

    private void Load()
    {
        if (_owner.State is not OwnerDataState.Ready ready)
        {
            return;
        }

        Readable = true;
        Revision = ready.Data.Revision;

        if (_collection.Current.Snapshot is not { } snapshot)
        {
            return;
        }

        var rows = PictureRows.Build(snapshot, _rules, ready.Data, _time.GetUtcNow());

        Counts = PictureRows.CountsOf(rows);
        Games = View == PicturesView.All ? rows : PictureRows.WorthALookList(rows);
    }
}
