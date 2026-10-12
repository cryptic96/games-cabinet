using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves a sync commit and an owner save running at the same moment never leave visitors with a collection that misses one of them.</summary>
[Trait("Category", "Owner")]
public sealed class CollectionPublisherRaceTests
{
    private const string CabinetA = "Cabinet A";
    private const int Rounds = 2000;

    [Fact]
    public async Task After_a_publish_and_a_save_race_the_collection_shown_is_built_from_the_last_sync_and_the_last_owner_data()
    {
        var harness = OwnerTestKit.Harness(snapshot: Listing(0));
        await harness.AddAsync(CabinetA);
        var mismatches = 0;

        for (var round = 1; round <= Rounds; round++)
        {
            var snapshot = Listing(round);
            var revision = harness.Service.Revision;
            var place = round % 2 == 1;
            var save = Task.Run(
                () => harness.Service.MutateAsync(revision, data => LocationEdits.Assign(data, 100, place ? data.Locations[0].Id : null, "Game 100"), CancellationToken.None),
                TestContext.Current.CancellationToken);

            harness.Publisher.Publish(snapshot);
            await save;

            var expected = CollectionState.FromSnapshot(snapshot, ArtRules.Default, OwnerOverlay.From(snapshot, harness.Data));
            mismatches += harness.Collection.Current.Version == expected.Version ? 0 : 1;
        }

        mismatches.Should().Be(0);
    }

    private static CollectionSnapshot Listing(int round) =>
        OwnerTestKit.Snapshot([OwnerTestKit.Base(1, 100, $"Lantern Harbour {round}"), OwnerTestKit.Base(2, 101, "Quiet Quarry")]);
}
