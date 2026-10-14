using Cabinet.Repository.Bgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Owner;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>A pacer for tests that never waits, so a sync of a few calls does not take the real gap between them.</summary>
public sealed class NoWaitPacer : IRequestPacer, IImagePacer, IOwnerCheckPacer
{
    /// <inheritdoc />
    public Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IDisposable>(NoLease.Instance);

    private sealed class NoLease : IDisposable
    {
        public static NoLease Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
