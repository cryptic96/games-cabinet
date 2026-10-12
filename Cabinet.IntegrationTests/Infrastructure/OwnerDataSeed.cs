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
