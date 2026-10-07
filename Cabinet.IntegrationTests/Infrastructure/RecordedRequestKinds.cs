using Cabinet.FakeBgg.Testing;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>Splits what a scripted BGG transport recorded by the call it was for, so a test can count one kind of call.</summary>
public static class RecordedRequestKinds
{
    /// <summary>The recorded requests for a collection.</summary>
    /// <param name="handler">The scripted transport.</param>
    public static IReadOnlyList<RecordedRequest> CollectionRequests(this ScriptedBggHandler handler) =>
        [.. ArgumentNullGuard(handler).Requests.Where(request => request.Uri.AbsolutePath.EndsWith("/collection", StringComparison.Ordinal))];

    /// <summary>The recorded requests for game details.</summary>
    /// <param name="handler">The scripted transport.</param>
    public static IReadOnlyList<RecordedRequest> ThingRequests(this ScriptedBggHandler handler) =>
        [.. ArgumentNullGuard(handler).Requests.Where(request => request.Uri.AbsolutePath.EndsWith("/thing", StringComparison.Ordinal))];

    private static ScriptedBggHandler ArgumentNullGuard(ScriptedBggHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return handler;
    }
}
