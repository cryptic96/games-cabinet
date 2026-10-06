using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins the narrow section design used on phones and the readability floors derived from the real gutters and frame:
/// nothing a visitor can tap is smaller than a tap target at the narrowest supported screen, orphan boxes keep room for
/// their two lines of text, and the append stability the desktop design has holds on the phone design too.
/// </summary>
public class PhoneProfileTests
{
    private const int PhoneMinBoxThicknessMm = 59;
    private const int PhoneMinOrphanHeightMm = 89;
    private const int PhoneMinUprightWidthMm = 71;
    private const int DesktopMinOrphanHeightMm = 80;
    private const int DesktopMinBoxThicknessMm = 34;
    private const int DesktopMinUprightWidthMm = 64;
    private const int RandomSeeds = 100;
    private const int StabilitySeeds = 50;
    private const int StabilityCollectionSize = 120;
    private const int ExpansionPercent = 15;
    private const int ThinDepthMm = 45;
    private const int ThickDepthMm = 55;

    private static readonly SectionDesign Phone = SectionDesigns.Phone;
    private static readonly SectionDesign Desktop = SectionDesigns.Desktop;

    public static TheoryData<int, int, int, int> FloorCases => new()
    {
        { 24, 304, 680, 54 },
        { 36, 304, 680, 81 },
        { 36, 592, 1240, 76 },
        { 24, 300, 600, 48 },
        { 25, 300, 600, 50 },
        { 24, 304, 744, 59 },
        { 36, 304, 744, 89 },
        { 36, 592, 1304, 80 },
        { 29, 592, 1304, 64 },
        { 29, 304, 744, 71 },
    };

