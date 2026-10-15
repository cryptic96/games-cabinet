namespace Cabinet.Domain.Owner;

/// <summary>
/// How the cabinet looks to every visitor: the piece of furniture, the wood it is made of, what the backs of the cubbies are,
/// whether the cubbies are lit, and how deep they are drawn. Every field is a short name the owner data stores as it is and a
/// page writes as it is, so each one is only ever used when it is on its own allow-list; anything else reads as that field's
/// default and leaves the other four alone.
/// </summary>
/// <param name="Design">The piece of furniture, one of <see cref="CabinetLook.Designs"/>.</param>
/// <param name="Wood">The wood, one of <see cref="CabinetLook.Woods"/>.</param>
/// <param name="Backs">What the back of every cubby is, one of <see cref="CabinetLook.BackChoices"/>.</param>
/// <param name="Lights">When the cubby lights are on, one of <see cref="CabinetLook.LightChoices"/>.</param>
/// <param name="Depth">How deep the cubbies are drawn, one of <see cref="CabinetLook.DepthChoices"/>.</param>
public sealed record CabinetLook(string Design, string Wood, string Backs, string Lights, string Depth)
{
    /// <summary>The longest value a stored look field may have before it is even compared with an allow-list, in characters.</summary>
    public const int MaxValueLength = 32;

    /// <summary>The designs the owner can pick, in the order they are offered.</summary>
    public static IReadOnlyList<string> Designs { get; } = ["classic", "modern", "display", "legs", "library", "plywood", "plywood-legs"];

    /// <summary>The woods the owner can pick, in the order they are offered.</summary>
    public static IReadOnlyList<string> Woods { get; } = ["brown", "walnut", "oak", "birch", "mahogany"];

    /// <summary>What the backs of the cubbies can be: the wood itself or a painted panel.</summary>
    public static IReadOnlyList<string> BackChoices { get; } = ["wood", "painted"];

    /// <summary>When the cubby lights can be on: never, always, or only in the evening by each visitor's own clock.</summary>
    public static IReadOnlyList<string> LightChoices { get; } = ["off", "on", "evenings"];

    /// <summary>How deep the cubbies can be drawn.</summary>
    public static IReadOnlyList<string> DepthChoices { get; } = ["flat", "shaded", "deep"];

    /// <summary>The look the cabinet has until the owner picks another.</summary>
    public static CabinetLook Default { get; } = new("classic", "brown", "wood", "off", "shaded");

    /// <summary>Gives the look to use for a stored one: every field that is on its allow-list as it is, every other field as its default.</summary>
    public CabinetLook Normalized() => new(
        Pick(Design, Designs, Default.Design),
        Pick(Wood, Woods, Default.Wood),
        Pick(Backs, BackChoices, Default.Backs),
        Pick(Lights, LightChoices, Default.Lights),
        Pick(Depth, DepthChoices, Default.Depth));

    /// <summary>Tells whether a posted value is on an allow-list exactly, with no other spelling accepted.</summary>
    /// <param name="value">The value, or null when none was posted.</param>
    /// <param name="allowed">The allow-list of the field.</param>
    public static bool IsAllowed(string? value, IReadOnlyList<string> allowed)
    {
        ArgumentNullException.ThrowIfNull(allowed);

        return value is not null && allowed.Contains(value, StringComparer.Ordinal);
    }

    /// <summary>Tells whether the wall behind the cabinet is a light one, so text and the logo on it are drawn dark.</summary>
    /// <param name="wood">The wood, as it is stored.</param>
    public static bool HasLightWall(string wood) => wood is "birch" or "walnut";

    /// <summary>The name a design is shown under.</summary>
    /// <param name="design">The design, as it is stored.</param>
    public static string DesignName(string design) => design switch
    {
        "classic" => "Classic",
        "modern" => "Modern",
        "display" => "Display",
        "legs" => "On legs",
        "library" => "Library",
        "plywood" => "Plywood",
        "plywood-legs" => "Plywood on legs",
        _ => design,
    };

    /// <summary>The name a wood is shown under.</summary>
    /// <param name="wood">The wood, as it is stored.</param>
    public static string WoodName(string wood) => wood switch
    {
        "brown" => "Warm brown",
        "walnut" => "Walnut",
        "oak" => "Oak",
        "birch" => "Birch",
        "mahogany" => "Mahogany",
        _ => wood,
    };

    /// <summary>The name the back of the cubbies is shown under.</summary>
    /// <param name="backs">The choice, as it is stored.</param>
    public static string BacksName(string backs) => backs switch
    {
        "painted" => "Painted backs",
        _ => "Wood backs",
    };

    /// <summary>The name a lights choice is shown under.</summary>
    /// <param name="lights">The choice, as it is stored.</param>
    public static string LightsName(string lights) => lights switch
    {
        "off" => "Off",
        "on" => "Always on",
        "evenings" => "Evenings only",
        _ => lights,
    };

    /// <summary>The name a depth choice is shown under.</summary>
    /// <param name="depth">The choice, as it is stored.</param>
    public static string DepthName(string depth) => depth switch
    {
        "flat" => "Flat",
        "shaded" => "Shaded",
        "deep" => "Deep",
        _ => depth,
    };

    private static string Pick(string? stored, IReadOnlyList<string> allowed, string fallback) =>
        stored is not null && allowed.Contains(stored, StringComparer.Ordinal) ? stored : fallback;
}
