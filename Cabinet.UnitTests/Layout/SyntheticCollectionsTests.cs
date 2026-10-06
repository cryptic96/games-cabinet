using System.Globalization;
using System.Text.Json;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Verifies the invented collections are stable, correctly sized, unique and free of anything real.</summary>
public class SyntheticCollectionsTests
{
    private const string EdgeName = "edge";
    private const char CombiningDiaeresis = (char)0x0308;

    public static TheoryData<string> NumericSampleNames => new(SyntheticCollections.SampleNames.Where(name => name != EdgeName));

    public static TheoryData<string> AllSampleNames => new(SyntheticCollections.SampleNames);

    [Theory]
    [MemberData(nameof(NumericSampleNames))]
    [Trait("Category", "Layout")]
    public void A_numeric_sample_has_exactly_its_named_count(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items).Should().BeTrue();

        items.Should().HaveCount(int.Parse(name, CultureInfo.InvariantCulture));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_edge_sample_holds_fourteen_games_with_awkward_titles()
    {
        SyntheticCollections.TryGetSample(EdgeName, out var items).Should().BeTrue();

        items.Should().HaveCount(14);
        items.Should().Contain(item => item.Title.Length >= 80, "one title is very long");
        items.Should().Contain(item => item.Title.Length == 0, "one title is blank");
        items.Should().Contain(item => item.Title.Contains(": ", StringComparison.Ordinal));
        items.Should().Contain(item => item.Title.Contains(" - ", StringComparison.Ordinal));
        items.Should().Contain(item => item.Title.Length > 28 && !item.Title.Contains(' ', StringComparison.Ordinal), "one title is a single long word");
        items.Any(item => item.Title.Any(IsKatakana)).Should().BeTrue("one title is katakana");
        items.Any(item => item.Title.Any(IsHebrew)).Should().BeTrue("one title is Hebrew");
        items.Should().Contain(item => item.Title.Contains(CombiningDiaeresis), "one title has a combining diaeresis");
        items.Should().Contain(item => item.Title.Any(char.IsSurrogate), "one title holds an emoji");
    }

    private static bool IsKatakana(char character) => character is >= '\u30A0' and <= '\u30FF';

    private static bool IsHebrew(char character) => character is >= '\u05D0' and <= '\u05EA';

    [Theory]
    [MemberData(nameof(AllSampleNames))]
    [Trait("Category", "Layout")]
    public void A_sample_is_identical_on_repeated_calls(string name)
    {
        SyntheticCollections.TryGetSample(name, out var first);
        SyntheticCollections.TryGetSample(name, out var second);

        JsonSerializer.Serialize(first).Should().Be(JsonSerializer.Serialize(second));
    }

