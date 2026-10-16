using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
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

    /// <summary>One game on its own, with its location picker under the picture choices.</summary>
    Single,
}

/// <summary>
/// The owner's pictures page: the games worth a look, or every game, each with the four pictures it can be drawn from, shown
/// as they would look, or one game on its own with the picker of its location. Every post goes through the single owner-data writer and is answered by <see cref="OwnerResponses"/>, so
/// the page works without scripts.
/// </summary>
public class PicturesModel : OwnerPage
{
    private const string PagePath = "/Owner/Pictures";
    private const string NoticeAnchor = "owner-notice";
    private const string AllViewValue = "all";
    private const string PicturesReturn = LocationPickerModel.ReturnToPictures;

    private readonly OwnerDataService _owner;
    private readonly CollectionStore _collection;
    private readonly ArtRules _rules;
    private readonly TimeProvider _time;
    private readonly OwnerCheckFetcher _fetcher;
    private readonly StagedItems<StagedLink> _staged;
    private readonly ArtCache _cache;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data.</param>
    /// <param name="collection">The collection visitors see, which lists the games.</param>
    /// <param name="rules">The rules that decide what the automatic pick draws.</param>
    /// <param name="time">The clock that decides whether a linked picture counts as dead.</param>
    /// <param name="fetcher">Fetches and processes the picture at an address the owner pasted, on a fetch slot of its own that the background work never holds.</param>
    /// <param name="staged">Holds a previewed picture until the owner uses or discards it.</param>
    /// <param name="cache">The directory of stored pictures, which a used picture is written to.</param>
    public PicturesModel(
        OwnerDataService owner,
        CollectionStore collection,
        ArtRules rules,
        TimeProvider time,
        OwnerCheckFetcher fetcher,
        StagedItems<StagedLink> staged,
        ArtCache cache)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(fetcher);
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(cache);

        _owner = owner;
        _collection = collection;
        _rules = rules;
        _time = time;
        _fetcher = fetcher;
        _staged = staged;
        _cache = cache;
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

    /// <summary>The game of the single-game view, or null when the page lists games.</summary>
    public int? SingleGameId { get; private set; }

    /// <summary>The location picker of every copy of the game the single-game view shows, by collection entry.</summary>
    public IReadOnlyDictionary<long, LocationPickerModel> Pickers { get; private set; } = new Dictionary<long, LocationPickerModel>();

    /// <summary>Whether the owner has defined at least one location, which decides whether the picker has places to offer.</summary>
    public bool HasLocations { get; private set; }

    /// <summary>How many games the page's two views and its counter are made from.</summary>
    public PictureCounts Counts { get; private set; } = new(0, 0, 0);

    /// <summary>The words for the outcome of the post that sent the browser here, or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether <see cref="Notice"/> reports a problem.</summary>
    public bool NoticeIsProblem { get; private set; }

    /// <summary>The revision of the change the Undo form would take back, or null when there is none to offer.</summary>
    public long? UndoRevision { get; private set; }

    /// <summary>The preview of a linked picture the single-game view shows, or null when there is none.</summary>
    public LinkPreview? Preview { get; private set; }

    /// <summary>The token of the staged picture <see cref="Preview"/> was made from; it is only set when <see cref="Preview"/> is not null.</summary>
    public string? PreviewToken { get; private set; }

    /// <summary>The value of the <c>view</c> query that selects the view of every game.</summary>
    public static string AllViewQuery => AllViewValue;

    /// <summary>The name the radios of one copy share, which is also the field a form posted without script carries.</summary>
    /// <param name="collectionId">The collection entry of the copy.</param>
    public static string GroupName(long collectionId) => $"pic-{collectionId}";

    /// <summary>The id of the row of one copy, which a form posted without script returns to.</summary>
    /// <param name="collectionId">The collection entry of the copy.</param>
    public static string RowId(long collectionId) => RowAnchor(collectionId);

