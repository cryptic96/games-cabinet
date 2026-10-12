using System.Globalization;

namespace Cabinet.Service.Language;

/// <summary>
/// Every label the server writes into the page, in one language. The English and Dutch tables hold the same properties,
/// so a label that exists in one language and not the other cannot compile.
/// </summary>
public sealed record SiteText
{
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

    /// <summary>The footer version line for a build.</summary>
    /// <param name="version">The running version.</param>
    /// <param name="commit">The short commit the build came from.</param>
    public string Version(string version, string commit) =>
        string.Format(CultureInfo.InvariantCulture, VersionFormat, version, commit);

    /// <summary>The exact-time line for a written-out time.</summary>
    /// <param name="exact">The time of the last good sync, written out.</param>
    public string LastSynced(string exact) =>
        string.Format(CultureInfo.InvariantCulture, LastSyncedFormat, exact);
}
