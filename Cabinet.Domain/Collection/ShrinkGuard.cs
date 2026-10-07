using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cabinet.Domain.Collection;

/// <summary>What the guard decided about a freshly fetched collection.</summary>
public abstract record GuardDecision
{
    /// <summary>The fetched collection may replace the shown one.</summary>
    public sealed record Accept : GuardDecision;

    /// <summary>The fetched collection looks wrong, so it is set aside and the shown one stays.</summary>
    /// <param name="Kind">What looked wrong.</param>
    /// <param name="Fingerprint">The fingerprint of the set aside collection's entry identifiers.</param>
    /// <param name="Count">How many entries the set aside collection held.</param>
    public sealed record HeldBack(HeldBackKind Kind, string Fingerprint, int Count) : GuardDecision;
}

/// <summary>
/// Decides whether a freshly fetched collection may replace the one that is shown. A collection that is empty when games
/// were shown is never accepted. A collection that lost more than half of the shown entries is accepted only when the next
/// fetch returns exactly the same set of entries, which tells a real clear-out from a partial answer. Everything else is
/// accepted. The guard is pure: it looks at counts and identifiers and nothing else.
/// </summary>
public static class ShrinkGuard
{
    /// <summary>Decides what to do with a fetched collection.</summary>
    /// <param name="previousCount">How many entries the shown collection holds; zero when none was ever shown.</param>
    /// <param name="candidateEntryIds">The collection entry identifiers of the fetched collection, each once.</param>
    /// <param name="heldBack">The collection set aside by the previous sync, or null when none is.</param>
    public static GuardDecision Evaluate(int previousCount, IReadOnlyCollection<long> candidateEntryIds, HeldBackRecord? heldBack)
    {
        ArgumentNullException.ThrowIfNull(candidateEntryIds);

        if (previousCount <= 0)
        {
            return new GuardDecision.Accept();
        }

        if (candidateEntryIds.Count == 0)
        {
            return new GuardDecision.HeldBack(HeldBackKind.Empty, Fingerprint(candidateEntryIds), 0);
        }

        var removed = previousCount - candidateEntryIds.Count;

        if (removed * 2 <= previousCount)
        {
            return new GuardDecision.Accept();
        }

        var fingerprint = Fingerprint(candidateEntryIds);

        return heldBack is { Kind: HeldBackKind.Shrunk } && heldBack.Fingerprint == fingerprint
            ? new GuardDecision.Accept()
            : new GuardDecision.HeldBack(HeldBackKind.Shrunk, fingerprint, candidateEntryIds.Count);
    }

    /// <summary>
    /// The fingerprint of a set of collection entries: the lowercase hexadecimal SHA-256 of the identifiers in ascending
    /// order, joined by commas, so the same set gives the same fingerprint in any order.
    /// </summary>
    /// <param name="entryIds">The collection entry identifiers.</param>
    public static string Fingerprint(IEnumerable<long> entryIds)
    {
        ArgumentNullException.ThrowIfNull(entryIds);

        var joined = string.Join(',', entryIds.Order().Select(id => id.ToString(CultureInfo.InvariantCulture)));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
    }
}
