using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;

namespace Cabinet.Service.Owner;

/// <summary>The English text of the owner pages' shell. The owner tools are written in English only.</summary>
public static class OwnerStrings
{
    /// <summary>The plate in the header that marks every page served at the owner address.</summary>
    public const string Plate = "Owner tools";

    /// <summary>The accessible label of the owner tools bar.</summary>
    public const string BarLabel = "Owner tools";

    /// <summary>The pictures page's name, as a bar item, heading and title part.</summary>
    public const string Pictures = "Pictures";

    /// <summary>The locations page's name, as a bar item, heading and title part.</summary>
    public const string Locations = "Locations";

    /// <summary>The settings page's name, as a bar item, heading and title part.</summary>
    public const string Settings = "Settings";

    /// <summary>The bar item that goes back to the cabinet.</summary>
    public const string Cabinet = "Cabinet";

    /// <summary>The skip link's text.</summary>
    public const string SkipLink = "Skip to the page content";

    /// <summary>The bare answer to a client outside the allowed ranges.</summary>
    public const string NotAvailable = "Not available from this address.";

    /// <summary>The line under the locations page's heading.</summary>
    public const string LocationsIntro = "Say where each game is stored.";

    /// <summary>The line under the pictures page's heading.</summary>
    public const string PicturesIntro = "Choose the picture the cabinet and the game card use for each game.";

    /// <summary>The button that saves the picture chosen for one game.</summary>
    public const string SavePictureButton = "Save picture";

    /// <summary>The picture choice that leaves the pick to the cabinet.</summary>
    public const string ChoiceAutomatic = "Automatic";

    /// <summary>The picture choice that draws the picture of the owned edition.</summary>
    public const string ChoiceOwnedEdition = "Owned edition";

    /// <summary>The picture choice that draws the game's main picture.</summary>
    public const string ChoiceMainPicture = "Main picture";

    /// <summary>The picture choice that draws the picture at the owner's own link.</summary>
    public const string ChoiceYourLink = "Your link";

    /// <summary>The heading shown in place of the pictures list while no game has been synced.</summary>
    public const string PicturesNoGamesHeading = "No games yet.";

    /// <summary>The line under <see cref="PicturesNoGamesHeading"/>.</summary>
    public const string PicturesNoGamesBody = "Your games show up here after the first sync.";

    /// <summary>The line on a tile whose candidate picture BGG does not have.</summary>
    public const string TileBggHasNone = "BGG has none";

    /// <summary>The line on the Your link tile while the owner has stored no link.</summary>
    public const string TileNoLinkYet = "None yet";

    /// <summary>The text on the Your link tile while the stored link's picture has not arrived.</summary>
    public const string TileFetching = "Fetching...";

    /// <summary>The tag on the tile whose picture the Automatic tile shows.</summary>
    public const string AutomaticsPick = "Automatic's pick";

    /// <summary>The tag a picture gets when the detector cannot tell what shape it has, and the one an unknown kind falls back to.</summary>
    public const string VerdictUnsure = "Shape unsure";

    /// <summary>The label of the tile of one picture choice.</summary>
    /// <param name="choice">The choice the tile stands for.</param>
    public static string TileLabel(PictureChoice choice) => choice switch
    {
        PictureChoice.OwnedEdition => ChoiceOwnedEdition,
        PictureChoice.MainPicture => ChoiceMainPicture,
        PictureChoice.Link => ChoiceYourLink,
        _ => ChoiceAutomatic,
    };

    /// <summary>The words that tell the owner what a picture looks like to the detector.</summary>
    /// <param name="kind">The kind of verdict.</param>
    public static string VerdictTag(ArtVerdictKind kind) => kind switch
    {
        ArtVerdictKind.Flat => "Reads as a flat cover",
        ArtVerdictKind.ThreeD => "Reads as a 3D shot",
        ArtVerdictKind.CutOut => "Reads as a cut-out",
        ArtVerdictKind.TightCrop => "Reads as a tight crop",
        _ => VerdictUnsure,
    };

