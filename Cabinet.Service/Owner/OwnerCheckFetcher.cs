using Cabinet.Repository.Images;

namespace Cabinet.Service.Owner;

/// <summary>
/// Spaces out the pictures the owner's own check fetches. It keeps its own gap, apart from the picture pacer the sync and the
/// background link worker share, so a check never waits for the background work to finish a request.
/// </summary>
public interface IOwnerCheckPacer : IImagePacer;

/// <summary>The pacer of the owner's check: a configured gap that is never shorter than <see cref="ImagePacer.MinimumGap"/>.</summary>
public sealed class OwnerCheckPacer : IOwnerCheckPacer
{
    private readonly ImagePacer _inner;

    /// <summary>Creates the pacer.</summary>
    /// <param name="gap">The least time between two requests of the owner's check; raised to <see cref="ImagePacer.MinimumGap"/> when it is shorter.</param>
    /// <param name="time">The clock the gap is measured with.</param>
    public OwnerCheckPacer(TimeSpan gap, TimeProvider time) => _inner = new ImagePacer(gap, time);

    /// <inheritdoc />
    public Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken) => _inner.WaitTurnAsync(cancellationToken);
}

/// <summary>
/// The fetcher behind the owner's interactive "Check link", on a fetch slot of its own: its own picture pacer and its own
/// picture client, which sends no credentials and keeps the same host, redirect, size, pixel and time limits as the shared
/// slot. The background link worker keeps the shared slot, so at most two requests to the picture host can overlap, one from
/// each slot, and each slot keeps its own gap.
/// </summary>
/// <param name="fetcher">The fetcher built on the owner's check slot.</param>
public sealed class OwnerCheckFetcher(OwnerLinkFetcher fetcher)
{
    /// <summary>The name of the picture host a link must point at, for a message that says where pictures may come from.</summary>
    public string AllowedHost => fetcher.AllowedHost;

    /// <summary>The most bytes a picture may have, for a message that says how large is too large.</summary>
    public long MaxBytes => fetcher.MaxBytes;

    /// <summary>Classifies, fetches and processes the picture at an address the owner entered.</summary>
    /// <param name="text">The address as the owner typed it, or null.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    public Task<LinkCheck> CheckAsync(string? text, CancellationToken cancellationToken) => fetcher.CheckAsync(text, cancellationToken);
}
