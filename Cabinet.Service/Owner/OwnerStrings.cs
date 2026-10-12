using System.Globalization;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Service.Pages;

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

    /// <summary>The button that removes the picture link of a game and the copy stored for it.</summary>
    public const string RemoveLinkButton = "Remove link";

    /// <summary>The status after a link was removed.</summary>
    public const string LinkRemoved = "Link removed";

    /// <summary>What stands in for the picture host's name where a message is made without knowing it.</summary>
    public const string AnyPictureHost = "the picture host";

    private const string LinkKeyPrefix = "link-";

    private static readonly Dictionary<LinkProblem, string> LinkKeys = new()
    {
        [LinkProblem.Blank] = "link-blank",
        [LinkProblem.NotAWebAddress] = "link-address",
        [LinkProblem.PageLink] = "link-page",
        [LinkProblem.OtherHost] = "link-host",
        [LinkProblem.FetchFailed] = "link-failed",
        [LinkProblem.NotFound] = "link-not-found",
        [LinkProblem.NotAPicture] = "link-not-picture",
        [LinkProblem.TooLarge] = "link-too-large",
        [LinkProblem.TooManyPixels] = "link-pixels",
        [LinkProblem.ExtremeShape] = "link-shape",
    };

    /// <summary>The words in front of the short address of the stored link, under the field.</summary>
    public const string CurrentLinkLabel = "Current link:";

    /// <summary>The refusal key a redirect or an answer carries for a problem with an address.</summary>
    /// <param name="problem">The problem.</param>
    public static string LinkKey(LinkProblem problem) => LinkKeys[problem];

    /// <summary>The problem a refusal key stands for, or null when the key is not the key of a problem with an address.</summary>
    /// <param name="key">The key.</param>
    public static LinkProblem? LinkProblemOf(string? key) =>
        key is not null && key.StartsWith(LinkKeyPrefix, StringComparison.Ordinal)
            ? LinkKeys.Where(pair => pair.Value == key).Select(pair => (LinkProblem?)pair.Key).FirstOrDefault()
            : null;

    /// <summary>Millions of bytes with at most one decimal, as the owner reads a picture's size.</summary>
    /// <param name="bytes">The size in bytes.</param>
    public static string Megabytes(long bytes) =>
        (bytes / 1_000_000d).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The words for one problem with a pasted address: what went wrong in a lead sentence, then the next step. Only the picture
    /// host's name and sizes are put in, never anything the owner pasted.
    /// </summary>
    /// <param name="problem">The problem.</param>
    /// <param name="host">The name of the picture host a link has to point at.</param>
    /// <param name="bytes">For a picture that is too large, the size the host announced; otherwise null.</param>
    /// <param name="limitBytes">For a picture that is too large, the most bytes a picture may have; otherwise null.</param>
    public static string LinkError(LinkProblem problem, string host, long? bytes, long? limitBytes) => problem switch
    {
        LinkProblem.Blank => "Paste a picture link first.",
        LinkProblem.NotAWebAddress => "That isn't a web address. Paste the full picture link, starting with https://.",
        LinkProblem.PageLink =>
            "That's a page link, not a picture link. In BGG's gallery, open the picture and choose \"Copy image address\", then paste that here.",
        LinkProblem.OtherHost =>
            $"That link isn't from BGG's picture host. Only links from {host} work here. Copy the picture's address from BGG's gallery.",
        LinkProblem.NotFound => "That picture isn't there. Check that you copied the whole link.",
        LinkProblem.NotAPicture => "That isn't a picture the cabinet can read. Try another one.",
        LinkProblem.TooLarge => $"That picture is too large to use{TooLargeSizes(bytes, limitBytes)}. Pick a smaller version from the gallery.",
        LinkProblem.TooManyPixels => "That picture has too many pixels to process. Pick a smaller version from the gallery.",
        LinkProblem.ExtremeShape => "That picture is very long and thin, so it can't be a box front. Try another one.",
        _ => "Couldn't fetch that picture. The link may be wrong or the picture host may be busy. Check the link and try again.",
    };

    private static string TooLargeSizes(long? bytes, long? limitBytes) => (bytes, limitBytes) switch
    {
        ({ } size, { } limit) => $" ({Megabytes(size)} MB; the limit is {Megabytes(limit)} MB)",
        (null, { } limit) => $" (the limit is {Megabytes(limit)} MB)",
        _ => string.Empty,
    };

    /// <summary>The heading of the backup panel.</summary>
    public const string BackupHeading = "Backup";

    /// <summary>The line under the backup panel's heading.</summary>
    public const string BackupHelper =
        "Your pictures and locations live on this server only. Download them now and then, so you can restore them if the server is lost.";

    /// <summary>The button that downloads the owner data; it is also the accessible name of the compact button on a phone.</summary>
    public const string DownloadButton = "Download owner data";

    /// <summary>The visible text of the compact download button below the phone breakpoint.</summary>
    public const string DownloadButtonShort = "Download data";

    /// <summary>The text of the download button while the file is being prepared.</summary>
    public const string Preparing = "Preparing...";

    /// <summary>The line of the backup panel while the owner has entered nothing yet.</summary>
    public const string NothingToBackUp = "Nothing to back up yet.";

    /// <summary>The words shown when the download could not be prepared.</summary>
    public const string DownloadFailed = "Couldn't prepare the download. Try again.";

    /// <summary>The start of the quiet line that names when the last download was made; the time follows it.</summary>
    public const string DownloadedPrefix = "Downloaded";

    /// <summary>The heading of the change log panel.</summary>
    public const string RecentChangesHeading = "Recent changes";

    /// <summary>The line under the change log's heading.</summary>
    public const string RecentChangesHelper = "The last 20 changes.";

    /// <summary>The line of the change log while it has no entry.</summary>
    public const string NoChangesYet = "No changes yet. Edits you make show up here.";

    /// <summary>The reminder when changes were made since the last download.</summary>
    /// <param name="count">How many changes, at least one.</param>
    public static string ChangesSinceDownload(int count) =>
        count == 1 ? "1 change since your last download" : $"{count.ToString(CultureInfo.InvariantCulture)} changes since your last download";

    /// <summary>The one-line reminder shown on a phone.</summary>
    /// <param name="count">How many changes, at least one.</param>
    public static string ChangesNotDownloaded(int count) =>
        count == 1 ? "1 change not downloaded" : $"{count.ToString(CultureInfo.InvariantCulture)} changes not downloaded";

    /// <summary>The reminder when the owner has never downloaded anything and has changes.</summary>
    /// <param name="count">How many changes, at least one.</param>
    public static string NeverDownloaded(int count) =>
        "You haven't downloaded your owner data yet. " + (count == 1
            ? "1 change is only on this server."
            : $"{count.ToString(CultureInfo.InvariantCulture)} changes are only on this server.");

    /// <summary>The note under the backup panel about the earlier versions the server keeps.</summary>
    /// <param name="kept">How many versions the server keeps.</param>
    public static string BackupFootnote(int kept) =>
        $"The site keeps the last {kept.ToString(CultureInfo.InvariantCulture)} versions of your owner data on the server.";

    /// <summary>The value of a <c>time</c> element's <c>datetime</c> attribute for a moment: UTC to the second.</summary>
    /// <param name="at">The moment.</param>
    public static string TimeAttribute(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// The readable text a <c>time</c> element holds until a script rewrites it in the viewer's own clock: the moment in UTC,
    /// the way the status line of the cabinet writes it.
    /// </summary>
    /// <param name="at">The moment.</param>
    public static string TimeUtcText(DateTimeOffset at) => SyncStatusText.ExactUtc(at);

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
        "link-removed" => $"{LinkRemoved}.",
        "link-expired" => "This preview expired. Check the link again.",
        "nothing-selected" => "Tick at least one game first.",
        "id-clash" or "save-failed" => "Couldn't save that. Try again.",
        _ when LinkProblemOf(statusKey) is { } problem => LinkError(problem, AnyPictureHost, null, null),
        _ => null,
    };
}