    /// <summary>The line on the Automatic tile that names the tile it duplicates.</summary>
    /// <param name="source">Which picture Automatic draws.</param>
    public static string AutomaticSubLine(AutomaticSource source) => source switch
    {
        AutomaticSource.OwnedEdition => $"Same as {ChoiceOwnedEdition}",
        AutomaticSource.MainPicture => $"Same as {ChoiceMainPicture}",
        _ => "Uses a generated cover",
    };

    /// <summary>The accessible name of the two links that switch which games the pictures page shows.</summary>
    public const string GamesShownLabel = "Games shown";

    /// <summary>The button that records that a game's pictures look right.</summary>
    public const string LooksRightButton = "Looks right";

    /// <summary>The mark of a game whose choice is the owner's own link.</summary>
    public const string MarkYours = "Yours";

    /// <summary>The mark, and the button text, of a game the owner dealt with.</summary>
    public const string MarkChecked = "Checked";

    /// <summary>The mark of a game whose linked picture no longer works.</summary>
    public const string MarkLinkDead = "Link no longer works";

    /// <summary>The mark of a game whose picture BGG replaced since the owner looked.</summary>
    public const string MarkChanged = "Picture changed on BGG";

    /// <summary>The mark of a game that has no picture at all.</summary>
    public const string MarkNoPicture = "No picture";

    /// <summary>The mark of a game whose stored picture cannot be shown.</summary>
    public const string MarkWontLoad = "Picture won't load";

    /// <summary>The mark of a game whose owned edition has a picture that Automatic does not draw.</summary>
    public const string MarkSkipsOwned = "Automatic skips the owned edition";

    /// <summary>The mark of a game whose linked picture has not been fetched yet.</summary>
    public const string MarkFetching = "Fetching your link...";

    /// <summary>The note under the marks of a game whose linked picture no longer works.</summary>
    public const string NoteLinkDead =
        "Your link no longer works. The cabinet keeps showing the copy it saved. Choose another picture, paste a new link, remove the link, or press \"Looks right\" to keep this copy.";

    /// <summary>The note under the marks of a game that has no usable picture.</summary>
    public const string NoteMissing = "BGG has no usable picture for this game, so the cabinet shows a generated cover.";

    /// <summary>The note under the marks of a game whose picture cannot be loaded.</summary>
    public const string NoteWontLoad = "The picture can't be loaded right now. The cabinet shows a generated cover until it can.";

    /// <summary>The heading shown in place of the worklist when nothing is left to check.</summary>
    public const string NothingToCheckHeading = "Nothing to check.";

    /// <summary>The line under <see cref="NothingToCheckHeading"/>.</summary>
    public const string NothingToCheckBody = "You've checked every game that needed a look. Use \"All games\" to change any picture.";

    /// <summary>The counter's words when no game is worth a look.</summary>
    public const string NothingNeedsLook = "Nothing needs a look right now.";

    /// <summary>The note under the marks of a game whose picture BGG replaced.</summary>
    /// <param name="picture">Which picture BGG replaced.</param>
    public static string NoteChanged(ChangedPicture picture) =>
        $"BGG replaced the {(picture == ChangedPicture.OwnedEdition ? "owned edition" : "main")} picture. Check that it still looks right.";

    /// <summary>The line above the one game the single-game view shows.</summary>
    public const string ShowingOneGame = "Showing one game.";

    /// <summary>The link from the single-game view back to the list of every game.</summary>
    public const string ShowAllGames = "Show all games";

    /// <summary>The visible label of the location picker in the single-game view.</summary>
    public const string LocationLabel = "Location";

    /// <summary>The note in the single-game view while there is no location to choose.</summary>
    public const string AddLocationOnLocationsPage = "Add a location on the Locations page first.";

    /// <summary>The link that goes with <see cref="AddLocationOnLocationsPage"/>.</summary>
    public const string LocationsLink = "Locations";

    /// <summary>The link that switches the pictures page to the games worth a look.</summary>
    /// <param name="count">How many games are left to check.</param>
    public static string WorthALookLink(int count) => $"Worth a look ({count})";

    /// <summary>The link that switches the pictures page to every game.</summary>
    /// <param name="count">How many games there are.</param>
    public static string AllGamesLink(int count) => $"All games ({count})";

