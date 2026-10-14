using Cabinet.Domain.Owner;

namespace Cabinet.Service.Owner;

/// <summary>
/// The look every visitor sees, read from the stored owner data and written by the server into the markup of every page that
/// shows the cabinet, on both ports, so the first paint already has it. A stored value that is not on its field's allow-list gives
/// that field's default alone, and owner data that cannot be read gives the default look.
/// </summary>
public sealed class SiteLook
{
    private readonly OwnerDataService _owner;

    /// <summary>Creates the look reader.</summary>
    /// <param name="owner">The single holder of the owner data.</param>
    public SiteLook(OwnerDataService owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <summary>The look in use now, with every field on its allow-list.</summary>
    public CabinetLook Current => _owner.State is OwnerDataState.Ready ready ? ready.Data.Look.Normalized() : CabinetLook.Default;
}
