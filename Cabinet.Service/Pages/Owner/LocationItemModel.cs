using Cabinet.Domain.Owner;

namespace Cabinet.Service.Pages.Owner;

/// <summary>
/// One location of the list as the item markup needs it: the location itself, where it stands in the list, how many games are
/// kept there, and what the item has to say after a form was posted without script.
/// </summary>
/// <param name="Location">The location.</param>
/// <param name="Position">Where it stands in the list, counting from one; zero for the template a script copies.</param>
/// <param name="Total">How many locations the list holds.</param>
/// <param name="Games">How many games are shown in this location, counting expansions that follow their base game here.</param>
/// <param name="NotPlacedOnDelete">How many games would be not placed if this location were deleted.</param>
/// <param name="Revision">The revision of the owner data the forms were built from.</param>
public sealed record LocationItemModel(
    OwnerLocation Location,
    int Position,
    int Total,
    int Games,
    int NotPlacedOnDelete,
    long Revision)
{
    /// <summary>The words that explain why the name was refused, or null.</summary>
    public string? ErrorText { get; init; }

    /// <summary>The words for the outcome of the post that sent the browser here, shown in the item's own status line, or null.</summary>
    public string? StatusText { get; init; }

    /// <summary>Whether <see cref="StatusText"/> reports a problem.</summary>
    public bool StatusIsProblem { get; init; }

    /// <summary>The revision of the change the item's Undo form would take back, or null when there is none to offer.</summary>
    public long? UndoRevision { get; init; }

    /// <summary>Whether the item asks for confirmation before it is deleted.</summary>
    public bool ConfirmingDelete { get; init; }

    /// <summary>Whether this is the template a script copies for a location it adds, which carries no real location.</summary>
    public bool IsTemplate => Position == 0;

    /// <summary>The identifier of the item's anchor on the page.</summary>
    public string AnchorId => $"loc-{Location.Id}";

    /// <summary>The identifier of the item's name error.</summary>
    public string ErrorId => $"loc-error-{Location.Id}";

    /// <summary>The template a script copies for a location it adds.</summary>
    /// <param name="revision">The revision of the owner data the page was built from.</param>
    public static LocationItemModel Template(long revision) => new(new OwnerLocation(string.Empty, string.Empty), 0, 0, 0, 0, revision);
}