    /// <summary>The counter above the games: how many are left to check, or why there is nothing to count.</summary>
    /// <param name="counts">What the worklist holds.</param>
    public static string CounterText(PictureCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (counts.Candidates == 0)
        {
            return NothingNeedsLook;
        }

        if (counts.ToCheck > 0)
        {
            return $"{counts.ToCheck} of {counts.Candidates} to check";
        }

        return counts.Candidates == 1 ? "You've checked 1 game." : $"You've checked all {counts.Candidates} games.";
    }

    /// <summary>The sentence under a game's choices that says why Automatic drew what it did.</summary>
    /// <param name="why">The reason.</param>
    /// <param name="ownedReadsAs">What the owned edition's picture looks like, when that is the reason.</param>
    public static string WhyLine(AutomaticWhy why, ArtVerdictKind? ownedReadsAs) => why switch
    {
        AutomaticWhy.OwnedEdition => "Automatic picks the owned edition.",
        AutomaticWhy.MainBecauseOwnedReads => $"Automatic picks the main picture because the owned edition reads as {ReadsAs(ownedReadsAs)}.",
        AutomaticWhy.MainBecauseNoOwned => "Automatic picks the main picture because BGG has no owned edition picture.",
        _ => "Automatic has no picture to use, so the cabinet shows a generated cover.",
    };

    private static string ReadsAs(ArtVerdictKind? kind) => kind switch
    {
        ArtVerdictKind.ThreeD => "a 3D shot",
        ArtVerdictKind.CutOut => "a cut-out",
        ArtVerdictKind.TightCrop => "a tight crop",
        _ => "an unsure shape",
    };

    /// <summary>The heading of the games panel on the locations page.</summary>
    public const string GamesHeading = "Games";

    /// <summary>The label of the field that names a new location.</summary>
    public const string NewLocationLabel = "New location";

    /// <summary>The hint under the field that names a new location.</summary>
    public const string NewLocationHint = "Shown to every visitor. Avoid people's names.";

    /// <summary>The button that adds a location.</summary>
    public const string AddLocationButton = "Add location";

    /// <summary>The button that saves where one game is kept.</summary>
    public const string SaveButton = "Save";

    /// <summary>The button that takes back the latest change.</summary>
    public const string UndoButton = "Undo";

    /// <summary>The picker option that keeps a game without a location of its own.</summary>
    public const string NotPlaced = "Not placed";

    /// <summary>The note shown while the list of locations is empty.</summary>
    public const string NoLocationsYet = "No locations yet. Add one to start placing games.";

    /// <summary>The note shown above the games while there is no location to place them in.</summary>
    public const string AddLocationFirst = "Add a location above before placing games.";

    /// <summary>The note shown while no game has been synced.</summary>
    public const string NoGamesYet = "No games yet. Your games show up here after the first sync.";

    /// <summary>The note shown while the owner data cannot be read.</summary>
    public const string OwnerDataUnreadable = "Your owner data couldn't be read, so nothing can be saved until it is restored.";

    /// <summary>The line at the top of the list of locations.</summary>
    public const string LocationsHelper = "Locations show in this order. Use the arrows to change it.";

    /// <summary>The visible text of the button that moves a location up the list.</summary>
    public const string UpButton = "Up";

    /// <summary>The visible text of the button that moves a location down the list.</summary>
    public const string DownButton = "Down";

    /// <summary>The visible text of the button that deletes a location.</summary>
    public const string DeleteButton = "Delete";

    /// <summary>The button that confirms deleting a location that holds games.</summary>
    public const string DeleteLocationButton = "Delete location";

    /// <summary>The button that keeps a location that was about to be deleted.</summary>
    public const string KeepItButton = "Keep it";

    /// <summary>The count of games for a location that holds none.</summary>
    public const string NoGames = "No games";

    /// <summary>The document title of an owner page.</summary>
    /// <param name="page">The page's name.</param>
    public static string PageTitle(string page) => $"{page} - {Plate}";

    /// <summary>The heading of the panel that lists the locations.</summary>
    /// <param name="count">How many locations there are.</param>
    public static string LocationsHeading(int count) => count == 0 ? "Your locations" : $"Your locations ({count})";

