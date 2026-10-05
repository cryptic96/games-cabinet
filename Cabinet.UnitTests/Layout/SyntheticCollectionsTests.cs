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
