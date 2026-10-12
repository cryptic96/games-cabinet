using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;

namespace Cabinet.Service.Pages.Owner;

/// <summary>One game on the locations page, with what the picker next to it needs.</summary>
/// <param name="GameId">The game.</param>
/// <param name="Title">The title to show.</param>
/// <param name="IsExpansion">Whether the game is an expansion.</param>
/// <param name="OwnLocationId">The location the owner gave the game itself, or null.</param>
/// <param name="BaseTitle">The title of the owned base game an expansion stands beside, or null for any other game.</param>
/// <param name="EffectiveLocation">The name of the location the game is shown in, or null when it is not placed.</param>
public sealed record LocationGameRow(
    int GameId,
    string Title,
    bool IsExpansion,
    string? OwnLocationId,
    string? BaseTitle,
    string? EffectiveLocation);

/// <summary>
/// The owner's locations page: the list of locations with a form to add one, and every game with a picker that says where it
/// is kept. Every post goes through the single owner-data writer and is answered by <see cref="OwnerResponses"/>, so the page
/// works with and without scripts.
/// </summary>
public class LocationsModel : OwnerPage
{
    private const string PagePath = "/Owner/Locations";
    private const string AddAnchor = "new-location";

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
    public void OnGet(string? done)
    {
        Load();
        Notice = OwnerResponses.NoticeFor(done);
        NoticeIsProblem = Notice is not null && OwnerResponses.IsProblem(done);
        UndoRevision = done == "saved" ? _owner.UndoableRevision : null;
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

        return OwnerResponses.For(this, result, PagePath, AddAnchor);
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

    private static string RowAnchor(int gameId) => $"game-{gameId}";

    private static string TitleOf(SnapshotItem item) =>
        string.IsNullOrWhiteSpace(item.Title) ? $"Game {item.GameId}" : item.Title.Trim();

    private void Load()
    {
        if (_owner.State is not OwnerDataState.Ready ready)
        {
            return;
        }

        Readable = true;
        Revision = ready.Data.Revision;
        Locations = ready.Data.Locations;

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

        Games =
        [
            .. snapshot.Items
                .GroupBy(item => item.GameId)
                .Select(group => group.First())
                .Select(item => new LocationGameRow(
                    item.GameId,
                    titles[item.GameId],
                    item.Kind == ItemKind.Expansion,
                    overlay.OwnLocationId(item.GameId),
                    overlay.OwnedBaseGameId(item.GameId) is { } baseId ? titles[baseId] : null,
                    overlay.EffectiveLocationName(item.GameId)))
                .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.GameId),
        ];
    }
}
