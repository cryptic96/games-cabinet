using Cabinet.Domain.Owner;

namespace Cabinet.Service.Owner;

/// <summary>
/// The finish every visitor sees, read from the stored owner data and written by the server into the markup of every page on
/// both ports, so the first paint already has it. A stored value that is not on the allow-list, or owner data that cannot be
/// read, gives the default finish.
/// </summary>
public sealed class SiteFinish
{
    private const string ClassPrefix = "finish-";

    private readonly OwnerDataService _owner;

    /// <summary>Creates the finish reader.</summary>
    /// <param name="owner">The single holder of the owner data.</param>
    public SiteFinish(OwnerDataService owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <summary>The finish in use now, always one of <see cref="Finishes.Allowed"/>.</summary>
    public string Current => _owner.State is OwnerDataState.Ready ready ? Finishes.Normalize(ready.Data.Finish) : OwnerData.DefaultFinish;

    /// <summary>The class a page carries on its body to show the finish in use.</summary>
    public string BodyClass() => ClassPrefix + Current;
}
