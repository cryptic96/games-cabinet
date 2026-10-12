using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>
/// The one rule that says when a picture the owner linked counts as dead, and how a check changes what is known about a link.
/// A link is dead only after two failed checks that are at least a day apart, so a host that is down for an hour or a single
/// bad answer never marks it; one success wipes the failures. The background work that fetches links and the owner's pictures
/// page both use it, so they can never disagree.
/// </summary>
public static class LinkHealth
{
    /// <summary>How many failed checks a link needs before it can count as dead.</summary>
    public const int DeadAfterFailures = 2;

    /// <summary>How long the failures must span before a link counts as dead.</summary>
    public static readonly TimeSpan DeadAfterSpan = TimeSpan.FromDays(1);

    /// <summary>Tells whether a link counts as dead at a moment.</summary>
    /// <param name="link">The link and what is known about its checks.</param>
    /// <param name="now">The moment of the latest check, or the moment the answer is wanted for.</param>
    public static bool IsDead(PictureLink link, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.FailedChecks >= DeadAfterFailures
            && link.FailedSinceUtc is { } since
            && now - since >= DeadAfterSpan;
    }

    /// <summary>The link after one more failed check: the count goes up, and the moment the failures began is kept once set.</summary>
    /// <param name="link">The link before the check.</param>
    /// <param name="now">The moment of the failed check.</param>
    public static PictureLink AfterFailure(PictureLink link, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link with { FailedChecks = link.FailedChecks + 1, FailedSinceUtc = link.FailedSinceUtc ?? now };
    }

    /// <summary>The link after a successful check: the new record is stored and every failure is forgotten.</summary>
    /// <param name="link">The link before the check.</param>
    /// <param name="record">The record of the picture that was fetched.</param>
    public static PictureLink AfterSuccess(PictureLink link, ImageRecord record)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(record);

        return link with { Record = record, FailedChecks = 0, FailedSinceUtc = null };
    }
}
