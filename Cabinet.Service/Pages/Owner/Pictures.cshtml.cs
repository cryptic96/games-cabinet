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
    private readonly OwnerLinkFetcher _fetcher;
    private readonly StagedItems<StagedLink> _staged;
    private readonly ArtCache _cache;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data.</param>
    /// <param name="collection">The collection visitors see, which lists the games.</param>
    /// <param name="rules">The rules that decide what the automatic pick draws.</param>
    /// <param name="time">The clock that decides whether a linked picture counts as dead.</param>
    /// <param name="fetcher">Fetches and processes the picture at an address the owner pasted.</param>
    /// <param name="staged">Holds a previewed picture until the owner uses or discards it.</param>
    /// <param name="cache">The directory of stored pictures, which a used picture is written to.</param>
    public PicturesModel(
        OwnerDataService owner,
        CollectionStore collection,
        ArtRules rules,
        TimeProvider time,
        OwnerLinkFetcher fetcher,
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

    /// <summary>The location picker of the game the single-game view shows, or null when there is none to draw.</summary>
    public LocationPickerModel? Picker { get; private set; }

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

    /// <summary>The name the radios of one game share, which is also the field a form posted without script carries.</summary>
    /// <param name="gameId">The game.</param>
    public static string GroupName(int gameId) => $"pic-{gameId}";

    /// <summary>Shows the page.</summary>
    /// <param name="done">The code of the post that sent the browser here; anything outside the fixed set is ignored.</param>
    /// <param name="view">The view to show; <c>all</c> is the only value that is not the default, and anything else is the default.</param>
    /// <param name="game">The game to show on its own; read as a positive whole number and looked up, so anything else shows that the game is gone.</param>
    /// <param name="preview">The token of a previewed linked picture; it is looked up among the staged pictures and never echoed, and a text of any other shape is ignored.</param>
    public void OnGet(string? done, string? view, string? game, string? preview)
    {
        View = game is null ? ViewOf(view) : PicturesView.Single;
        SingleGameId = game is null ? null : ParseGameId(game);
        Load();
        Notice = OwnerResponses.NoticeFor(done);
        NoticeIsProblem = Notice is not null && OwnerResponses.IsProblem(done);
        UndoRevision = done == "saved" ? _owner.UndoableRevision : null;
        LoadPreview(preview);
    }

    /// <summary>Sets which picture one game is drawn from.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="choice">The kind of picture, written as its name; anything else is refused. A form posted without script names the field after the game instead.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostChooseAsync(long revision, int gameId, string? choice, string? view, int? game, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var now = _time.GetUtcNow();
        var kind = ParseChoice(choice ?? (Request.HasFormContentType ? Request.Form[GroupName(gameId)].FirstOrDefault() : null));

        var result = await _owner.MutateAsync(
            revision,
            data => kind is { } known ? ChooseInSnapshot(data, snapshot, gameId, known, now) : OwnerChange.Refused(data, "no-picture"),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId), extra: StateAfter(result, gameId), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>Records that one game's pictures look right.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="view">The view the form was on, so a form posted without script returns to it.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostLooksRightAsync(long revision, int gameId, string? view, int? game, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var now = _time.GetUtcNow();

        var result = await _owner.MutateAsync(
            revision,
            data => LooksRightInSnapshot(data, snapshot, gameId, now),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId), extra: StateAfter(result, gameId), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>Takes back the latest change, when nothing was written after it.</summary>
    /// <param name="revision">The revision the change produced, which the Undo form carries.</param>
    /// <param name="gameId">The game the change was about, so the answer can tell how that game stands now; missing when not known.</param>
    /// <param name="game">The game of the single-game view the form was on, so a form posted without script returns to it.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostUndoAsync(long revision, int? gameId, int? game, CancellationToken cancellationToken)
    {
        var result = await _owner.UndoAsync(revision, cancellationToken);

        return OwnerResponses.For(this, result, PagePath, game is null ? NoticeAnchor : RowAnchor(game.Value), extra: StateAfter(result, gameId), game: KnownGame(game));
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

        if (await _fetcher.CheckAsync(link, cancellationToken) is not LinkCheck.Ready ready)
        {
            return Refused("link-failed", gameId, null, gameId);
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
            : RedirectToPage(PagePath, pageHandler: null, routeValues: new { game = gameId, preview = token }, fragment: RowAnchor(gameId));
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

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId), extra: StateAfter(result, gameId, withLinkTile: true), view: ViewQueryOf(view, game), game: KnownGame(game));
    }

    /// <summary>Throws a previewed picture away. Nothing was stored, so nothing else changes.</summary>
    /// <param name="gameId">The game the preview was for, so a form posted without script returns to its view.</param>
    /// <param name="token">The token of the staged picture; one that is not the shape of a token or is unknown does nothing.</param>
    public IActionResult OnPostDiscardPreview(int gameId, string? token)
    {
        _staged.Remove(token);

        return OwnerResponses.AsksForJson(Request)
            ? new JsonResult(new Dictionary<string, object?> { ["outcome"] = "discarded" })
            : RedirectToPage(PagePath, pageHandler: null, routeValues: new { game = KnownGame(gameId) }, fragment: RowAnchor(gameId));
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

    private IActionResult Refused(string statusKey, int gameId, string? view = null, int? game = null) =>
        OwnerResponses.For(
            this,
            new OwnerResult(OwnerOutcome.Refused, null, statusKey),
            PagePath,
            RowAnchor(gameId),
            view: ViewQueryOf(view, game),
            game: KnownGame(game));

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

    private static string RowAnchor(int gameId) => $"game-{gameId}";

    private Dictionary<string, object?> StateAfter(OwnerResult result, int? gameId, bool withLinkTile = false)
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
        var row = SingleGameId is { } id ? rows.FirstOrDefault(candidate => candidate.GameId == id) : null;

        if (row is null)
        {
            return;
        }

        Games = [row];

        if (LocationsModel.RowFor(snapshot, data, _collection.Current.Items, row.GameId) is { } placement)
        {
            Picker = LocationPickerModel.For(placement, data.Locations, data.Revision, OwnerStrings.LocationLabel, PicturesReturn);
        }
    }
}
