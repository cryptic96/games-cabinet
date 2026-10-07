namespace Cabinet.Domain.Collection;

/// <summary>
/// The rules read when the cabinet is built from the stored collection, as opposed to the facts stored with each picture.
/// Changing a rule changes which picture a game shows without any picture being fetched again.
/// </summary>
/// <param name="Thresholds">The tuning values that turn the stored measurements of a picture into a verdict.</param>
public sealed record ArtRules(ArtThresholds Thresholds)
{
    /// <summary>The rules a cabinet starts with.</summary>
    public static ArtRules Default { get; } = new(ArtThresholds.Default);

    /// <summary>Text that differs whenever any rule differs, so a change of rules shows up in the collection version.</summary>
    public string Fingerprint => Thresholds.Fingerprint;
}
