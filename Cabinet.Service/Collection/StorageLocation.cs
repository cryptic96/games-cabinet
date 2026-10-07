namespace Cabinet.Service.Collection;

/// <summary>The directory the service keeps its files in.</summary>
/// <param name="Path">The absolute path of the directory; it exists.</param>
public sealed record StorageDirectory(string Path);

/// <summary>Works out where the service keeps its files and makes sure the directory exists.</summary>
public static class StorageLocation
{
    /// <summary>The configuration key that names the storage directory.</summary>
    public const string DirectoryKey = "Storage:Directory";

    private const string ServiceManagerKey = "STATE_DIRECTORY";
    private const string DevelopmentDirectoryName = ".cabinet-state";

    /// <summary>
    /// Resolves the storage directory: the configured one, else the first directory the service manager provides, else a
    /// directory under the content root in Development only. The directory is created when it is missing.
    /// </summary>
    /// <param name="configuration">The configuration that may name the directory.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <exception cref="InvalidOperationException">No directory can be determined.</exception>
    public static string Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var path = ConfiguredDirectory(configuration)
            ?? FirstStateDirectory(configuration)
            ?? (environment.IsDevelopment() ? Path.Combine(environment.ContentRootPath, DevelopmentDirectoryName) : null);

        if (path is null)
        {
            throw new InvalidOperationException(
                $"{DirectoryKey} must be set when the service manager provides no state directory.");
        }

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(full);

        return full;
    }

    private static string? ConfiguredDirectory(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration[DirectoryKey]) ? null : configuration[DirectoryKey]!.Trim();

    private static string? FirstStateDirectory(IConfiguration configuration) =>
        configuration[ServiceManagerKey]?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
}
