using System.Globalization;

namespace Cabinet.Service.Language;

/// <summary>
/// Every label the server writes into the page, in one language. The English and Dutch tables hold the same properties,
/// so a label that exists in one language and not the other cannot compile.
/// </summary>
public sealed record SiteText
{
    /// <summary>The players chip that stands for this many players or more.</summary>
    private const int MaxPlayersChip = 8;

    /// <summary>The English labels.</summary>
    public static SiteText English { get; } = new()
    {
        SiteName = "Games Cabinet",
        LanguageLabel = "Language",
        EnglishName = "EN, English",
        DutchName = "NL, Nederlands",
        SkipLink = "Skip to the list of games",
        GamesListHeading = "All games",
        CabinetLabel = "Cabinet",
        CabinetHint = "Use the arrow keys to move between games and press Enter to open one.",
        FillingHeading = "The cabinet is being filled.",
        FillingBody = "The games are being copied over from BoardGameGeek. Check back in a few minutes.",
        NoScript = "The cabinet needs JavaScript to be drawn. Please turn it on and reload.",
        VersionFormat = "Version {0} ({1})",
        LogoAlt = "Powered by BGG",
        SyncNow = "Sync now",
        LastSyncedFormat = "Last synced {0}",
        FilterBarLabel = "Find a game",
        SearchLabel = "Search games",
        ClearVisible = "Clear",
        ClearName = "Clear filters",
        PlayerSuffixOneFormat = "{0} player",
        PlayerSuffixManyFormat = "{0} players",
        PlayerSuffixMax = "8 or more players",
        TimeSuffixFormat = "{0} minutes or less",
        ViewLegend = "View",
        ViewAll = "All together",
        ViewByLocation = "By location",
        PlayersLegend = "Players",
        TimeLegend = "Max. minutes",
        AnyChoice = "Any",
        LocationLabel = "Location",
        AnyLocation = "Any location",
        UnknownLocation = "Unknown location",
        FiltersButton = "Filters",
        FiltersActiveSuffix = "active",
        SheetTitle = "Filters",
    };

    /// <summary>The Dutch labels, informal and in the singular you-form.</summary>
    public static SiteText Dutch { get; } = new()
    {
        SiteName = "Spellenkast",
        LanguageLabel = "Taal",
        EnglishName = "EN, English",
        DutchName = "NL, Nederlands",
        SkipLink = "Ga naar de lijst met spellen",
        GamesListHeading = "Alle spellen",
        CabinetLabel = "Kast",
        CabinetHint = "Gebruik de pijltoetsen om tussen spellen te bewegen en druk op Enter om er een te openen.",
        FillingHeading = "De kast wordt gevuld.",
        FillingBody = "De spellen worden overgenomen van BoardGameGeek. Kom over een paar minuten terug.",
        NoScript = "De kast heeft JavaScript nodig om getekend te worden. Zet het aan en laad de pagina opnieuw.",
        VersionFormat = "Versie {0} ({1})",
        LogoAlt = "Powered by BGG",
        SyncNow = "Nu synchroniseren",
        LastSyncedFormat = "Laatst gesynchroniseerd op {0}",
        FilterBarLabel = "Zoek een spel",
        SearchLabel = "Zoek een spel",
        ClearVisible = "Wissen",
        ClearName = "Filters wissen",
        PlayerSuffixOneFormat = "{0} speler",
        PlayerSuffixManyFormat = "{0} spelers",
        PlayerSuffixMax = "8 of meer spelers",
        TimeSuffixFormat = "{0} minuten of minder",
        ViewLegend = "Weergave",
        ViewAll = "Alles samen",
        ViewByLocation = "Per locatie",
        PlayersLegend = "Spelers",
        TimeLegend = "Max. minuten",
        AnyChoice = "Alle",
        LocationLabel = "Locatie",
        AnyLocation = "Alle locaties",
        UnknownLocation = "Onbekende locatie",
        FiltersButton = "Filters",
        FiltersActiveSuffix = "actief",
        SheetTitle = "Filters",
    };

    /// <summary>The page title and heading.</summary>
    public required string SiteName { get; init; }

    /// <summary>The accessible name of the language navigation.</summary>
    public required string LanguageLabel { get; init; }

    /// <summary>The accessible name of the English toggle item.</summary>
    public required string EnglishName { get; init; }

    /// <summary>The accessible name of the Dutch toggle item.</summary>
    public required string DutchName { get; init; }

    /// <summary>The text of the link that jumps to the list of games.</summary>
    public required string SkipLink { get; init; }

    /// <summary>The visually hidden heading of the list of games.</summary>
    public required string GamesListHeading { get; init; }

    /// <summary>The accessible name of the cabinet group.</summary>
    public required string CabinetLabel { get; init; }

