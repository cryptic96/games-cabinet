using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace Cabinet.Service.Pages.Owner;

/// <summary>One game on the locations page, with what the picker next to it needs.</summary>
/// <param name="GameId">The game.</param>
/// <param name="Title">The title to show.</param>
/// <param name="IsExpansion">Whether the game is an expansion.</param>
/// <param name="OwnLocationId">The location the owner gave the game itself, or null.</param>
/// <param name="BaseTitle">The title of the owned base game an expansion stands beside, or null for any other game.</param>
/// <param name="EffectiveLocation">The name of the location the game is shown in, or null when it is not placed.</param>
/// <param name="BaseGameId">The owned base game an expansion stands beside, or null for any other game.</param>
/// <param name="EffectiveLocationId">The identifier of the location the game is shown in, or null when it is not placed.</param>
/// <param name="CoverUrl">The path of the small stored picture of the game's box, or null when it has none.</param>
/// <param name="SwatchColour">The colour of the game's spine as a hexadecimal colour, or null when it has none.</param>
/// <param name="CoverWidth">The width of the stored picture in pixels, or zero without a picture.</param>
/// <param name="CoverHeight">The height of the stored picture in pixels, or zero without a picture.</param>
public sealed record LocationGameRow(
    int GameId,
    string Title,
    bool IsExpansion,
    string? OwnLocationId,
    string? BaseTitle,
    string? EffectiveLocation,
    int? BaseGameId = null,
    string? EffectiveLocationId = null,
    string? CoverUrl = null,
    string? SwatchColour = null,
    int CoverWidth = 0,
    int CoverHeight = 0)
{
    /// <summary>Whether the game is an expansion that stands beside an owned base game, so that no location of its own means it follows the base.</summary>
    public bool FollowsBase => BaseGameId is not null;
}

/// <summary>
/// The owner's locations page: the list of locations with a form to add one, and every game with a picker that says where it
/// is kept. Every post goes through the single owner-data writer and is answered by <see cref="OwnerResponses"/>, so the page
/// works with and without scripts.
/// </summary>
public class LocationsModel : OwnerPage
{
    private const string PagePath = "/Owner/Locations";
    private const string AddAnchor = "new-location";
    private const string NoticeAnchor = "owner-notice";
    private const string RefusedPrefix = "refused-";
    private const string JsonMediaType = "application/json";
    private const string GamesAnchor = "games-heading";
    private const string LocationTargetPrefix = "location:";
    private const string NotPlacedTarget = "not-placed";
    private const string SameAsBaseTarget = "same-as-base";
    private const int CoverWidthPx = 240;

    private readonly OwnerDataService _owner;
    private readonly CollectionStore _collection;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data.</param>
    /// <param name="collection">The collection visitors see, which lists the games to place.</param>
    public LocationsModel(OwnerDataService owner, CollectionStore collection)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(collection);

