namespace Cabinet.Domain;

/// <summary>The running build's version and source commit, parsed from the assembly's informational version.</summary>
/// <param name="Version">The semantic version of the build.</param>
/// <param name="Commit">The full source commit hash, or <c>unknown</c> when the build carries none.</param>
public sealed record BuildInfo(string Version, string Commit)
{
    private const string UnknownCommit = "unknown";
    private const string UnknownVersion = "0.0.0";
    private const int ShortCommitLength = 7;

    /// <summary>The first seven characters of the commit, or <c>unknown</c> when the commit is not known.</summary>
    public string ShortCommit =>
        Commit == UnknownCommit || Commit.Length <= ShortCommitLength ? Commit : Commit[..ShortCommitLength];

    /// <summary>
    /// Splits an informational version of the form <c>version+commit</c> at the first plus sign.
    /// A blank value yields <c>0.0.0</c> and <c>unknown</c>; a value without a plus sign has no known commit.
    /// </summary>
    public static BuildInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new BuildInfo(UnknownVersion, UnknownCommit);
        }

        var value = informationalVersion.Trim();
        var plus = value.IndexOf('+');

        if (plus < 0)
        {
            return new BuildInfo(value, UnknownCommit);
        }

        var version = value[..plus];
        var commit = value[(plus + 1)..];

        return new BuildInfo(
            version.Length == 0 ? UnknownVersion : version,
            commit.Length == 0 ? UnknownCommit : commit);
    }
}
