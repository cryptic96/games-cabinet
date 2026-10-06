using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins what stays put when games and expansions are added to a collection of the kind the cabinet really holds. Adding
/// a plain game, an expansion whose base game is not owned, or one more expansion for a game that already has some
/// changes at most one cubby. The one accepted exception is the first expansion for a base game that had none: that
/// family reserves its stack column and may move, and later cubbies may shift to make room, so only the games ordered
/// before the base game are held to their exact cubby.
/// </summary>
public class FamilyStabilityTests
{
    private const int Seeds = 200;
    private const int CollectionSize = 120;
    private const int ExpansionPercent = 15;
    private const int MarkerSeeds = 200;
    private const int MarkerCollectionSize = 200;
    private const int MarkerExpansionPercent = 35;

    private static readonly SectionDesign Design = SectionDesigns.Desktop;

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_base_game_changes_at_most_one_cubby_when_the_collection_has_expansions()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize, ExpansionPercent);
            var next = SyntheticCollections.NextBaseGame(items, seed);

            var (before, after) = BuildBeforeAndAfter(items, next);

            LayoutAssertions.ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            LayoutAssertions.AssertValid(after, [.. items, next]);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_an_expansion_to_a_family_that_already_has_one_changes_only_the_base_games_cubby()
    {
        var tested = 0;

        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize, ExpansionPercent);
            var baseId = FamilyBases(items).OrderBy(id => id).Skip(seed % 3).FirstOrDefault();

            if (baseId == 0)
            {
                continue;
            }

            var next = SyntheticCollections.NextExpansion(items, baseId, seed);
            var (before, after) = BuildBeforeAndAfter(items, next);
            var changed = LayoutAssertions.ChangedCubbies(before, after);

            changed.Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            var home = PositionOfBase(after, baseId);
            changed.Where(position => position.Section != home.Section || position.Cubby != home.Cubby)
                .Should().BeEmpty("seed {0}: only the cubby of base game {1} changes", seed, baseId);
            LayoutAssertions.AssertValid(after, [.. items, next]);
            tested++;
        }

        tested.Should().BeGreaterThan(Seeds / 2, "most seeded collections hold a family");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_an_expansion_whose_base_game_is_not_owned_changes_at_most_one_cubby()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize, ExpansionPercent);
            var next = SyntheticCollections.NextOrphanExpansion(items, seed);

            var (before, after) = BuildBeforeAndAfter(items, next);

            LayoutAssertions.ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            LayoutAssertions.AssertValid(after, [.. items, next]);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_an_expansion_to_a_stack_that_shows_the_marker_only_raises_the_hidden_count()
    {
        var tested = 0;

        for (var seed = 1; seed <= MarkerSeeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, MarkerCollectionSize, MarkerExpansionPercent);
            var before = CabinetLayoutEngine.Build(items, Design);
            var marker = LayoutAssertions.PlacementsWithPosition(before)
                .FirstOrDefault(entry => entry.Placement.Kind == PlacementKind.MoreMarker);

            if (marker.Placement is null)
            {
                continue;
            }

            var baseId = marker.Placement.FamilyId!.Value;
            var next = SyntheticCollections.NextExpansion(items, baseId, seed);
            var after = CabinetLayoutEngine.Build([.. items, next], Design);

            LayersOf(after, baseId).Should().Equal(LayersOf(before, baseId), "seed {0}: the visible layers stay as they were", seed);
            MoreCountOf(after, baseId).Should().Be(marker.Placement.MoreCount + 1, "seed {0}", seed);
            LayoutAssertions.ChangedCubbies(before, after).Should().Equal([(marker.Section, marker.Cubby)], "seed {0}", seed);
            tested++;
        }

        tested.Should().BeGreaterThanOrEqualTo(20, "enough seeded collections hold a full stack");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void First_expansion_for_a_base_game_reserves_its_stack_and_keeps_earlier_games_in_place()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize);
            var baseGame = items[20 + (seed % 80)];
            var next = SyntheticCollections.NextExpansion(items, baseGame.BggId, seed);

            var (before, after) = BuildBeforeAndAfter(items, next);

            var placed = LayoutAssertions.PlacementsWithPosition(after);
            var layer = placed.SingleOrDefault(entry => entry.Placement.GameId == next.BggId && entry.Placement.Kind == PlacementKind.ExpansionLayer);
            var home = PositionOfBase(after, baseGame.BggId);
            (layer.Placement is not null ? (layer.Section, layer.Cubby) : home)
                .Should().Be(home, "seed {0}: the new expansion stands in the stack of its base game", seed);
            MoreCountOf(after, baseGame.BggId).Should().Be(layer.Placement is null ? 1 : 0, "seed {0}", seed);
            LayoutAssertions.AssertValid(after, [.. items, next]);

            var earlier = items.Where(item => item.CollectionId < baseGame.CollectionId).Select(item => item.BggId).ToHashSet();
            var earlierBefore = LayoutAssertions.PlacementsWithPosition(before).Where(entry => earlier.Contains(entry.Placement.GameId));

            foreach (var entry in earlierBefore)
            {
                var moved = placed.Single(candidate => candidate.Placement.GameId == entry.Placement.GameId);
                (moved.Section, moved.Cubby).Should().Be((entry.Section, entry.Cubby), "seed {0}: game {1} was ordered before the base game", seed, entry.Placement.GameId);
            }
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_the_base_game_of_an_existing_orphan_turns_the_orphan_into_part_of_that_family()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var items = SyntheticCollections.Random(seed, 60);
            var orphan = SyntheticCollections.NextOrphanExpansion(items, seed);
            var withOrphan = new List<CabinetItem>([.. items, orphan]);
            var absent = orphan.ExpansionOf[0];
            var arrival = SyntheticCollections.NextBaseGame(withOrphan, seed) with { BggId = absent.BggId, Title = absent.Title };

            var before = CabinetLayoutEngine.Build(withOrphan, Design);
            var after = CabinetLayoutEngine.Build([.. withOrphan, arrival], Design);

            LayoutAssertions.AssertValid(before, withOrphan);
            LayoutAssertions.AssertValid(after, [.. withOrphan, arrival]);
            LayoutAssertions.PlacementsWithPosition(before).Single(entry => entry.Placement.GameId == orphan.BggId)
                .Placement.Kind.Should().Be(PlacementKind.OrphanExpansion);
            LayersOf(after, absent.BggId).Should().ContainSingle().Which.Placement.GameId.Should().Be(orphan.BggId);
        }
    }

    private static (CabinetLayout Before, CabinetLayout After) BuildBeforeAndAfter(IReadOnlyList<CabinetItem> items, CabinetItem next) =>
        (CabinetLayoutEngine.Build(items, Design), CabinetLayoutEngine.Build([.. items, next], Design));

    private static IEnumerable<int> FamilyBases(IReadOnlyList<CabinetItem> items) =>
        items
            .Where(item => item.Kind == ItemKind.Expansion)
            .Select(item => LayoutAssertions.OwnedParentOf(item, items))
            .OfType<int>()
            .Distinct();

    private static (int Section, int Cubby) PositionOfBase(CabinetLayout layout, int baseId)
    {
        var entry = LayoutAssertions.PlacementsWithPosition(layout)
            .Single(candidate => candidate.Placement.GameId == baseId
                && candidate.Placement.Kind is PlacementKind.Cover or PlacementKind.Spine or PlacementKind.FlatBox);

        return (entry.Section, entry.Cubby);
    }

    private static List<(int Section, int Cubby, Placement Placement)> LayersOf(CabinetLayout layout, int baseId) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer && entry.Placement.FamilyId == baseId)
            .OrderBy(entry => entry.Placement.YMm)
            .ToList();

    private static int MoreCountOf(CabinetLayout layout, int baseId) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind == PlacementKind.MoreMarker && entry.Placement.FamilyId == baseId)
            .Sum(entry => entry.Placement.MoreCount ?? 0);
}
