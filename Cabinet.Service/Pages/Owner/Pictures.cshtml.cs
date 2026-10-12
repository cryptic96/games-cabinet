using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;

namespace Cabinet.Service.Pages.Owner;

/// <summary>One game on the pictures page, with the picture the owner chose and which choices have something behind them.</summary>
/// <param name="GameId">The game.</param>
/// <param name="Title">The title to show.</param>
/// <param name="IsExpansion">Whether the game is an expansion.</param>
/// <param name="Choice">The kind of picture the owner chose; Automatic when the owner chose none.</param>
/// <param name="HasOwnedEdition">Whether BGG offers a picture for the owned edition.</param>
/// <param name="HasMainPicture">Whether BGG offers a main picture for the game.</param>
/// <param name="HasLink">Whether the owner has a picture link stored for the game.</param>
/// <param name="CoverUrl">The path of the small stored picture the cabinet draws now, or null when it draws none.</param>
/// <param name="CoverWidth">The width of that picture in pixels, or zero without one.</param>
/// <param name="CoverHeight">The height of that picture in pixels, or zero without one.</param>
public sealed record PictureGameRow(
    int GameId,
    string Title,
    bool IsExpansion,
    PictureChoice Choice,
    bool HasOwnedEdition,
    bool HasMainPicture,
    bool HasLink,
    string? CoverUrl,
    int CoverWidth,
    int CoverHeight)
{
    /// <summary>Whether a choice has a picture behind it, so its radio can be picked.</summary>
    /// <param name="choice">The kind of picture.</param>
    public bool Offers(PictureChoice choice) => choice switch
    {
        PictureChoice.OwnedEdition => HasOwnedEdition,
        PictureChoice.MainPicture => HasMainPicture,
        PictureChoice.Link => HasLink,
        _ => true,
    };
}

/// <summary>
/// The owner's pictures page: every synced game with the four pictures it can be drawn from. Every post goes through the single
/// owner-data writer and is answered by <see cref="OwnerResponses"/>, so the page works without scripts.
/// </summary>
public class PicturesModel : OwnerPage
{
    private const string PagePath = "/Owner/Pictures";
    private const string NoticeAnchor = "owner-notice";

    private readonly OwnerDataService _owner;
    private readonly CollectionStore _collection;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data.</param>
    /// <param name="collection">The collection visitors see, which lists the games.</param>
    public PicturesModel(OwnerDataService owner, CollectionStore collection)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(collection);

        _owner = owner;
        _collection = collection;
    }

    /// <inheritdoc />
    public override string PageName => OwnerStrings.Pictures;

    /// <summary>Whether the owner data could be read; the page offers no form while it cannot.</summary>
    public bool Readable { get; private set; }

    /// <summary>The revision the forms were built from.</summary>
    public long Revision { get; private set; }

    /// <summary>Every owned game once, by title.</summary>
    public IReadOnlyList<PictureGameRow> Games { get; private set; } = [];

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

    /// <summary>Sets which picture one game is drawn from.</summary>
    /// <param name="revision">The revision the form was built from.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="choice">The kind of picture, written as its name; anything else is refused.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostChooseAsync(long revision, int gameId, string? choice, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var kind = ParseChoice(choice);

        var result = await _owner.MutateAsync(
            revision,
            data => kind is { } known ? ChooseInSnapshot(data, snapshot, gameId, known) : OwnerChange.Refused(data, "no-picture"),
            cancellationToken);

        return OwnerResponses.For(this, result, PagePath, RowAnchor(gameId));
    }

    /// <summary>Takes back the latest change, when nothing was written after it.</summary>
    /// <param name="revision">The revision the change produced, which the Undo form carries.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostUndoAsync(long revision, CancellationToken cancellationToken)
    {
        var result = await _owner.UndoAsync(revision, cancellationToken);

        return OwnerResponses.For(this, result, PagePath, NoticeAnchor);
    }

    private static PictureChoice? ParseChoice(string? text) =>
        text is not null && Enum.GetNames<PictureChoice>().Contains(text, StringComparer.Ordinal)
            ? Enum.Parse<PictureChoice>(text)
            : null;

    private static OwnerChange ChooseInSnapshot(OwnerData data, CollectionSnapshot? snapshot, int gameId, PictureChoice choice)
    {
        var item = snapshot?.Items.FirstOrDefault(candidate => candidate.GameId == gameId);

        if (snapshot is null || item is null)
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        return PictureEdits.Choose(data, gameId, choice, TitleOf(item), item.VersionImageUrl, SnapshotMapper.MainPictureUrl(item, snapshot));
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

        if (_collection.Current.Snapshot is not { } snapshot)
        {
            return;
        }

        var covers = new Dictionary<int, CabinetItem>();

        foreach (var cabinetItem in _collection.Current.Items)
        {
            covers.TryAdd(cabinetItem.BggId, cabinetItem);
        }

        Games =
        [
            .. snapshot.Items
                .GroupBy(item => item.GameId)
                .Select(group => group.First())
                .Select(item => RowOf(item, snapshot, ready.Data, covers.GetValueOrDefault(item.GameId)))
                .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.GameId),
        ];
    }

    private static PictureGameRow RowOf(SnapshotItem item, CollectionSnapshot snapshot, OwnerData data, CabinetItem? shown)
    {
        var setting = data.Games.FirstOrDefault(game => game.GameId == item.GameId)?.Picture;
        var variant = LocationsModel.CoverOf(shown);

        return new PictureGameRow(
            item.GameId,
            TitleOf(item),
            item.Kind == ItemKind.Expansion,
            setting?.Choice ?? PictureChoice.Automatic,
            item.VersionImageUrl is not null,
            SnapshotMapper.MainPictureUrl(item, snapshot) is not null,
            setting?.Link is not null,
            variant?.Url,
            variant?.Width ?? 0,
            variant?.Height ?? 0);
    }
}