    /// <summary>Shows the page.</summary>
    /// <param name="done">The code of the post that sent the browser here; anything outside the fixed set is ignored.</param>
    /// <param name="view">The view to show; <c>all</c> is the only value that is not the default, and anything else is the default.</param>
    /// <param name="game">The game to show on its own; read as a positive whole number and looked up, so anything else shows that the game is gone.</param>
    /// <param name="preview">The token of a previewed linked picture; it is looked up among the staged pictures and never echoed, and a text of any other shape is ignored.</param>
    /// <param name="size">For a picture that was too large, the size in bytes the picture host announced; it is only ever shown as a number of megabytes.</param>
    public void OnGet(string? done, string? view, string? game, string? preview, long? size)
    {
        View = game is null ? ViewOf(view) : PicturesView.Single;
        SingleGameId = game is null ? null : ParseGameId(game);
        Load();
        Notice = OwnerResponses.LinkProblemOf(done) is { } problem
            ? OwnerStrings.LinkError(problem, _fetcher.AllowedHost, size is > 0 ? size : null, _fetcher.MaxBytes)
            : OwnerResponses.NoticeFor(done);
        NoticeIsProblem = Notice is not null && OwnerResponses.IsProblem(done);
        UndoRevision = done is "saved" or "link-removed" ? _owner.UndoableRevision : null;
        LoadPreview(preview);
    }

    /// <summary>Sets which picture one copy of a game is drawn from; no other copy changes.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The collection entry of the copy; it has to be an entry of that game in the collection, or the choice is refused.</param>
    /// <param name="choice">The kind of picture, written as its name; anything else is refused. A form posted without script names the field after the copy instead.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostChooseAsync(long revision, int gameId, long collectionId, string? choice, string? view, int? game, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var now = _time.GetUtcNow();
        var kind = ParseChoice(choice ?? (Request.HasFormContentType ? Request.Form[GroupName(collectionId)].FirstOrDefault() : null));

