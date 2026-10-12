using Cabinet.Domain.Owner;
using Cabinet.Service.Owner;

namespace Cabinet.Service.Pages.Owner;

/// <summary>One choice of a game's location picker.</summary>
/// <param name="Value">The value posted: a location's identifier, or empty for none of its own.</param>
/// <param name="Text">The words the owner reads.</param>
/// <param name="Selected">Whether the picker starts on this choice.</param>
public sealed record LocationPickerOption(string Value, string Text, bool Selected);

/// <summary>
/// What the shared location picker needs to draw one game's select: the choices in order, the one that is chosen, and the revision
/// the form was built from. The same partial serves every owner page that places a single game, and it always posts to the
/// locations page's assign handler.
/// </summary>
/// <param name="GameId">The game the picker places.</param>
/// <param name="Title">The game's title, which names the select for assistive technology.</param>
/// <param name="Options">The choices: first the empty one, then the locations in the owner's order.</param>
/// <param name="Revision">The revision of the owner data the form was built from.</param>
/// <param name="VisibleLabel">A visible label to draw above the select, or null when the surrounding row already names the game.</param>
/// <param name="ReturnTo">Which page a form posted without script goes back to, one of the fixed words the assign handler knows; null for the locations page.</param>
public sealed record LocationPickerModel(
    int GameId,
    string Title,
    IReadOnlyList<LocationPickerOption> Options,
    long Revision,
    string? VisibleLabel = null,
    string? ReturnTo = null)
{
    /// <summary>The return word that sends a form posted without script back to the single-game view of the pictures page.</summary>
    public const string ReturnToPictures = "pictures";

    /// <summary>
    /// Builds the picker of a game. The first choice is <c>Same as {base title}</c> for an expansion that stands beside an owned
    /// base game, because such an expansion has no not-placed state of its own, and <c>Not placed</c> for every other game; then
    /// come the locations in list order.
    /// </summary>
    /// <param name="game">The game's row.</param>
    /// <param name="locations">The owner's locations in list order.</param>
    /// <param name="revision">The revision of the owner data the form is built from.</param>
    /// <param name="visibleLabel">A visible label for the select, or null for none.</param>
    /// <param name="returnTo">Which page a form posted without script goes back to; null for the locations page.</param>
    public static LocationPickerModel For(
        LocationGameRow game,
        IReadOnlyList<OwnerLocation> locations,
        long revision,
        string? visibleLabel = null,
        string? returnTo = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(locations);

        var first = game.BaseTitle is null ? OwnerStrings.NotPlaced : OwnerStrings.SameAs(game.BaseTitle);
        var options = new List<LocationPickerOption> { new(string.Empty, first, game.OwnLocationId is null) };

        options.AddRange(locations.Select(location =>
            new LocationPickerOption(location.Id, location.Name, string.Equals(location.Id, game.OwnLocationId, StringComparison.Ordinal))));

        return new LocationPickerModel(game.GameId, game.Title, options, revision, visibleLabel, returnTo);
    }
}
