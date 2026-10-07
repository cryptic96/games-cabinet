namespace Cabinet.Repository.Storage;

/// <summary>Writes a file so that a reader, or a crash, sees either the whole old content or the whole new content.</summary>
public static class AtomicJsonFile
{
    private const string TemporaryFilePattern = ".*.tmp";

    /// <summary>
    /// Writes the content to a temporary file in the same directory, flushes it to disk and renames it over the target.
    /// Nothing is left behind when the write fails.
    /// </summary>
    /// <param name="path">The file to create or replace.</param>
    /// <param name="content">The bytes to write.</param>
    public static void WriteAtomically(string path, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, fullPath, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);
            throw;
        }
    }

    /// <summary>Deletes temporary files an interrupted write left in the directory.</summary>
    /// <param name="directory">The directory to clean.</param>
    public static void RemoveStrayTemporaryFiles(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, TemporaryFilePattern))
        {
            DeleteQuietly(file);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }
    }
}
