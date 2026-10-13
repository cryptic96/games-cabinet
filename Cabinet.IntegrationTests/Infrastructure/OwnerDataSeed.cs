using Cabinet.Domain.Owner;
using Cabinet.Repository.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>Writes invented owner data into a state directory before a host starts, so a test can begin with decisions already made.</summary>
public static class OwnerDataSeed
{
    /// <summary>Stores the data in the owner-data file of the directory, the way the host's own store would.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist.</param>
    /// <param name="data">The invented owner data.</param>
    public static void Write(string directory, OwnerData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(data);

        new OwnerDataStore(directory, NullLogger<OwnerDataStore>.Instance).Save(data);
    }

    /// <summary>Keeps a copy of the owner-data file that is in the directory now as an earlier version made at a moment, the way the host's own store would.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist and hold an owner-data file.</param>
    /// <param name="at">The moment the copy is stamped with.</param>
    public static void KeepEarlierVersion(string directory, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        new OwnerDataStore(directory, NullLogger<OwnerDataStore>.Instance).KeepEarlierVersion(at);
    }

    /// <summary>Replaces the content of the owner-data file with bytes the store cannot read.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist.</param>
    /// <param name="content">The bytes the file holds from now on.</param>
    public static void WriteRaw(string directory, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(content);

        File.WriteAllBytes(Path.Combine(directory, OwnerDataStore.FileName), content);
    }

    /// <summary>The bytes of an owner-data file written by a release that is newer than this one.</summary>
    public static byte[] NewerFormat() =>
        System.Text.Encoding.UTF8.GetBytes($"{{\"kind\":\"{OwnerData.KindMarker}\",\"schemaVersion\":{OwnerData.CurrentSchemaVersion + 1},\"revision\":7}}");

    /// <summary>Builds owner data with the given locations and placements, at revision one.</summary>
    /// <param name="locations">The invented location names, in order; each gets an identifier made from its position.</param>
    /// <param name="placements">For a game, the name of the location it is kept in.</param>
    public static OwnerData With(IReadOnlyList<string> locations, IReadOnlyDictionary<int, string> placements)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(placements);

        var owned = locations.Select((name, index) => new OwnerLocation($"seed{index:D4}", name)).ToList();
        var games = placements
            .OrderBy(placement => placement.Key)
            .Select(placement => new OwnerGame(placement.Key, owned.Single(location => location.Name == placement.Value).Id, null, null))
            .ToList();

        return OwnerData.Empty with { Revision = 1, Locations = owned, Games = games };
    }
}
