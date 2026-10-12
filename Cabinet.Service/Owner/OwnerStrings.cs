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

    /// <summary>The accessible name of the picker of one game.</summary>
    /// <param name="title">The game's title.</param>
    public static string PickerLabel(string title) => $"Location for {title}";

    /// <summary>The picker option that makes an expansion follow its base game.</summary>
    /// <param name="baseTitle">The base game's title.</param>
    public static string SameAs(string baseTitle) => $"Same as {baseTitle}";

    /// <summary>The words for a status key, or null when the key is not one the owner tools use.</summary>
    /// <param name="statusKey">The key an owner result carries.</param>
    public static string? Message(string? statusKey) => statusKey switch
    {
        "location-added" => "Location added.",
        "location-renamed" => "Location renamed.",
        "location-moved" => "Location moved.",
        "location-deleted" => "Location deleted.",
        "game-moved" or "game-unplaced" => "Saved.",
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
        "id-clash" or "save-failed" => "Couldn't save that. Try again.",
        _ => null,
    };
}