    /// <summary>How many games a location holds.</summary>
    /// <param name="count">The number of games.</param>
    public static string GameCount(int count) => count switch
    {
        0 => NoGames,
        1 => "1 game",
        _ => $"{count} games",
    };

    /// <summary>The accessible name of the name field of one location.</summary>
    /// <param name="position">The location's place in the list, counting from one.</param>
    public static string NameOfLocation(int position) => $"Name of location {position}";

    /// <summary>The accessible name of the button that moves a location up.</summary>
    /// <param name="name">The location's name.</param>
    public static string MoveUpLabel(string name) => $"Move {name} up";

    /// <summary>The accessible name of the button that moves a location down.</summary>
    /// <param name="name">The location's name.</param>
    public static string MoveDownLabel(string name) => $"Move {name} down";

    /// <summary>The accessible name of the button that deletes a location.</summary>
    /// <param name="name">The location's name.</param>
    public static string DeleteLabel(string name) => $"Delete {name}";

    /// <summary>The title of the question that asks before a location that holds games is deleted.</summary>
    /// <param name="name">The location's name.</param>
    public static string DeleteTitle(string name) => $"Delete \"{name}\"?";

    /// <summary>The first sentence of the warning above the button that deletes a location that holds games.</summary>
    /// <param name="games">How many games would become not placed.</param>
    public static string DeleteLead(int games) =>
        games == 1 ? "1 game will become not placed." : $"{games} games will become not placed.";

    /// <summary>The second sentence of the warning above the button that deletes a location that holds games.</summary>
    /// <param name="games">How many games would become not placed.</param>
    public static string DeleteRest(int games) =>
        games == 1 ? "It isn't moved anywhere else." : "They aren't moved anywhere else.";

    /// <summary>Whether a status key is one of the reasons a location name is refused.</summary>
    /// <param name="statusKey">The key an owner result carries.</param>
    public static bool IsNameProblem(string? statusKey) =>
        statusKey is "name-empty" or "name-too-long" or "name-taken" or "too-many-locations" or "name-characters";

    /// <summary>The name of the group of picture choices of one game.</summary>
    /// <param name="title">The game's title.</param>
    public static string PictureLegend(string title) => $"Picture for {title}";

    /// <summary>The accessible name of the picker of one game.</summary>
    /// <param name="title">The game's title.</param>
    public static string PickerLabel(string title) => $"Location for {title}";

    /// <summary>The picker option that makes an expansion follow its base game.</summary>
    /// <param name="baseTitle">The base game's title.</param>
    public static string SameAs(string baseTitle) => $"Same as {baseTitle}";

    /// <summary>The accessible name of the checkbox that ticks one game.</summary>
    /// <param name="title">The game's title.</param>
    public static string SelectLabel(string title) => $"Select {title}";

    /// <summary>The label of the search box above the games.</summary>
    public const string FindLabel = "Find a game";

    /// <summary>The label of the checkbox that ticks every game that is shown.</summary>
    public const string SelectAllShown = "Select all shown";

    /// <summary>The label of the switch that shows only the games that are not placed, with how many there are.</summary>
    /// <param name="count">How many games are not placed.</param>
    public static string UnplacedLabel(int count) => $"Not placed yet ({count})";

    /// <summary>The tag on an expansion's row.</summary>
    public const string ExpansionTag = "Expansion";

    /// <summary>The tag on the row of an expansion whose base game is not owned.</summary>
    public const string OrphanExpansionTag = "Expansion (base not owned)";

    /// <summary>The accessible name of the dock that appears while games are ticked.</summary>
    public const string SelectionLabel = "Selection";

    /// <summary>The dock button that opens the list of places to move the ticked games to.</summary>
    public const string MoveToButton = "Move to...";

    /// <summary>The dock button that unticks every game.</summary>
    public const string ClearButton = "Clear";

    /// <summary>The summary of the disclosure that holds the form for a picture from a link.</summary>
    public const string LinkSummary = "Use a picture from a link";

    /// <summary>The label of the field the owner pastes a picture address into.</summary>
    public const string LinkLabel = "Picture link";