    [Theory]
    [MemberData(nameof(AllSampleNames))]
    [Trait("Category", "Layout")]
    public void A_sample_has_unique_titles_and_ascending_identifiers(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        items.Select(item => item.Title).Should().OnlyHaveUniqueItems();
        items.Select(item => item.BggId).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        items.Select(item => item.CollectionId).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(AllSampleNames))]
    [Trait("Category", "Layout")]
    public void A_sample_title_never_holds_an_ascii_digit(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        items.Should().OnlyContain(item => !item.Title.Any(char.IsAsciiDigit));
    }

    [Theory]
    [InlineData("")]
    [InlineData("64")]
    [InlineData("-1")]
    [InlineData("Edge")]
    [InlineData("abc")]
    [InlineData(null)]
    [Trait("Category", "Layout")]
    public void An_unknown_sample_name_finds_nothing(string? name)
    {
        SyntheticCollections.TryGetSample(name, out var items).Should().BeFalse();

        items.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_random_collection_is_fixed_by_its_seed_and_has_unique_titles()
    {
        var first = SyntheticCollections.Random(11, 120);
        var second = SyntheticCollections.Random(11, 120);
        var other = SyntheticCollections.Random(12, 120);

        JsonSerializer.Serialize(first).Should().Be(JsonSerializer.Serialize(second));
        JsonSerializer.Serialize(first).Should().NotBe(JsonSerializer.Serialize(other));
        first.Select(item => item.Title).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_sample_of_sixty_five_holds_forty_nine_base_games_and_sixteen_expansions_in_the_shapes_the_cabinet_must_handle()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var expansions = items.Where(item => item.Kind == ItemKind.Expansion).ToList();
        var ownedIds = items.Where(item => item.Kind == ItemKind.Base).Select(item => item.BggId).ToHashSet();
        var ownedParents = expansions.Select(expansion => expansion.ExpansionOf.Where(reference => ownedIds.Contains(reference.BggId)).ToList()).ToList();

        items.Count(item => item.Kind == ItemKind.Base).Should().Be(49);
        expansions.Should().HaveCount(16);
        ownedParents.Count(parents => parents.Count == 0).Should().Be(3, "three expansions name a base game that is not owned");
        ownedParents.Count(parents => parents.Count == 2).Should().Be(1, "one expansion extends two owned games");
        expansions.Max(expansion => expansion.ExpansionOf.Max(reference => reference.Title.Length)).Should().BeGreaterThanOrEqualTo(58);

        var familySizes = ownedParents
            .Where(parents => parents.Count > 0)
            .GroupBy(parents => parents.Min(reference => reference.BggId))
            .Select(group => group.Count())
            .Order()
            .ToList();
        familySizes.Max().Should().BeGreaterThanOrEqualTo(8);
        familySizes.Should().Contain([1, 2, 9]);

        expansions
            .Where(expansion => ownedParents[expansions.IndexOf(expansion)].Count > 0)
            .Should().Contain(expansion => expansion.CollectionId < items.Single(item => item.BggId == expansion.ExpansionOf.Min(reference => reference.BggId)).CollectionId, "one expansion comes before its base game");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_sample_of_sixty_five_lays_out_every_shape_and_leaves_room_in_the_last_section()
    {
        SyntheticCollections.TryGetSample("65", out var items);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);

        var kinds = layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).Select(placement => placement.Kind).ToHashSet();
        kinds.Should().Contain(Enum.GetValues<PlacementKind>(), "the review sample shows every kind of placement");
        layout.Sections[^1].Cubbies.Should().Contain(cubby => cubby.Placements.Count == 0);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_sample_of_four_hundred_has_about_a_fifth_expansions_in_families_of_up_to_ten_and_some_orphans()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var expansions = items.Where(item => item.Kind == ItemKind.Expansion).ToList();

        expansions.Count.Should().BeInRange(55, 90);
        expansions.Count(expansion => LayoutAssertions.OwnedParentOf(expansion, items) is null).Should().BeInRange(5, 20);

        var biggest = expansions
            .Select(expansion => LayoutAssertions.OwnedParentOf(expansion, items))
            .OfType<int>()
            .GroupBy(id => id)
            .Max(group => group.Count());
        biggest.Should().BeInRange(6, 10);
    }

    [Theory]
    [InlineData("65")]
    [InlineData("400")]
    [Trait("Category", "Layout")]
    public void Base_game_titles_named_by_expansions_are_invented_digit_free_and_not_owned_when_absent(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var ownedTitles = items.Select(item => item.Title).ToHashSet();

        var references = items.SelectMany(item => item.ExpansionOf).ToList();

        references.Should().NotBeEmpty();
        references.Should().OnlyContain(reference => !reference.Title.Any(char.IsAsciiDigit));

        foreach (var reference in references.Where(reference => items.All(item => item.BggId != reference.BggId)))
        {
            ownedTitles.Should().NotContain(reference.Title, "a base game that is not owned has a title of its own");
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_random_collection_without_a_share_of_expansions_holds_base_games_only()
    {
        SyntheticCollections.Random(21, 100).Should().OnlyContain(item => item.Kind == ItemKind.Base && item.ExpansionOf.Count == 0);
        SyntheticCollections.Random(21, 100, 20).Count(item => item.Kind == ItemKind.Expansion).Should().BeInRange(10, 30);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_next_expansion_and_the_next_orphan_sort_after_everything_and_have_unused_titles()
    {
        var items = SyntheticCollections.Random(5, 50, 20);
        var parent = items.First(item => item.Kind == ItemKind.Base);

        var expansion = SyntheticCollections.NextExpansion(items, parent.BggId, 5);
        var orphan = SyntheticCollections.NextOrphanExpansion(items, 5);

        foreach (var next in new[] { expansion, orphan })
        {
            next.Kind.Should().Be(ItemKind.Expansion);
            next.BggId.Should().BeGreaterThan(items.Max(item => item.BggId));
            next.CollectionId.Should().BeGreaterThan(items.Max(item => item.CollectionId));
            items.Select(item => item.Title).Should().NotContain(next.Title);
            next.Title.Any(char.IsAsciiDigit).Should().BeFalse();
        }

        expansion.ExpansionOf.Should().Equal(new BaseGameRef(parent.BggId, parent.Title));
        items.Any(item => item.BggId == orphan.ExpansionOf[0].BggId).Should().BeFalse("the base game of an orphan is not owned");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_next_base_game_sorts_after_everything_and_has_an_unused_title()
    {
        var items = SyntheticCollections.Random(5, 50);

        var next = SyntheticCollections.NextBaseGame(items, 5);

        next.BggId.Should().BeGreaterThan(items.Max(item => item.BggId));
        next.CollectionId.Should().BeGreaterThan(items.Max(item => item.CollectionId));
        items.Select(item => item.Title).Should().NotContain(next.Title);
        next.Kind.Should().Be(ItemKind.Base);
    }
}
