namespace Cabinet.Domain.Owner;

/// <summary>
/// The wood finishes the cabinet can be shown in, and the edit that sets one. A stored finish is only ever used when it is on
/// the allow-list, because it ends up as a class on every page; anything else reads as the default.
/// </summary>
public static class Finishes
{
    private const string UnknownFinishStatusKey = "unknown-finish";

    /// <summary>The finishes the owner can pick, by the name they are stored under.</summary>
    public static IReadOnlyList<string> Allowed { get; } = [OwnerData.DefaultFinish];

    /// <summary>Gives the finish to use for a stored value: the value itself when it is on the allow-list, the default otherwise.</summary>
    /// <param name="stored">The stored value, which may be missing or hold anything.</param>
    public static string Normalize(string? stored) =>
        stored is not null && Allowed.Contains(stored, StringComparer.Ordinal) ? stored : OwnerData.DefaultFinish;

    /// <summary>
    /// Sets the finish of the cabinet. A finish that is not allowed is refused, and the finish the data already holds is
    /// nothing to do.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="value">The finish to set.</param>
    public static OwnerChange SetFinish(OwnerData data, string? value)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (value is null || !Allowed.Contains(value, StringComparer.Ordinal))
        {
            return OwnerChange.Refused(data, UnknownFinishStatusKey);
        }

        if (string.Equals(data.Finish, value, StringComparison.Ordinal))
        {
            return OwnerChange.None;
        }

        return new OwnerChange(data with { Finish = value }, $"Cabinet finish: {char.ToUpperInvariant(value[0])}{value[1..]}", "finish-set");
    }
}
