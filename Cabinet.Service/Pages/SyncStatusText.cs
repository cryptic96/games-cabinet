using System.Globalization;

namespace Cabinet.Service.Pages;

/// <summary>
/// The words of the sync status line as the server writes them on first paint, in UTC. The page script rewrites the same
/// sentences from the visitor's own clock, so the rules here and in the script's strings module are kept identical.
/// </summary>
public static class SyncStatusText
{
    /// <summary>What the status line says before any sync has succeeded.</summary>
    public const string NeverSynced = "Not synced yet";

    private const string JustNow = "Synced just now";
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const int SecondsPerDay = 86400;

    /// <summary>
    /// How long ago the collection was synced, in whole units rounded down: just now under a minute (or for a time in the
    /// future), then minutes, then hours up to a day, then days, where one day reads as yesterday.
    /// </summary>
    /// <param name="elapsed">The time since the last good sync.</param>
    public static string Relative(TimeSpan elapsed)
    {
        var seconds = (long)Math.Floor(elapsed.TotalSeconds);

        if (seconds < SecondsPerMinute)
        {
            return JustNow;
        }

        if (seconds < SecondsPerHour)
        {
            return $"Synced {Plural(seconds / SecondsPerMinute, "minute")} ago";
        }

        if (seconds < SecondsPerDay)
        {
            return $"Synced {Plural(seconds / SecondsPerHour, "hour")} ago";
        }

        var days = seconds / SecondsPerDay;

        return days == 1 ? "Synced yesterday" : $"Synced {days.ToString(CultureInfo.InvariantCulture)} days ago";
    }

    /// <summary>The moment written out in UTC for the first paint, for example <c>6 October 2026 at 12:32 UTC</c>.</summary>
    /// <param name="at">The moment to write.</param>
    public static string ExactUtc(DateTimeOffset at) =>
        at.UtcDateTime.ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>The sentence shown when recent syncs have not gone through.</summary>
    /// <param name="exact">The exact time of the last good sync.</param>
    public static string StaleRecent(string exact) =>
        $"Showing the last sync from {exact}. Recent syncs haven't gone through.";

    /// <summary>The sentence shown while a suspicious result is waiting for the next sync to confirm.</summary>
    /// <param name="exact">The exact time of the last good sync.</param>
    public static string StaleHeldBack(string exact) =>
        $"Showing the last sync from {exact}. A much smaller collection from BGG is waiting for the next sync to confirm.";

    /// <summary>
    /// Whether the page should say it is showing an older sync: only once something has been synced, and then either while a
    /// result is held back or when the last good sync is at least as old as the stale threshold.
    /// </summary>
    /// <param name="lastSyncedUtc">When the collection was last confirmed, or null before the first sync.</param>
    /// <param name="now">The current time.</param>
    /// <param name="staleAfter">How old the collection may get before it counts as stale.</param>
    /// <param name="heldBack">Whether a suspicious result is waiting to be confirmed.</param>
    public static bool IsStale(DateTimeOffset? lastSyncedUtc, DateTimeOffset now, TimeSpan staleAfter, bool heldBack)
    {
        if (lastSyncedUtc is not { } last)
        {
            return false;
        }

        return heldBack || now - last >= staleAfter;
    }

    private static string Plural(long count, string unit) =>
        count == 1 ? $"1 {unit}" : $"{count.ToString(CultureInfo.InvariantCulture)} {unit}s";
}
