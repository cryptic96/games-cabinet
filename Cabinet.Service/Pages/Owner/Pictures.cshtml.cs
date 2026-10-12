using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc;

namespace Cabinet.Service.Pages.Owner;

/// <summary>
/// The owner's pictures page: every synced game with the four pictures it can be drawn from, shown as they would look. Every
/// post goes through the single owner-data writer and is answered by <see cref="OwnerResponses"/>, so the page works without
/// scripts.
/// </summary>
public class PicturesModel : OwnerPage
{
    private const string PagePath = "/Owner/Pictures";
    private const string NoticeAnchor = "owner-notice";

    private readonly OwnerDataService _owner;
    private readonly CollectionStore _collection;
    private readonly ArtRules _rules;

    /// <summary>Creates the page.</summary>
    /// <param name="owner">The single writer of the owner data.</param>
    /// <param name="collection">The collection visitors see, which lists the games.</param>
    /// <param name="rules">The rules that decide what the automatic pick draws.</param>
    public PicturesModel(OwnerDataService owner, CollectionStore collection, ArtRules rules)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rules);

        _owner = owner;
        _collection = collection;
        _rules = rules;
    }

    /// <inheritdoc />
    public override string PageName => OwnerStrings.Pictures;

    /// <summary>Whether the owner data could be read; the page offers no form while it cannot.</summary>
    public bool Readable { get; private set; }

    /// <summary>The revision the forms were built from.</summary>
    public long Revision { get; private set; }

    /// <summary>Every owned game once, by title.</summary>
    public IReadOnlyList<PictureRow> Games { get; private set; } = [];

    /// <summary>The words for the outcome of the post that sent the browser here, or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether <see cref="Notice"/> reports a problem.</summary>
    public bool NoticeIsProblem { get; private set; }

    /// <summary>The revision of the change the Undo form would take back, or null when there is none to offer.</summary>
    public long? UndoRevision { get; private set; }

    /// <summary>The name the radios of one game share, which is also the field a form posted without script carries.</summary>
    /// <param name="gameId">The game.</param>
    public static string GroupName(int gameId) => $"pic-{gameId}";

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
    /// <param name="choice">The kind of picture, written as its name; anything else is refused. A form posted without script names the field after the game instead.</param>
    /// <param name="cancellationToken">Cancels the wait for the writer.</param>
    public async Task<IActionResult> OnPostChooseAsync(long revision, int gameId, string? choice, CancellationToken cancellationToken)
    {
        var snapshot = _collection.Current.Snapshot;
        var kind = ParseChoice(choice ?? (Request.HasFormContentType ? Request.Form[GroupName(gameId)].FirstOrDefault() : null));

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

        return PictureEdits.Choose(data, gameId, choice, PictureRows.TitleOf(item), item.VersionImageUrl, SnapshotMapper.MainPictureUrl(item, snapshot));
    }

    private static string RowAnchor(int gameId) => $"game-{gameId}";

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

        Games = PictureRows.Build(snapshot, _rules, ready.Data);
    }
}
