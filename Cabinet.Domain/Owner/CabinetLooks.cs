namespace Cabinet.Domain.Owner;

/// <summary>The edit that sets the look of the cabinet, and the words that describe a look.</summary>
public static class CabinetLooks
{
    private const string UnknownLookStatusKey = "unknown-look";
    private const string LookSetStatusKey = "look-set";
    private const string SummarySeparator = " · ";

    /// <summary>
    /// Sets the look of the cabinet. A look with any value that is not on its own allow-list is refused whole, and a look equal to the
    /// one the data already shows is nothing to do.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="design">The design to set.</param>
    /// <param name="wood">The wood to set.</param>
    /// <param name="backs">What the backs of the cubbies are.</param>
    /// <param name="lights">When the cubby lights are on.</param>
    /// <param name="depth">How deep the cubbies are drawn.</param>
    public static OwnerChange SetLook(OwnerData data, string? design, string? wood, string? backs, string? lights, string? depth)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!CabinetLook.IsAllowed(design, CabinetLook.Designs)
            || !CabinetLook.IsAllowed(wood, CabinetLook.Woods)
            || !CabinetLook.IsAllowed(backs, CabinetLook.BackChoices)
            || !CabinetLook.IsAllowed(lights, CabinetLook.LightChoices)
            || !CabinetLook.IsAllowed(depth, CabinetLook.DepthChoices))
        {
            return OwnerChange.Refused(data, UnknownLookStatusKey);
        }

        var chosen = new CabinetLook(design!, wood!, backs!, lights!, depth!);

        if (chosen == data.Look.Normalized())
        {
            return OwnerChange.None;
        }

        return new OwnerChange(data with { Look = chosen }, $"Cabinet look set to {Summary(chosen)}", LookSetStatusKey);
    }

    /// <summary>Describes a look on one line, with the same words the owner's controls carry.</summary>
    /// <param name="look">The look, which is normalised first.</param>
    public static string Summary(CabinetLook look)
    {
        ArgumentNullException.ThrowIfNull(look);

        var shown = look.Normalized();

        return string.Join(
            SummarySeparator,
            CabinetLook.DesignName(shown.Design),
            CabinetLook.WoodName(shown.Wood),
            CabinetLook.BacksName(shown.Backs),
            $"Lights: {CabinetLook.LightsName(shown.Lights)}",
            $"Depth: {CabinetLook.DepthName(shown.Depth)}");
    }
}