    /// <summary>The visually hidden keyboard hint of the cabinet.</summary>
    public required string CabinetHint { get; init; }

    /// <summary>The heading shown until the first collection has been copied over.</summary>
    public required string FillingHeading { get; init; }

    /// <summary>The sentence under the being-filled heading.</summary>
    public required string FillingBody { get; init; }

    /// <summary>The message shown when scripts are off.</summary>
    public required string NoScript { get; init; }

    /// <summary>The footer version line with the version as <c>{0}</c> and the commit as <c>{1}</c>.</summary>
    public required string VersionFormat { get; init; }

    /// <summary>The alternative text of the BoardGameGeek credit, a brand name that stays the same in every language.</summary>
    public required string LogoAlt { get; init; }

    /// <summary>The text of the sync button while it can be used.</summary>
    public required string SyncNow { get; init; }

    /// <summary>The exact-time line with the written-out time as <c>{0}</c>.</summary>
    public required string LastSyncedFormat { get; init; }

    /// <summary>The accessible name of the search region above the cabinet.</summary>
    public required string FilterBarLabel { get; init; }

    /// <summary>The label and the placeholder of the search field.</summary>
    public required string SearchLabel { get; init; }

    /// <summary>The visible text of the button that clears the filters.</summary>
    public required string ClearVisible { get; init; }

    /// <summary>The accessible name of the button that clears the filters.</summary>
    public required string ClearName { get; init; }

    /// <summary>The spoken suffix of the players chip for exactly one player, with the number as <c>{0}</c>.</summary>
    public required string PlayerSuffixOneFormat { get; init; }

    /// <summary>The spoken suffix of the players chip for two to seven players, with the number as <c>{0}</c>.</summary>
    public required string PlayerSuffixManyFormat { get; init; }

    /// <summary>The spoken suffix of the players chip that means the largest group size or more.</summary>
    public required string PlayerSuffixMax { get; init; }

    /// <summary>The spoken suffix of the time chip, with the number of minutes as <c>{0}</c>.</summary>
    public required string TimeSuffixFormat { get; init; }

    /// <summary>The legend of the control that switches between one cabinet and a cabinet per location.</summary>
    public required string ViewLegend { get; init; }

    /// <summary>The view choice that shows every game in one cabinet.</summary>
    public required string ViewAll { get; init; }

    /// <summary>The view choice that shows one cabinet per storage location.</summary>
    public required string ViewByLocation { get; init; }

    /// <summary>The legend of the players choice.</summary>
    public required string PlayersLegend { get; init; }

    /// <summary>The legend of the play time choice.</summary>
    public required string TimeLegend { get; init; }

    /// <summary>The text of the choice that leaves a players or time filter off.</summary>
    public required string AnyChoice { get; init; }

    /// <summary>The visually hidden label of the location choice.</summary>
    public required string LocationLabel { get; init; }

    /// <summary>The location choice that leaves the location filter off.</summary>
    public required string AnyLocation { get; init; }

    /// <summary>The location choice and the heading for games that have no storage location.</summary>
    public required string UnknownLocation { get; init; }

    /// <summary>The text of the button that opens the filters on a narrow screen.</summary>
    public required string FiltersButton { get; init; }

    /// <summary>The word after the number of filters in use in the accessible name of the filters button.</summary>
    public required string FiltersActiveSuffix { get; init; }

    /// <summary>The visually hidden title of the filters sheet.</summary>
    public required string SheetTitle { get; init; }

    /// <summary>The footer version line for a build.</summary>
    /// <param name="version">The running version.</param>
    /// <param name="commit">The short commit the build came from.</param>
    public string Version(string version, string commit) =>
        string.Format(CultureInfo.InvariantCulture, VersionFormat, version, commit);

    /// <summary>The exact-time line for a written-out time.</summary>
    /// <param name="exact">The time of the last good sync, written out.</param>
    public string LastSynced(string exact) =>
        string.Format(CultureInfo.InvariantCulture, LastSyncedFormat, exact);

    /// <summary>The spoken suffix of a players chip: singular for one, the open-ended form for the largest size, plural otherwise.</summary>
    /// <param name="players">The number of players the chip stands for.</param>
    public string PlayersSuffix(int players) => players switch
    {
        1 => string.Format(CultureInfo.InvariantCulture, PlayerSuffixOneFormat, players),
        MaxPlayersChip => PlayerSuffixMax,
        _ => string.Format(CultureInfo.InvariantCulture, PlayerSuffixManyFormat, players),
    };

    /// <summary>The spoken suffix of a time chip.</summary>
    /// <param name="minutes">The longest play time the chip allows, in minutes.</param>
    public string TimeSuffix(int minutes) =>
        string.Format(CultureInfo.InvariantCulture, TimeSuffixFormat, minutes);
}
