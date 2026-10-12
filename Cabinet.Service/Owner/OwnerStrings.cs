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

    /// <summary>The document title of an owner page.</summary>
    /// <param name="page">The page's name.</param>
    public static string PageTitle(string page) => $"{page} - {Plate}";
}
