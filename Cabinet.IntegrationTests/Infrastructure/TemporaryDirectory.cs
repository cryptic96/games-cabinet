namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>A directory under the system temporary path that is deleted when the test is done with it.</summary>
public sealed class TemporaryDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public TemporaryDirectory() =>
        Directory.CreateDirectory(FullPath = Path.Combine(Path.GetTempPath(), $"cabinet-sync-tests-{Guid.NewGuid():N}"));

    /// <summary>The absolute path of the directory.</summary>
    public string FullPath { get; }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(FullPath, recursive: true);
}