    /// <summary>The hint under the field the owner pastes a picture address into.</summary>
    public const string LinkHint = "A direct picture link from BGG's gallery. Open the picture there, choose \"Copy image address\", and paste it here.";

    /// <summary>The button that fetches the picture at the pasted address and shows a preview.</summary>
    public const string CheckLinkButton = "Check link";

    /// <summary>The button that stores the previewed picture and chooses it.</summary>
    public const string UseThisButton = "Use this";

    /// <summary>The button that throws the previewed picture away.</summary>
    public const string DiscardPreviewButton = "Discard preview";

    /// <summary>The heading of the preview of a linked picture.</summary>
    public const string PreviewHeading = "Preview";

    /// <summary>The line under <see cref="PreviewHeading"/>.</summary>
    public const string PreviewSub = "How this picture will look in the cabinet.";

    /// <summary>The caption of the face-out box in the preview.</summary>
    public const string BoxFrontCaption = "Box front";

    /// <summary>The caption of the spine in the preview.</summary>
    public const string SpineCaption = "Spine";

    /// <summary>The line in the preview that says the site cannot tell whether the picture belongs to the game.</summary>
    public const string PreviewDisclaimer = "The site can't check that this picture belongs to this game.";

    /// <summary>The accessible name of the face-out box in the preview.</summary>
    /// <param name="title">The game's title.</param>
    public static string PreviewBoxLabel(string title) => $"Box front of {title}";

    /// <summary>The accessible name of the spine in the preview.</summary>
    /// <param name="title">The game's title.</param>
    public static string PreviewSpineLabel(string title) => $"Spine of {title}";

    /// <summary>Says where the size of the box in a preview came from.</summary>
    /// <param name="source">Where it came from.</param>
    public static string ShapeText(PreviewShapeSource source) => source switch
    {
        PreviewShapeSource.Bgg => "Box shape: from BGG's size",
        PreviewShapeSource.Picture => "Box shape: from this picture",
        _ => "Box shape: estimated",
    };

    /// <summary>The note under a preview whose picture reads as something other than a flat cover, or null when there is none to give.</summary>
    /// <param name="kind">What the picture reads as.</param>
    public static string? PreviewNote(ArtVerdictKind kind) => kind switch
    {
        ArtVerdictKind.ThreeD or ArtVerdictKind.CutOut or ArtVerdictKind.TightCrop =>
            $"This picture reads as {ReadsAs(kind)}. You can still use it. The box keeps its shape from BGG's size or an estimate.",
        _ => null,
    };

    /// <summary>The words for a status key, or null when the key is not one the owner tools use.</summary>
    /// <param name="statusKey">The key an owner result carries.</param>
    public static string? Message(string? statusKey) => statusKey switch
    {
        "location-added" => "Location added.",
        "location-renamed" => "Location renamed.",
        "location-moved" => "Location moved.",
        "location-deleted" => "Location deleted.",
        "game-moved" or "game-unplaced" or "picture-chosen" => "Saved.",
        "picture-checked" => "Marked as checked.",
        "unchanged" => "Nothing to change.",
        "undone" => "Undone.",
        "out-of-date" => "This page was out of date, so nothing was saved. It has been reloaded; try again.",
        "cant-undo" => "That change can't be undone any more.",
        "unreadable" => OwnerDataUnreadable,
        "name-empty" => "Give the location a name.",
        "name-too-long" => "Use 60 characters or fewer.",
        "name-taken" => "You already have a location with that name.",
        "too-many-locations" => "You can have up to 50 locations.",
        "name-characters" => "Use letters, numbers and ordinary punctuation only.",
        "unknown-location" => "That location isn't in your list.",
        "unknown-game" => "That game isn't in your collection any more.",
        "no-picture" => "That picture isn't available for this game.",
        "no-link" => "This game has no link saved yet.",
        "link-failed" => "Couldn't use that link. Check it and try again.",
        "link-expired" => "This preview expired. Check the link again.",
        "nothing-selected" => "Tick at least one game first.",
        "id-clash" or "save-failed" => "Couldn't save that. Try again.",
        _ => null,
    };
}