    [Theory]
    [MemberData(nameof(FloorCases))]
    [Trait("Category", "Layout")]
    public void The_floor_in_millimetres_is_the_pixel_target_rounded_up_to_whole_millimetres(
        int targetPx,
        int smallestRenderedWidthPx,
        int renderedWidthMm,
        int expected)
    {
        ReadabilityFloor.Millimetres(targetPx, smallestRenderedWidthPx, renderedWidthMm).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 304, 744)]
    [InlineData(24, 0, 744)]
    [InlineData(24, 304, 0)]
    [InlineData(-1, 304, 744)]
    [Trait("Category", "Layout")]
    public void The_floor_rejects_a_size_that_is_not_positive(int targetPx, int smallestRenderedWidthPx, int renderedWidthMm)
    {
        var act = () => ReadabilityFloor.Millimetres(targetPx, smallestRenderedWidthPx, renderedWidthMm);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_phone_design_derives_its_floors_from_the_rendered_width_including_the_furniture_sides()
    {
        Phone.OuterWidthMm.Should().Be(680);
        Phone.RenderedWidthMm.Should().Be(744);
        Phone.SmallestRenderedWidthPx.Should().Be(304);
        Phone.MinBoxThicknessMm.Should().Be(PhoneMinBoxThicknessMm);
        Phone.MinOrphanHeightMm.Should().Be(PhoneMinOrphanHeightMm);
        Phone.MinUprightExpansionWidthMm.Should().Be(PhoneMinUprightWidthMm);
        Phone.InteriorHeightMm.Should().Be(2060);
        Phone.Name.Should().Be(SectionDesigns.PhoneName);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_desktop_design_derives_its_orphan_and_upright_floors_and_keeps_its_starting_box_floor()
    {
        Desktop.OuterWidthMm.Should().Be(1240);
        Desktop.RenderedWidthMm.Should().Be(1304);
        Desktop.SmallestRenderedWidthPx.Should().Be(592);
        Desktop.MinBoxThicknessMm.Should().Be(DesktopMinBoxThicknessMm);
        Desktop.MinOrphanHeightMm.Should().Be(DesktopMinOrphanHeightMm);
        Desktop.MinUprightExpansionWidthMm.Should().Be(DesktopMinUprightWidthMm);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_phone_design_has_fewer_cubbies_across_than_the_desktop_design_and_a_different_cubby_count()
    {
        Phone.Rows.Max(row => row.CubbyWidthsMm.Count).Should().BeLessThan(Desktop.Rows.Max(row => row.CubbyWidthsMm.Count));
        Phone.Cubbies.Count.Should().Be(14);
        Phone.Cubbies.Count.Should().NotBe(Desktop.Cubbies.Count);
        SectionDesigns.TryGet(SectionDesigns.PhoneName, out var found).Should().BeTrue();
        found.Should().BeSameAs(Phone);
        SectionDesigns.All.Should().Equal(Desktop, Phone);
    }

    [Theory]
    [InlineData("65")]
    [InlineData("400")]
    [Trait("Category", "Layout")]
    public void Nothing_in_the_samples_is_thinner_on_a_phone_than_the_floor(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        var layout = CabinetLayoutEngine.Build(items, Phone);

        LayoutAssertions.AssertValid(layout, items);
        AssertEveryBoxMeetsTheFloors(layout, Phone, name);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Nothing_in_a_seeded_collection_is_thinner_on_a_phone_than_the_floor()
    {
        for (var seed = 1; seed <= RandomSeeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, 120, 15);

            var layout = CabinetLayoutEngine.Build(items, Phone);

            LayoutAssertions.AssertValid(layout, items);
            AssertEveryBoxMeetsTheFloors(layout, Phone, $"seed {seed}");
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Desktop_spines_are_at_least_as_wide_as_the_desktop_floor()
    {
        SyntheticCollections.TryGetSample("400", out var items);

        var layout = CabinetLayoutEngine.Build(items, Desktop);

        AllPlacements(layout).Where(placement => placement.Kind == PlacementKind.Spine)
            .Should().OnlyContain(placement => placement.WidthMm >= DesktopMinBoxThicknessMm);
        AllPlacements(layout).Where(placement => placement.Kind == PlacementKind.OrphanExpansion)
            .Should().OnlyContain(placement => placement.HeightMm >= DesktopMinOrphanHeightMm);
    }

    [Theory]
    [InlineData("65")]
    [InlineData("400")]
    [Trait("Category", "Layout")]
    public void The_cabinet_grows_with_the_collection_on_both_profiles_and_the_phone_needs_more_sections(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        var desktop = CabinetLayoutEngine.Build(items, Desktop);
        var phone = CabinetLayoutEngine.Build(items, Phone);

        phone.Profile.Should().Be(SectionDesigns.PhoneName);
        phone.Sections.Count.Should().BeGreaterThan(desktop.Sections.Count);
        phone.Sections.Should().OnlyContain(section => section.Cubbies.Count == Phone.Cubbies.Count);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_sample_builds_a_valid_layout_on_the_phone_design()
    {
        foreach (var name in SyntheticCollections.SampleNames)
        {
            SyntheticCollections.TryGetSample(name, out var items);

            LayoutAssertions.AssertValid(CabinetLayoutEngine.Build(items, Phone), items);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_a_phone_appending_a_base_game_or_an_orphan_changes_at_most_one_cubby()
    {
        for (var seed = 1; seed <= StabilitySeeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, StabilityCollectionSize, ExpansionPercent);
            var before = CabinetLayoutEngine.Build(items, Phone);

            foreach (var next in new[] { SyntheticCollections.NextBaseGame(items, seed), SyntheticCollections.NextOrphanExpansion(items, seed) })
            {
                var after = CabinetLayoutEngine.Build([.. items, next], Phone);

                LayoutAssertions.ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
                LayoutAssertions.AssertValid(after, [.. items, next]);
            }
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_a_phone_an_expansion_joining_an_existing_stack_changes_only_the_base_games_cubby()
    {
        var tested = 0;

        for (var seed = 1; seed <= StabilitySeeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, StabilityCollectionSize, ExpansionPercent);
            var before = CabinetLayoutEngine.Build(items, Phone);
            var baseId = FamiliesWithAStack(before).OrderBy(id => id).Skip(seed % 3).FirstOrDefault();

            if (baseId == 0)
            {
                continue;
            }

            var thin = SyntheticCollections.NextExpansion(items, baseId, seed);
            var next = thin with { Box = thin.Box with { DepthMm = ThinDepthMm } };
            var after = CabinetLayoutEngine.Build([.. items, next], Phone);
            var changed = LayoutAssertions.ChangedCubbies(before, after);
            var home = PositionOfBase(after, baseId);

            changed.Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            changed.Where(position => position != home).Should().BeEmpty("seed {0}: only the cubby of base game {1} changes", seed, baseId);
            LayoutAssertions.AssertValid(after, [.. items, next]);
            tested++;
        }

        tested.Should().BeGreaterThan(StabilitySeeds / 2, "most seeded collections hold a family with a stack");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_a_phone_an_expansion_that_widens_its_family_keeps_every_earlier_game_in_place()
    {
        for (var seed = 1; seed <= StabilitySeeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, StabilityCollectionSize);
            var baseGame = items[20 + (seed % 80)];
            var arrival = SyntheticCollections.NextExpansion(items, baseGame.BggId, seed);
            var next = arrival with { Box = arrival.Box with { DepthMm = seed % 2 == 0 ? ThickDepthMm : ThinDepthMm } };
            var before = CabinetLayoutEngine.Build(items, Phone);
            var after = CabinetLayoutEngine.Build([.. items, next], Phone);
            var earlier = items.Where(item => item.CollectionId < baseGame.CollectionId).Select(item => item.BggId).ToHashSet();
            var placedAfter = LayoutAssertions.PlacementsWithPosition(after);

            LayoutAssertions.AssertValid(after, [.. items, next]);

            foreach (var entry in LayoutAssertions.PlacementsWithPosition(before).Where(entry => earlier.Contains(entry.Placement.GameId)))
            {
                var moved = placedAfter.Single(candidate => candidate.Placement.GameId == entry.Placement.GameId);

                (moved.Section, moved.Cubby).Should().Be((entry.Section, entry.Cubby), "seed {0}: game {1} was ordered before the base game", seed, entry.Placement.GameId);
            }
        }
    }

    private static void AssertEveryBoxMeetsTheFloors(CabinetLayout layout, SectionDesign design, string what)
    {
        foreach (var placement in AllPlacements(layout))
        {
            switch (placement.Kind)
            {
                case PlacementKind.Spine:
                    placement.WidthMm.Should().BeGreaterThanOrEqualTo(design.MinBoxThicknessMm, "spine {0} in {1}", placement.GameId, what);
                    break;
                case PlacementKind.ExpansionSpine:
                    placement.WidthMm.Should().BeGreaterThanOrEqualTo(design.MinUprightExpansionWidthMm, "upright {0} in {1}", placement.GameId, what);
                    break;
                case PlacementKind.FlatBox:
                case PlacementKind.ExpansionLayer:
                case PlacementKind.MoreMarker:
                    placement.HeightMm.Should().BeGreaterThanOrEqualTo(design.MinBoxThicknessMm, "{0} {1} in {2}", placement.Kind, placement.GameId, what);
                    break;
                case PlacementKind.OrphanExpansion:
                    placement.HeightMm.Should().BeGreaterThanOrEqualTo(design.MinOrphanHeightMm, "orphan {0} in {1}", placement.GameId, what);
                    break;
                default:
                    break;
            }
        }
    }

    private static List<Placement> AllPlacements(CabinetLayout layout) =>
        layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).ToList();

    private static IEnumerable<int> FamiliesWithAStack(CabinetLayout layout) =>
        AllPlacements(layout)
            .Where(placement => placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker)
            .Select(placement => placement.FamilyId!.Value)
            .Distinct();

    private static (int Section, int Cubby) PositionOfBase(CabinetLayout layout, int baseId)
    {
        var entry = LayoutAssertions.PlacementsWithPosition(layout)
            .Single(candidate => candidate.Placement.GameId == baseId
                && candidate.Placement.Kind is PlacementKind.Cover or PlacementKind.Spine or PlacementKind.FlatBox);

        return (entry.Section, entry.Cubby);
    }
}