        _owner = owner;
        _collection = collection;
    }

    /// <inheritdoc />
    public override string PageName => OwnerStrings.Locations;

    /// <summary>Whether the owner data could be read; the page offers no form while it cannot.</summary>
    public bool Readable { get; private set; }

    /// <summary>The revision the forms were built from.</summary>
    public long Revision { get; private set; }

    /// <summary>The locations in the order the owner set.</summary>
    public IReadOnlyList<OwnerLocation> Locations { get; private set; } = [];

    /// <summary>The items of the list, one per location in the same order, with what each one has to show.</summary>
    public IReadOnlyList<LocationItemModel> Items { get; private set; } = [];

    /// <summary>The words that explain why the name in the add form was refused, or null.</summary>
    public string? AddError { get; private set; }

    /// <summary>Whether the panel of the list is open: while it is empty and while the owner is working in it.</summary>
    public bool PanelOpen { get; private set; }

    /// <summary>Every owned game once, by title.</summary>
    public IReadOnlyList<LocationGameRow> Games { get; private set; } = [];

    /// <summary>The words for the outcome of the post that sent the browser here, or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether <see cref="Notice"/> reports a problem.</summary>
    public bool NoticeIsProblem { get; private set; }

    /// <summary>The revision of the change the Undo form would take back, or null when there is none to offer.</summary>
    public long? UndoRevision { get; private set; }

    /// <summary>Shows the page.</summary>
    /// <param name="done">The code of the post that sent the browser here; anything outside the fixed set is ignored.</param>
    /// <param name="item">The location the post was about; ignored unless it is one of the owner's locations.</param>
    /// <param name="confirm">The location the owner is about to delete; ignored unless it is one of the owner's locations.</param>
    public void OnGet(string? done, string? item, string? confirm)
    {
        Load();

        var notice = OwnerResponses.NoticeFor(done);
        var problem = notice is not null && OwnerResponses.IsProblem(done);
        var undoRevision = done == "saved" ? _owner.UndoableRevision : null;
        var itemId = KnownLocationId(item);
        var confirmId = KnownLocationId(confirm);
        var nameProblem = NameProblemKey(done);

        if (nameProblem is not null)
        {
            var text = OwnerStrings.Message(nameProblem);
            Items = [.. Items.Select(row => row.Location.Id == itemId ? row with { ErrorText = text } : row)];
            AddError = itemId is null ? text : null;
        }
        else if (itemId is not null && notice is not null)
        {
            Items = [.. Items.Select(row => row.Location.Id == itemId
                ? row with { StatusText = notice, StatusIsProblem = problem, UndoRevision = undoRevision }
                : row)];
        }
        else
        {
            Notice = notice;
            NoticeIsProblem = problem;
            UndoRevision = undoRevision;
        }

        if (confirmId is not null)
        {
            Items = [.. Items.Select(row => row.Location.Id == confirmId ? row with { ConfirmingDelete = true } : row)];
        }

        PanelOpen = Locations.Count == 0 || itemId is not null || confirmId is not null || nameProblem is not null;
    }

    /// <summary>Adds a location at the end of the list.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="name">The name the owner typed.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostAddLocationAsync(long revision, string? name, CancellationToken cancellationToken)
    {
        var result = await _owner.MutateAsync(
            revision,
            data => LocationEdits.Add(data, name ?? string.Empty, OwnerDataService.NewId),
            cancellationToken);

        var created = result.Outcome == OwnerOutcome.Saved ? result.Data?.Locations.LastOrDefault()?.Id : null;

        return RespondAtItem(result, created, created, AddAnchor);
    }

    /// <summary>Renames a location in place.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="id">The location to rename.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostRenameAsync(long revision, string? id, string? name, CancellationToken cancellationToken)
    {
        var result = await _owner.MutateAsync(
            revision,
            data => LocationEdits.Rename(data, id ?? string.Empty, name ?? string.Empty),
            cancellationToken);

        return RespondAtItem(result, id, null, AddAnchor);
    }

    /// <summary>Moves a location one place up the list.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="id">The location to move.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public Task<IActionResult> OnPostMoveUpAsync(long revision, string? id, CancellationToken cancellationToken) =>
        MoveAsync(revision, id, -1, cancellationToken);

    /// <summary>Moves a location one place down the list.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="id">The location to move.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public Task<IActionResult> OnPostMoveDownAsync(long revision, string? id, CancellationToken cancellationToken) =>
        MoveAsync(revision, id, 1, cancellationToken);

    /// <summary>
    /// Deletes a location that leaves no game not placed. A form posted without script for a location that would leave games not
    /// placed is sent to a page that asks first, and a script that asked in its own dialog posts to
    /// <see cref="OnPostDeleteConfirmedAsync"/> instead.
    /// </summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="id">The location to delete.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostDeleteAsync(long revision, string? id, CancellationToken cancellationToken)
    {
        var known = KnownLocationId(id);

        if (!AcceptsJson(Request) && known is not null && HoldsGames(known))
        {
            return RedirectToPage(PagePath, pageHandler: null, routeValues: new { confirm = known }, fragment: $"loc-{known}");
        }

        var snapshot = _collection.Current.Snapshot;
        var neighbour = NeighbourOf(id);
        var result = await _owner.MutateAsync(
            revision,
            data => LocationEdits.CountLeftNotPlaced(data, snapshot, id ?? string.Empty) > 0
                ? OwnerChange.Refused(data, "confirm-needed")
                : LocationEdits.Delete(data, snapshot, id ?? string.Empty),
            cancellationToken);

        return RespondAtItem(result, result.Outcome == OwnerOutcome.Saved ? neighbour : id, null, AddAnchor);
    }

    /// <summary>Deletes a location after the owner was told how many games would become not placed.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="id">The location to delete.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostDeleteConfirmedAsync(long revision, string? id, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var neighbour = NeighbourOf(id);
        var result = await _owner.MutateAsync(
            revision,
            data => LocationEdits.Delete(data, snapshot, id ?? string.Empty),
            cancellationToken);

        return RespondAtItem(result, result.Outcome == OwnerOutcome.Saved ? neighbour : id, null, AddAnchor);
    }

    /// <summary>Puts a game in a location, or takes it out of the one it is in.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game to move.</param>
    /// <param name="locationId">The location to put it in; empty for none of its own.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostAssignAsync(long revision, int gameId, string? locationId, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;

        var result = await _owner.MutateAsync(
            revision,
            data => AssignInSnapshot(data, snapshot, gameId, locationId),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId));
    }

    /// <summary>
    /// Moves the ticked games in one change: into one location, to no location of their own, or back to following their base game.
    /// Every game must be in the collection and the target must be one of the owner's locations or one of the two fixed words;
    /// otherwise nothing moves.
    /// </summary>
    /// <param name="revision">The revision the page was built from.</param>
    /// <param name="gameIds">The ticked games.</param>
    /// <param name="target">Where they go: <c>location:{id}</c>, <c>not-placed</c> or <c>same-as-base</c>.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostBulkMoveAsync(long revision, int[]? gameIds, string? target, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var ids = gameIds ?? [];

        var result = await _owner.MutateAsync(
            revision,
            data => BulkMoveInSnapshot(data, snapshot, ids, target),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, GamesAnchor);
    }

    /// <summary>Takes back the latest change, when nothing was written after it.</summary>
    /// <param name="revision">The revision the change produced, which the Undo form carries.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostUndoAsync(long revision, CancellationToken cancellationToken)
    {
        var result = await _owner.UndoAsync(revision, cancellationToken);

        return OwnerResponses.For(this, result, PagePath, NoticeAnchor);
    }

    private static bool AcceptsJson(HttpRequest request) =>
        request.Headers.Accept.Any(value => value is not null && value.Contains(JsonMediaType, StringComparison.OrdinalIgnoreCase));

    private static string? NameProblemKey(string? done) =>
        done is not null && done.StartsWith(RefusedPrefix, StringComparison.Ordinal) && OwnerStrings.IsNameProblem(done[RefusedPrefix.Length..])
            ? done[RefusedPrefix.Length..]
            : null;

    private static OwnerChange AssignInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int gameId, string? locationId)
    {
        var item = snapshot?.Items.FirstOrDefault(candidate => candidate.GameId == gameId);

        if (snapshot is null || item is null)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        var followsBase = OwnerOverlay.From(snapshot, data).OwnedBaseGameId(gameId) is not null;

        return LocationEdits.Assign(data, gameId, locationId, TitleOf(item), followsBase);
    }

    private static OwnerChange BulkMoveInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int[] gameIds, string? target)
    {
        var moveTarget = ParseTarget(target);

        if (moveTarget is null)
        {
            return OwnerChange.Refused(data, "unknown-location");
        }

        if (snapshot is null)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        var titles = new Dictionary<int, string>();

        foreach (var item in snapshot.Items)
        {
            titles.TryAdd(item.GameId, TitleOf(item));
        }

        var overlay = OwnerOverlay.From(snapshot, data);

        return LocationEdits.MoveMany(
            data,
            gameIds,
            moveTarget,
            gameId => titles.GetValueOrDefault(gameId),
            gameId => overlay.OwnedBaseGameId(gameId) is not null);
    }

    private static MoveTarget? ParseTarget(string? target) => target switch
    {
        NotPlacedTarget => MoveTarget.NotPlaced,
        SameAsBaseTarget => MoveTarget.SameAsBase,
        not null when target.StartsWith(LocationTargetPrefix, StringComparison.Ordinal) && target.Length > LocationTargetPrefix.Length
            => MoveTarget.Location(target[LocationTargetPrefix.Length..]),
        _ => null,
    };

    private static string RowAnchor(int gameId) => $"game-{gameId}";

    private static string TitleOf(SnapshotItem item) =>
        string.IsNullOrWhiteSpace(item.Title) ? $"Game {item.GameId}" : item.Title.Trim();

    private static List<LocationItemModel> ItemsFor(OwnerData data, CollectionSnapshot? snapshot)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (snapshot is not null)
        {
            var overlay = OwnerOverlay.From(snapshot, data);

            foreach (var gameId in snapshot.Items.Select(item => item.GameId).Distinct())
            {
                if (overlay.EffectiveLocationId(gameId) is { } locationId)
                {
                    counts[locationId] = counts.GetValueOrDefault(locationId) + 1;
                }
            }
        }

        return
        [
            .. data.Locations.Select((location, index) =>
            {
                var games = counts.GetValueOrDefault(location.Id);
                var notPlaced = games == 0 ? 0 : LocationEdits.CountLeftNotPlaced(data, snapshot, location.Id);

                return new LocationItemModel(location, index + 1, data.Locations.Count, games, notPlaced, data.Revision);
            }),
        ];
    }

    private async Task<IActionResult> MoveAsync(long revision, string? id, int step, CancellationToken cancellationToken)
    {
        var result = await _owner.MutateAsync(revision, data => LocationEdits.Move(data, id ?? string.Empty, step), cancellationToken);

        return RespondAtItem(result, id, null, AddAnchor);
    }

    private IActionResult RespondAtItem(OwnerResult result, string? id, string? createdId, string fallbackAnchor)
    {
        var known = id is not null
            && result.Data is { } data
            && data.Locations.Any(location => string.Equals(location.Id, id, StringComparison.Ordinal));
        var answer = OwnerResponses.For(this, result, PagePath, known ? $"loc-{id}" : fallbackAnchor, createdId);

        if (known && answer is RedirectToPageResult redirect)
        {
            redirect.RouteValues ??= new RouteValueDictionary();
            redirect.RouteValues["item"] = id;
        }

        return answer;
    }

    private string? NeighbourOf(string? id)
    {
        if (_owner.State is not OwnerDataState.Ready ready)
        {
            return null;
        }

        var locations = ready.Data.Locations;
        var index = locations.ToList().FindIndex(location => string.Equals(location.Id, id, StringComparison.Ordinal));

        if (index < 0)
        {
            return null;
        }

        return index + 1 < locations.Count ? locations[index + 1].Id : index > 0 ? locations[index - 1].Id : null;
    }

    private string? KnownLocationId(string? id) =>
        _owner.State is OwnerDataState.Ready ready
            ? ready.Data.Locations.FirstOrDefault(location => string.Equals(location.Id, id, StringComparison.Ordinal))?.Id
            : null;

    private bool HoldsGames(string id) =>
        _owner.State is OwnerDataState.Ready ready
        && LocationEdits.CountLeftNotPlaced(ready.Data, _collection.Current.Snapshot, id) > 0;

    private void Load()
    {
        if (_owner.State is not OwnerDataState.Ready ready)
        {
            return;
        }

        Readable = true;
        Revision = ready.Data.Revision;
        Locations = ready.Data.Locations;
        Items = ItemsFor(ready.Data, _collection.Current.Snapshot);

        if (_collection.Current.Snapshot is not { } snapshot)
        {
            return;
        }

        var overlay = OwnerOverlay.From(snapshot, ready.Data);
        var titles = new Dictionary<int, string>();

        foreach (var item in snapshot.Items)
        {
            titles.TryAdd(item.GameId, TitleOf(item));
        }

        var covers = new Dictionary<int, CabinetItem>();

        foreach (var cabinetItem in _collection.Current.Items)
        {
            covers.TryAdd(cabinetItem.BggId, cabinetItem);
        }

        var rows = snapshot.Items
            .GroupBy(item => item.GameId)
            .Select(group => group.First())
            .Select(item => RowOf(item, overlay, titles, covers))
            .ToList();

        Games = Nested(rows);
    }

    private static LocationGameRow RowOf(
        SnapshotItem item,
        OwnerOverlay overlay,
        IReadOnlyDictionary<int, string> titles,
        IReadOnlyDictionary<int, CabinetItem> covers)
    {
        var baseId = overlay.OwnedBaseGameId(item.GameId);
        var shown = covers.GetValueOrDefault(item.GameId);
        var variant = CoverOf(shown);
        var swatch = shown?.Colour?.Background;

        return new LocationGameRow(
            item.GameId,
            titles[item.GameId],
            item.Kind == ItemKind.Expansion,
            overlay.OwnLocationId(item.GameId),
            baseId is { } id && titles.TryGetValue(id, out var baseTitle) ? baseTitle : null,
            overlay.EffectiveLocationName(item.GameId),
            baseId,
            overlay.EffectiveLocationId(item.GameId),
            variant?.Url,
            RgbColour.TryParseHex(swatch, out _) ? swatch : null,
            variant?.Width ?? 0,
            variant?.Height ?? 0);
    }

    private static ArtVariant? CoverOf(CabinetItem? item)
    {
        var variants = item?.Art?.Variants;

        if (variants is null || variants.Count == 0)
        {
            return null;
        }

        return variants.Where(variant => variant.Width >= CoverWidthPx).OrderBy(variant => variant.Width).FirstOrDefault()
            ?? variants.OrderByDescending(variant => variant.Width).First();
    }

    private static List<LocationGameRow> Nested(List<LocationGameRow> rows)
    {
        var present = rows.Select(row => row.GameId).ToHashSet();
        var under = rows
            .Where(row => row.BaseGameId is { } baseId && present.Contains(baseId))
            .ToLookup(row => row.BaseGameId!.Value);
        var top = rows
            .Where(row => row.BaseGameId is not { } baseId || !present.Contains(baseId))
            .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.GameId);
        var ordered = new List<LocationGameRow>(rows.Count);

        foreach (var row in top)
        {
            ordered.Add(row);
            ordered.AddRange(under[row.GameId].OrderBy(child => child.Title, StringComparer.OrdinalIgnoreCase).ThenBy(child => child.GameId));
        }

        return ordered;
    }
}
