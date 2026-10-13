using Cabinet.Domain.Owner;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>An owner-data store that works like the one it wraps until a test tells it that the disk is full, and then refuses every save.</summary>
/// <param name="inner">The store that does the real work.</param>
public sealed class SometimesFailingStore(IOwnerDataStore inner) : IOwnerDataStore
{
    /// <summary>Whether every save fails with an input and output error.</summary>
    public bool FailSaves { get; set; }

    /// <inheritdoc />
    public OwnerDataState Load() => inner.Load();

    /// <inheritdoc />
    public void Save(OwnerData data)
    {
        if (FailSaves)
        {
            throw new IOException("The disk is full.");
        }

        inner.Save(data);
    }

    /// <inheritdoc />
    public void KeepEarlierVersion(DateTimeOffset now) => inner.KeepEarlierVersion(now);

    /// <inheritdoc />
    public IReadOnlyList<EarlierVersion> EarlierVersions() => inner.EarlierVersions();

    /// <inheritdoc />
    public byte[]? ReadEarlierVersion(int index) => inner.ReadEarlierVersion(index);

    /// <inheritdoc />
    public DateTimeOffset? NewestEarlierVersionAt() => inner.NewestEarlierVersionAt();
}