        var result = await _owner.MutateAsync(
            revision,
            data => kind is { } known ? ChooseInSnapshot(data, snapshot, gameId, collectionId, known, now) : OwnerChange.Refused(data, "no-picture"),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(collectionId), extra: StateAfter(result, collectionId), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>Records that one copy's pictures look right; no other copy changes.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The collection entry of the copy; it has to be an entry of that game in the collection, or the post is refused.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostLooksRightAsync(long revision, int gameId, long collectionId, string? view, int? game, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var now = _time.GetUtcNow();

        var result = await _owner.MutateAsync(
            revision,
            data => LooksRightInSnapshot(data, snapshot, gameId, collectionId, now),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(collectionId), extra: StateAfter(result, collectionId), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>
    /// Removes the picture link of a game and the record of its stored copy. When the link was the choice, the choice falls back
    /// to Automatic. The change can be undone, which puts the link back and fetches its picture again.
    /// </summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostRemoveLinkAsync(long revision, int gameId, string? view, int? game, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var item = snapshot?.Items.FirstOrDefault(candidate => candidate.GameId == gameId);

        var result = await _owner.MutateAsync(
            revision,
            data => item is null ? OwnerChange.Refused(data, "unknown-game") : PictureEdits.RemoveLink(data, gameId, PictureRows.TitleOf(item)),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, GameAnchor(gameId), extra: StateAfter(result, FirstEntryOf(gameId), withLinkTile: true), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>Takes back the latest change, when nothing was written after it.</summary>
    /// <param name="revision">The revision the change produced, which the Undo form carries.</param>
    /// <param name="gameId">The game the change was about, so the answer can tell how that game stands now; missing when not known.</param>
    /// <param name="collectionId">The copy the change was about, which the answer reports on; missing for a change that was made for the whole game, which the answer then reports on the game's first copy.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostUndoAsync(long revision, int? gameId, long? collectionId, int? game, CancellationToken cancellationToken)
    {
        var result = await _owner.UndoAsync(revision, cancellationToken);

        if (result.Outcome == OwnerOutcome.Saved)
        {
            result = await FetchLinksWithoutCopiesAsync(result, cancellationToken);
        }

        var entry = EntryOf(collectionId, gameId ?? game);
        var anchor = entry is { } row ? RowAnchor(row) : NoticeAnchor;

        return OwnerResponses.For(this, result, PagePath, game is null && collectionId is null ? NoticeAnchor : anchor, extra: StateAfter(result, entry, withLinkTile: true), game: KnownGame(game));
    }

    /// <summary>
    /// Fetches the picture at an address the owner pasted, processes it and stages it for a preview. Nothing is stored. A script
    /// gets the preview as JSON; a form posted without script is sent to the game's own view, which renders the preview from the
    /// staged picture.
    /// </summary>
    /// <param name="gameId">The game the link is for.</param>
    /// <param name="link">The address as the owner typed it.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    public async Task<IActionResult> OnPostCheckLinkAsync(int gameId, string? link, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;

        if (snapshot is null || !snapshot.Items.Any(item => item.GameId == gameId))
        {
            return Refused("unknown-game", gameId, null, gameId);
        }

        var check = await _fetcher.CheckAsync(link, cancellationToken);

        if (check is not LinkCheck.Ready ready)
        {
            var failed = check as LinkCheck.Failed;

            return LinkRefused(gameId, failed?.Problem ?? LinkProblem.FetchFailed, failed?.Bytes);
        }

        var preview = LinkPreview.Build(snapshot, gameId, _rules, ready.Link.Record);
        var variant = preview is null ? null : ready.Link.Variants.FirstOrDefault(candidate => candidate.Name == preview.ArtFileName);

        if (preview is null || variant is null)
        {
            return Refused("link-failed", gameId, null, gameId);
        }

        var token = _staged.Stage(new StagedLink(gameId, ready.Link, variant));

        return OwnerResponses.AsksForJson(Request)
            ? new JsonResult(PreviewJson(token, preview))
            : RedirectToPage(PagePath, pageHandler: null, routeValues: new { game = gameId, preview = token }, fragment: GameAnchor(gameId));
    }

    /// <summary>
    /// Uses the previewed picture: its files are written to the art directory first, then the link and its record are stored as
    /// the game's own picture, replacing any link the game had. The staged picture is forgotten once the change is saved.
    /// </summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="token">The token of the staged picture.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostUseLinkAsync(long revision, int gameId, string? token, string? view, int? game, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var item = snapshot?.Items.FirstOrDefault(candidate => candidate.GameId == gameId);

        if (snapshot is null || item is null)
        {
            return Refused("unknown-game", gameId, view, game);
        }

        if (!_staged.TryGet(token, out var staged) || staged.GameId != gameId)
        {
            return Refused("link-expired", gameId, view, game);
        }

        if (!TryStore(staged.Link))
        {
            return Refused("save-failed", gameId, view, game);
        }

        var result = await _owner.MutateAsync(
            revision,
            data => PictureEdits.UseLink(data, gameId, staged.Link.Url, staged.Link.Record, PictureRows.TitleOf(item), item.VersionImageUrl, SnapshotMapper.MainPictureUrl(item, snapshot)),
            cancellationToken);

        if (result.Outcome == OwnerOutcome.Saved)
        {
            _staged.Remove(token);
        }

        return OwnerResponses.For(this, result, PagePath, GameAnchor(gameId), extra: StateAfter(result, FirstEntryOf(gameId), withLinkTile: true), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>Throws a previewed picture away. Nothing was stored, so nothing else changes.</summary>
    /// <param name="gameId">The game the preview was for, so a form posted without script returns to its view.</param>
    /// <param name="token">The token of the staged picture; one that is not the shape of a token or is unknown does nothing.</param>
    public IActionResult OnPostDiscardPreview(int gameId, string? token)
    {
        _staged.Remove(token);

        return OwnerResponses.AsksForJson(Request)
            ? new JsonResult(new Dictionary<string, object?> { ["outcome"] = "discarded" })
            : RedirectToPage(PagePath, pageHandler: null, routeValues: new { game = KnownGame(gameId) }, fragment: GameAnchor(gameId));
    }

    private static PicturesView ViewOf(string? view) =>
        string.Equals(view, AllViewValue, StringComparison.Ordinal) ? PicturesView.All : PicturesView.WorthALook;

    private static string? ViewQueryOf(string? view, int? game = null) =>
        game is null && ViewOf(view) == PicturesView.All ? AllViewValue : null;

    private static int? ParseGameId(string text) =>
        int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private int? KnownGame(int? game) =>
        game is { } id && _collection.Current.Snapshot?.Items.Any(item => item.GameId == id) == true ? id : null;

    private static PictureChoice? ParseChoice(string? text) =>
        text is not null && Enum.GetNames<PictureChoice>().Contains(text, StringComparer.Ordinal)
            ? Enum.Parse<PictureChoice>(text)
            : null;

    private static OwnerChange ChooseInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int gameId, long collectionId, PictureChoice choice, DateTimeOffset now)
    {
        if (snapshot is null || CopyOf(snapshot, gameId, collectionId) is not { } copy)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        return PictureEdits.Choose(
            data,
            gameId,
            collectionId,
            copy.Entries,
            choice,
            PictureRows.TitleOf(copy.Item),
            copy.Item.VersionImageUrl,
            SnapshotMapper.MainPictureUrl(copy.Item, snapshot),
            now);
    }

    private static OwnerChange LooksRightInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int gameId, long collectionId, DateTimeOffset now)
    {
        if (snapshot is null || CopyOf(snapshot, gameId, collectionId) is not { } copy)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        return PictureEdits.LooksRight(
            data,
            gameId,
            collectionId,
            copy.Entries,
            PictureRows.TitleOf(copy.Item),
            copy.Item.VersionImageUrl,
            SnapshotMapper.MainPictureUrl(copy.Item, snapshot),
            now);
    }

    /// <summary>The copy a post names, with every collection entry of its game, or null when the game does not own that entry.</summary>
    private static (SnapshotItem Item, IReadOnlyList<long> Entries)? CopyOf(CollectionSnapshot snapshot, int gameId, long collectionId)
    {
        var copies = snapshot.Items.Where(candidate => candidate.GameId == gameId).OrderBy(candidate => candidate.CollectionId).ToList();
        var item = copies.FirstOrDefault(candidate => candidate.CollectionId == collectionId);

        return item is null ? null : (item, [.. copies.Select(copy => copy.CollectionId).Distinct()]);
    }

    /// <summary>The first collection entry of a game, which a game-wide link post reports on and returns to, or null when the game is not in the collection.</summary>
    private long? FirstEntryOf(int gameId) =>
        _collection.Current.Snapshot?.Items.Where(item => item.GameId == gameId).Select(item => (long?)item.CollectionId).Min();

    /// <summary>The entry a post reports on: the posted copy when it is an entry of the game, else the game's first copy.</summary>
    private long? EntryOf(long? collectionId, int? gameId)
    {
        var snapshot = _collection.Current.Snapshot;

        if (snapshot is null)
        {
            return null;
        }

        if (collectionId is { } posted && snapshot.Items.Any(item => item.CollectionId == posted && (gameId is null || item.GameId == gameId)))
        {
            return posted;
        }

        return gameId is { } id ? FirstEntryOf(id) : null;
    }

    private IActionResult Refused(string statusKey, int gameId, string? view = null, int? game = null) =>
        OwnerResponses.For(
            this,
            new OwnerResult(OwnerOutcome.Refused, null, statusKey),
            PagePath,
            GameAnchor(gameId),
            view: ViewQueryOf(view, game),
            game: KnownGame(game));

    /// <summary>
    /// After an undo has put a link back, a copy of its picture that was tidied away in the meantime is forgotten, so the link
    /// shows as being fetched, and the background work is asked to fetch it again now.
    /// </summary>
    private async Task<OwnerResult> FetchLinksWithoutCopiesAsync(OwnerResult undone, CancellationToken cancellationToken)
    {
        await _owner.MutateSystemAsync(data => WithoutMissingCopies(data), cancellationToken);
        _owner.RequestLinkWork();

        return _owner.State is OwnerDataState.Ready ready ? undone with { Data = ready.Data } : undone;
    }

    private OwnerData? WithoutMissingCopies(OwnerData data)
    {
        var missing = data.Games.Where(game => game.Picture?.Link is { Record: { } record } && record.Files?.All(file => _cache.Has(file.Name)) != true).ToList();

        if (missing.Count == 0)
        {
            return null;
        }

        return data with
        {
            Games =
            [
                .. data.Games.Select(game => missing.Contains(game) ? game with { Picture = game.Picture! with { Link = game.Picture.Link! with { Record = null } } } : game),
            ],
        };
    }

    private IActionResult LinkRefused(int gameId, LinkProblem problem, long? announcedBytes)
    {
        var key = OwnerStrings.LinkKey(problem);
        var refusal = new OwnerResult(OwnerOutcome.Refused, null, key);

        if (!OwnerResponses.AsksForJson(Request))
        {
            return RedirectToPage(
                PagePath,
                pageHandler: null,
                routeValues: new { done = OwnerResponses.DoneCode(refusal), game = gameId, size = problem == LinkProblem.TooLarge ? announcedBytes : null },
                fragment: GameAnchor(gameId));
        }

        var extra = new Dictionary<string, object?>
        {
            ["problem"] = CamelName(problem),
            ["message"] = OwnerStrings.LinkError(problem, _fetcher.AllowedHost, announcedBytes, _fetcher.MaxBytes),
        };

        return OwnerResponses.For(this, refusal, PagePath, GameAnchor(gameId), extra: extra, game: gameId);
    }

    private bool TryStore(ProcessedLink link)
    {
        try
        {
            foreach (var variant in link.Variants)
            {
                _cache.Write(variant);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void LoadPreview(string? token)
    {
        if (!StagedItems<StagedLink>.IsToken(token))
        {
            return;
        }

        if (!_staged.TryGet(token, out var staged))
        {
            if (Notice is null)
            {
                Notice = OwnerStrings.Message("link-expired");
                NoticeIsProblem = true;
            }

            return;
        }

        if (View != PicturesView.Single || staged.GameId != SingleGameId || _collection.Current.Snapshot is not { } snapshot)
        {
            return;
        }

        Preview = LinkPreview.Build(snapshot, staged.GameId, _rules, staged.Link.Record);
        PreviewToken = Preview is null ? null : token;
    }

    private static Dictionary<string, object?> PreviewJson(string token, LinkPreview preview) =>
        new()
        {
            ["outcome"] = "ready",
            ["token"] = token,
            ["previewUrl"] = $"{OwnerEndpoints.PreviewPath}/{token}",
            ["preview"] = new Dictionary<string, object?>
            {
                ["title"] = preview.Title,
                ["boxWidthMm"] = preview.BoxWidthMm,
                ["boxHeightMm"] = preview.BoxHeightMm,
                ["boxDepthMm"] = preview.BoxDepthMm,
                ["fit"] = CamelName(preview.Fit),
                ["edges"] = preview.Edges is { } edges
                    ? new Dictionary<string, string> { ["top"] = edges.Top, ["right"] = edges.Right, ["bottom"] = edges.Bottom, ["left"] = edges.Left }
                    : null,
                ["artWidth"] = preview.ArtWidth,
                ["artHeight"] = preview.ArtHeight,
                ["shapeSource"] = CamelName(preview.ShapeSource),
                ["shapeText"] = OwnerStrings.ShapeText(preview.ShapeSource),
                ["verdict"] = CamelName(preview.Verdict),
                ["verdictText"] = OwnerStrings.VerdictTag(preview.Verdict),
                ["note"] = OwnerStrings.PreviewNote(preview.Verdict),
                ["spineWidthMm"] = preview.SpineWidthMm,
                ["spineHeightMm"] = preview.SpineHeightMm,
                ["spineLabel"] = preview.SpineLabel,
                ["spineBackground"] = preview.Spine.Background,
                ["spineText"] = preview.Spine.Text,
            },
        };

    private static Dictionary<string, object?> LinkTileJson(PictureRow row)
    {
        var tile = row.Tile(PictureChoice.Link);

        return new Dictionary<string, object?>
        {
            ["state"] = tile.Fetching ? "fetching" : tile.Url is not null ? "picture" : "none",
            ["url"] = tile.Url,
            ["width"] = tile.Width,
            ["height"] = tile.Height,
            ["verdictText"] = tile.Verdict is { } verdict ? OwnerStrings.VerdictTag(verdict) : null,
            ["address"] = tile.LinkAddress,
            ["fullAddress"] = tile.LinkFullAddress,
        };
    }

    private static string CamelName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();

        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }

    private static string RowAnchor(long collectionId) => $"entry-{collectionId}";

    private string GameAnchor(int gameId) => FirstEntryOf(gameId) is { } first ? RowAnchor(first) : NoticeAnchor;

    private Dictionary<string, object?> StateAfter(OwnerResult result, long? collectionId, bool withLinkTile = false)
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

        if (collectionId is { } id && rows.FirstOrDefault(row => row.CollectionId == id) is { } row)
        {
            state["resolved"] = row.Look.Resolved;
            state["choice"] = row.Choice.ToString();
            state["fetching"] = row.Look.Fetching;

            if (withLinkTile && result.Outcome == OwnerOutcome.Saved)
            {
                state["linkTile"] = LinkTileJson(row);
            }
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
        HasLocations = ready.Data.Locations.Count > 0;

        if (View == PicturesView.Single)
        {
            LoadSingle(rows, snapshot, ready.Data);

            return;
        }

        Games = View == PicturesView.All ? rows : PictureRows.WorthALookList(rows);
    }

    private void LoadSingle(IReadOnlyList<PictureRow> rows, CollectionSnapshot snapshot, OwnerData data)
    {
        var copies = SingleGameId is { } id ? rows.Where(candidate => candidate.GameId == id).ToList() : [];

        if (copies.Count == 0)
        {
            return;
        }

        Games = copies;

        var pickers = new Dictionary<long, LocationPickerModel>();

        foreach (var copy in copies)
        {
            if (LocationsModel.RowForCopy(snapshot, data, _collection.Current.Items, copy.CollectionId) is { } placement)
            {
                pickers[copy.CollectionId] = LocationPickerModel.For(placement, data.Locations, data.Revision, OwnerStrings.LocationLabel, PicturesReturn);
            }
        }

        Pickers = pickers;
    }
}
