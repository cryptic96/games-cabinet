using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins what stays put when games and expansions are added to a collection of the kind the cabinet really holds. Adding
/// a plain game, an expansion whose base game is not owned, or an expansion that joins a stack its game already has
/// changes at most one cubby, and for an expansion of a family that cubby is the base game's. The one accepted exception
/// is an expansion that makes its family wider: the first expansion for a base game, the first one that lies in a game's
/// stack, or one that stands upright. That family may move and later cubbies may shift, so only the games ordered before
/// the base game are held to their exact cubby. A game that joins a series is the other accepted exception, tested with
/// the engine, so these checks leave out the appended games that join one.
/// </summary>
public class FamilyStabilityTests
{
    private const int Seeds = 200;
    private const int CollectionSize = 120;
    private const int ExpansionPercent = 15;
    private const int MarkerSeeds = 200;
    private const int MarkerCollectionSize = 200;
    private const int MarkerExpansionPercent = 35;
    private const int ThinDepthMm = 45;
    private const int ThickDepthMm = 55;

    private static readonly SectionDesign Design = SectionDesigns.Desktop;
    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0, CoverFromExpansions: 0);
    private static readonly LayoutOptions WithoutExpansionCovers = LayoutOptions.Default with { CoverFromExpansions = 0 };

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_base_game_changes_at_most_one_cubby_when_the_collection_has_expansions()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize, ExpansionPercent);
            var next = SyntheticCollections.NextBaseGame(items, seed);

            var (before, after) = BuildBeforeAndAfter(items, next);

            LayoutAssertions.AssertValid(after, [.. items, next]);

            if (!LayoutAssertions.JoinsSeries(items, next))
            {
                LayoutAssertions.ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            }
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_an_expansion_that_joins_a_stack_its_family_already_has_changes_only_the_base_games_cubby()
    {
        var tested = 0;

        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize, ExpansionPercent);
            var before = CabinetLayoutEngine.Build(items, Design, WithoutExpansionCovers);
            var baseId = FamiliesWithAStack(before).OrderBy(id => id).Skip(seed % 3).FirstOrDefault();

            if (baseId == 0)
            {
                continue;
            }

            var next = WithDepth(SyntheticCollections.NextExpansion(items, baseId, seed), ThinDepthMm);
            var after = CabinetLayoutEngine.Build([.. items, next], Design, WithoutExpansionCovers);
            var changed = LayoutAssertions.ChangedCubbies(before, after);

            changed.Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            var home = PositionOfBase(after, baseId);
            changed.Where(position => position.Section != home.Section || position.Cubby != home.Cubby)
                .Should().BeEmpty("seed {0}: only the cubby of base game {1} changes", seed, baseId);
            LayoutAssertions.AssertValid(after, [.. items, next]);
            tested++;
        }

        tested.Should().BeGreaterThan(Seeds / 2, "most seeded collections hold a family with a stack");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_thick_expansion_beyond_the_upright_room_to_a_cover_base_changes_only_the_base_games_cubby()
    {
        var filler = SyntheticCollections.Random(3, 8);
        var baseGame = new CabinetItem(
            NextId(filler), NextCollectionId(filler), "Invented Wide Cover Base", ItemKind.Base,
            new BoxDimensions(Design.Limits.MaxFamilyBaseWidthMm, 380, 50), []);
        var thin = ExpansionFor(baseGame, 1, ThinDepthMm);
        var items = new List<CabinetItem>([.. filler, baseGame, thin]);
        var next = ExpansionFor(baseGame, 2, ThickDepthMm);

        var (before, after) = BuildBeforeAndAfter(items, next);

        PlacementOf(before, next.BggId).HasValue.Should().BeFalse("the expansion is not in the cabinet before it arrives");
        PlacementOf(after, baseGame.BggId + 2)!.Value.Placement.Kind.Should().Be(PlacementKind.ExpansionLayer, "no room beside a base game at the width limit");
        PositionOfBaseKind(after, baseGame.BggId).Should().Be(PlacementKind.Cover);
        LayoutAssertions.ChangedCubbies(before, after).Should().Equal([PositionOfBase(after, baseGame.BggId)]);
        LayoutAssertions.AssertValid(after, [.. items, next]);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_thick_expansion_beyond_the_two_uprights_to_a_spine_base_changes_only_the_base_games_cubby()
    {
        var filler = SyntheticCollections.Random(7, 30);
        var baseGame = new CabinetItem(
            NextId(filler), NextCollectionId(filler), "Invented Narrow Spine Base", ItemKind.Base, new BoxDimensions(220, 300, 40), []);
        var items = new List<CabinetItem>(
            [.. filler, baseGame, ExpansionFor(baseGame, 1, ThickDepthMm), ExpansionFor(baseGame, 2, ThickDepthMm), ExpansionFor(baseGame, 3, ThinDepthMm)]);
        var next = ExpansionFor(baseGame, 4, ThickDepthMm);

        var before = CabinetLayoutEngine.Build(items, Design, SpinesOnly);
        var after = CabinetLayoutEngine.Build([.. items, next], Design, SpinesOnly);

        UprightsOf(before, baseGame.BggId).Should().HaveCount(2);
        PlacementOf(after, next.BggId)!.Value.Placement.Kind.Should().Be(PlacementKind.ExpansionLayer, "two uprights already stand beside the base game");
        LayoutAssertions.ChangedCubbies(before, after).Should().Equal([PositionOfBase(after, baseGame.BggId)]);
        LayoutAssertions.AssertValid(after, [.. items, next]);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_thicker_expansion_to_a_stack_with_room_lays_it_below_the_thinner_layers_and_changes_only_the_base_games_cubby()
    {
        var filler = SyntheticCollections.Random(5, 30);
        var baseGame = new CabinetItem(
            NextId(filler), NextCollectionId(filler), "Invented Stack Base", ItemKind.Base, new BoxDimensions(220, 300, 40), []);
        var items = new List<CabinetItem>([.. filler, baseGame, ExpansionFor(baseGame, 1, 30), ExpansionFor(baseGame, 2, 30)]);
        var next = ExpansionFor(baseGame, 3, ThinDepthMm);

        var before = CabinetLayoutEngine.Build(items, Design, SpinesOnly);
        var after = CabinetLayoutEngine.Build([.. items, next], Design, SpinesOnly);

        LayersOf(before, baseGame.BggId).Select(entry => entry.Placement.GameId).Should().Equal(baseGame.BggId + 1, baseGame.BggId + 2);
        LayersOf(after, baseGame.BggId).Select(entry => entry.Placement.GameId)
            .Should().Equal(next.BggId, baseGame.BggId + 1, baseGame.BggId + 2);
        LayoutAssertions.ChangedCubbies(before, after).Should().Equal([PositionOfBase(after, baseGame.BggId)]);
        LayoutAssertions.AssertValid(after, [.. items, next]);
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

            LayoutAssertions.AssertValid(after, [.. items, next]);

            if (!LayoutAssertions.JoinsSeries(items, next))
            {
                LayoutAssertions.ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            }
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
            var next = WithDepth(SyntheticCollections.NextExpansion(items, baseId, seed), ThinDepthMm);
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
    public void An_expansion_that_widens_its_family_may_move_it_but_keeps_earlier_games_in_place()
    {
        var uprightSeeds = 0;

        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize, ExpansionPercent);
            var baseId = FamilyBases(items).OrderBy(id => id).Skip(seed % 3).FirstOrDefault();

            if (baseId == 0)
            {
                continue;
            }

            var baseGame = items.Single(item => item.BggId == baseId && item.Kind == ItemKind.Base);
            var next = WithDepth(SyntheticCollections.NextExpansion(items, baseId, seed), ThickDepthMm);
            var (before, after) = BuildBeforeAndAfter(items, next, WithoutExpansionCovers);
            var arrival = PlacementOf(after, next.BggId);
            var home = PositionOfBase(after, baseId);

            LayoutAssertions.AssertValid(after, [.. items, next]);
            AssertEarlierGamesKeepTheirCubby(items, baseGame, before, after, seed);

            if (arrival is null)
            {
                MoreCountOf(after, baseId).Should().BeGreaterThan(MoreCountOf(before, baseId), "seed {0}: the arrival is counted by the marker", seed);

                continue;
            }

            (arrival.Value.Section, arrival.Value.Cubby).Should().Be(home, "seed {0}: the arrival stands in the cubby of its base game", seed);

            if (arrival.Value.Placement.Kind != PlacementKind.ExpansionSpine)
            {
                AssertStackedArrivalOfAFamilyWithAStack(before, after, baseId, seed);

                continue;
            }

            uprightSeeds++;
            AssertTouchesTheBaseOrThePreviousUpright(after, baseId, next.BggId, seed);
        }

        uprightSeeds.Should().BeGreaterThan(Seeds / 4, "most seeded families have room for an upright");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void First_expansion_for_a_base_game_reserves_its_place_and_keeps_earlier_games_in_place()
    {
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, CollectionSize);
            var baseGame = items[20 + (seed % 80)];
            var next = SyntheticCollections.NextExpansion(items, baseGame.BggId, seed);

            var (before, after) = BuildBeforeAndAfter(items, next);

            var arrival = PlacementOf(after, next.BggId);
            var home = PositionOfBase(after, baseGame.BggId);
            (arrival is not null ? (arrival.Value.Section, arrival.Value.Cubby) : home)
                .Should().Be(home, "seed {0}: the new expansion stands beside its base game", seed);
            arrival?.Placement.Kind.Should().BeOneOf(PlacementKind.ExpansionLayer, PlacementKind.ExpansionSpine);
            MoreCountOf(after, baseGame.BggId).Should().Be(arrival is null ? 1 : 0, "seed {0}", seed);
            LayoutAssertions.AssertValid(after, [.. items, next]);
            AssertEarlierGamesKeepTheirCubby(items, baseGame, before, after, seed);
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
            var beside = UprightsOf(after, absent.BggId).Concat(LayersOf(after, absent.BggId)).ToList();
            beside.Should().ContainSingle().Which.Placement.GameId.Should().Be(orphan.BggId);
        }
    }

    private static void AssertEarlierGamesKeepTheirCubby(
        IReadOnlyList<CabinetItem> items,
        CabinetItem baseGame,
        CabinetLayout before,
        CabinetLayout after,
        int seed)
    {
        var earlier = items
            .Where(item => item.CollectionId < baseGame.CollectionId)
            .Where(item => item.Kind == ItemKind.Base || LayoutAssertions.OwnedParentOf(item, items) is null)
            .Select(item => item.BggId)
            .ToHashSet();
        var placed = LayoutAssertions.PlacementsWithPosition(after);

        foreach (var entry in LayoutAssertions.PlacementsWithPosition(before).Where(entry => earlier.Contains(entry.Placement.GameId)
                     && entry.Placement.Kind is not (PlacementKind.ExpansionLayer or PlacementKind.ExpansionSpine or PlacementKind.MoreMarker)))
        {
            var moved = placed.Single(candidate => candidate.Placement.GameId == entry.Placement.GameId
                && candidate.Placement.Kind is not (PlacementKind.ExpansionLayer or PlacementKind.ExpansionSpine or PlacementKind.MoreMarker));
            (moved.Section, moved.Cubby).Should().Be((entry.Section, entry.Cubby), "seed {0}: game {1} was ordered before the base game", seed, entry.Placement.GameId);
        }
    }

    private static void AssertStackedArrivalOfAFamilyWithAStack(CabinetLayout before, CabinetLayout after, int baseId, int seed)
    {
        if (!FamiliesWithAStack(before).Contains(baseId))
        {
            return;
        }

        var home = PositionOfBase(after, baseId);

        LayoutAssertions.ChangedCubbies(before, after)
            .Where(position => position.Section != home.Section || position.Cubby != home.Cubby)
            .Should().BeEmpty("seed {0}: a thick arrival that joins an existing stack changes only the base's cubby", seed);
    }

    private static void AssertTouchesTheBaseOrThePreviousUpright(CabinetLayout after, int baseId, int arrivalId, int seed)
    {
        var baseEntry = LayoutAssertions.PlacementsWithPosition(after).Single(entry => entry.Placement.GameId == baseId
            && entry.Placement.Kind is PlacementKind.Cover or PlacementKind.Spine);
        var uprights = UprightsOf(after, baseId);
        var index = uprights.FindIndex(entry => entry.Placement.GameId == arrivalId);
        var edge = index == 0
            ? baseEntry.Placement.XMm + baseEntry.Placement.WidthMm
            : uprights[index - 1].Placement.XMm + uprights[index - 1].Placement.WidthMm;

        uprights[index].Placement.XMm.Should().Be(edge, "seed {0}: the upright touches the base game or the upright before it", seed);
        (uprights[index].Section, uprights[index].Cubby).Should().Be((baseEntry.Section, baseEntry.Cubby));
    }

    private static (CabinetLayout Before, CabinetLayout After) BuildBeforeAndAfter(
        IReadOnlyList<CabinetItem> items,
        CabinetItem next,
        LayoutOptions? options = null) =>
        (CabinetLayoutEngine.Build(items, Design, options ?? LayoutOptions.Default), CabinetLayoutEngine.Build([.. items, next], Design, options ?? LayoutOptions.Default));

    private static CabinetItem WithDepth(CabinetItem expansion, int depthMm) =>
        expansion with { Box = expansion.Box with { DepthMm = depthMm } };

    private static CabinetItem ExpansionFor(CabinetItem baseGame, int number, int depthMm) =>
        new(
            baseGame.BggId + number,
            baseGame.CollectionId + number,
            $"Invented Expansion {number} For {baseGame.BggId}",
            ItemKind.Expansion,
            new BoxDimensions(120, 200, depthMm),
            [new BaseGameRef(baseGame.BggId, baseGame.Title)]);

    private static int NextId(IReadOnlyList<CabinetItem> items) => items.Max(item => item.BggId) + 100;

    private static long NextCollectionId(IReadOnlyList<CabinetItem> items) => items.Max(item => item.CollectionId) + 100;

    private static IEnumerable<int> FamilyBases(IReadOnlyList<CabinetItem> items) =>
        items
            .Where(item => item.Kind == ItemKind.Expansion)
            .Select(item => LayoutAssertions.OwnedParentOf(item, items))
            .OfType<int>()
            .Distinct();

    private static IEnumerable<int> FamiliesWithAStack(CabinetLayout layout) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker)
            .Select(entry => entry.Placement.FamilyId!.Value)
            .Distinct();

    private static (int Section, int Cubby) PositionOfBase(CabinetLayout layout, int baseId)
    {
        var entry = LayoutAssertions.PlacementsWithPosition(layout)
            .Single(candidate => candidate.Placement.GameId == baseId
                && candidate.Placement.Kind is PlacementKind.Cover or PlacementKind.Spine or PlacementKind.FlatBox);

        return (entry.Section, entry.Cubby);
    }

    private static PlacementKind PositionOfBaseKind(CabinetLayout layout, int baseId) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Single(candidate => candidate.Placement.GameId == baseId
                && candidate.Placement.Kind is PlacementKind.Cover or PlacementKind.Spine or PlacementKind.FlatBox)
            .Placement.Kind;

    private static (int Section, int Cubby, Placement Placement)? PlacementOf(CabinetLayout layout, int gameId)
    {
        var matches = LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.GameId == gameId
                && entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.ExpansionSpine)
            .ToList();

        return matches.Count == 0 ? null : matches[0];
    }

    private static List<(int Section, int Cubby, Placement Placement)> LayersOf(CabinetLayout layout, int baseId) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer && entry.Placement.FamilyId == baseId)
            .OrderBy(entry => entry.Placement.YMm)
            .ToList();

    private static List<(int Section, int Cubby, Placement Placement)> UprightsOf(CabinetLayout layout, int baseId) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind == PlacementKind.ExpansionSpine && entry.Placement.FamilyId == baseId)
            .OrderBy(entry => entry.Placement.XMm)
            .ToList();

    private static int MoreCountOf(CabinetLayout layout, int baseId) =>
        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind == PlacementKind.MoreMarker && entry.Placement.FamilyId == baseId)
            .Sum(entry => entry.Placement.MoreCount ?? 0);
}
