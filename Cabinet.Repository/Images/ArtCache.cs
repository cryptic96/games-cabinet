using System.Text.RegularExpressions;
using Cabinet.Repository.Storage;

namespace Cabinet.Repository.Images;

/// <summary>
/// The directory of stored pictures beside the stored collection. Every file is written whole or not at all, every name is
/// checked before it is used as a path, and a file is removed only when nothing refers to it and it has been unused for the
/// grace period, so a picture a page is still loading never disappears.
/// </summary>
public sealed partial class ArtCache
{
    /// <summary>The name of the directory inside the storage directory.</summary>
    public const string DirectoryName = "art";

    private const string FilePattern = "*.webp";

    /// <summary>Creates the cache, makes sure its directory exists and removes the temporary files an interrupted write left.</summary>
    /// <param name="stateDirectory">The storage directory; it must exist.</param>
    public ArtCache(string stateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);

        Path = System.IO.Path.Combine(stateDirectory, DirectoryName);
        Directory.CreateDirectory(Path);
        AtomicJsonFile.RemoveStrayTemporaryFiles(Path);
    }

    /// <summary>The directory the pictures are stored in.</summary>
    public string Path { get; }

    /// <summary>Tells whether a stored file with this name exists.</summary>
    /// <param name="name">The file name.</param>
    public bool Has(string name) => IsValidName(name) && File.Exists(System.IO.Path.Combine(Path, name));

    /// <summary>Writes a file under its own name, atomically.</summary>
    /// <param name="art">The encoded picture.</param>
    /// <exception cref="ArgumentException">The name is not of the form the processor produces.</exception>
    public void Write(EncodedArt art)
    {
        ArgumentNullException.ThrowIfNull(art);

        if (!IsValidName(art.Name))
        {
            throw new ArgumentException("The file name is not a stored picture name.", nameof(art));
        }

        AtomicJsonFile.WriteAtomically(System.IO.Path.Combine(Path, art.Name), art.Bytes);
    }

    /// <summary>
    /// Deletes the files that nothing refers to and that were last written before the grace period began. Files that are
    /// referenced, files that are young and everything that is not a stored picture are left alone.
    /// </summary>
    /// <param name="referenced">The names of the files some stored record refers to.</param>
    /// <param name="grace">How long an unreferenced file is kept.</param>
    /// <param name="now">The current time.</param>
    /// <returns>How many files were deleted.</returns>
    public int Prune(IReadOnlySet<string> referenced, TimeSpan grace, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(referenced);

        var deleted = 0;
        var cutoff = now - grace;

        foreach (var file in Directory.EnumerateFiles(Path, FilePattern))
        {
            var name = System.IO.Path.GetFileName(file);

            if (!IsValidName(name) || referenced.Contains(name) || File.GetLastWriteTimeUtc(file) >= cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }
        }

        return deleted;
    }

    private static bool IsValidName(string name) => NamePattern().IsMatch(name);

    [GeneratedRegex(@"^[0-9a-f]{16}-[0-9]{1,4}\.webp\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
