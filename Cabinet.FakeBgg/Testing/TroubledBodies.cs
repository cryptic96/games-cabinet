namespace Cabinet.FakeBgg.Testing;

/// <summary>
/// A response body for tests that hands out its first few bytes and then fails, the way a body read fails part way when a
/// connection drops, a compressed stream is broken or anything else goes wrong mid-download.
/// </summary>
/// <param name="failure">Creates the exception the second read throws.</param>
public sealed class BreakingBodyStream(Func<Exception> failure) : Stream
{
    private static readonly byte[] FirstBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private bool _started;

    /// <summary>A body that ends early the way a connection that drops part way through a download does.</summary>
    public static BreakingBodyStream DroppedConnection() =>
        new(() => new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely."));

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (_started)
        {
            throw failure();
        }

        _started = true;
        var length = Math.Min(buffer.Length, FirstBytes.Length);
        FirstBytes.AsSpan(0, length).CopyTo(buffer);

        return length;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return Read(buffer.Span);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// A response body for tests that sends nothing at all and ends only when the read is cancelled, the way a host that sent
/// its headers and then stalled behaves.
/// </summary>
/// <param name="reached">Runs once, when the reader first asks for bytes; null for nothing.</param>
public sealed class StallingBodyStream(Action? reached = null) : Stream
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once the reader has asked for the first bytes and is waiting for them.</summary>
    public Task Reached => _reached.Task;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("A stalled body can only be read asynchronously.");

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_reached.TrySetResult())
        {
            reached?.Invoke();
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        return 0;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
