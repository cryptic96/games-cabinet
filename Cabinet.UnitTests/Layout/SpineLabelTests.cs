using System.Globalization;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins how titles are shortened for the text drawn on spines and flat boxes.</summary>
public class SpineLabelTests
{
    private const string Ellipsis = "…";

    [Fact]
    [Trait("Category", "Layout")]
    public void A_colon_cuts_the_title_to_the_text_before_it()
    {
        SpineLabel.Shorten("Vossmere: The Ash Accord", 40).Should().Be("Vossmere");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_spaced_dash_cuts_the_title_to_the_text_before_it()
    {
        SpineLabel.Shorten("Tarnwyn - Brindle Reborn", 40).Should().Be("Tarnwyn");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_earlier_of_a_colon_and_a_dash_decides_the_cut()
    {
        SpineLabel.Shorten("Brindle - Kelmont: Voss", 40).Should().Be("Brindle");
        SpineLabel.Shorten("Brindle: Kelmont - Voss", 40).Should().Be("Brindle");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_title_that_starts_with_a_colon_keeps_its_text()
    {
        SpineLabel.Shorten(": The Ash Accord", 40).Should().Be(": The Ash Accord");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_dash_without_spaces_does_not_cut()
    {
        SpineLabel.Shorten("Tarn-wyn Brindle", 40).Should().Be("Tarn-wyn Brindle");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_title_longer_than_the_budget_keeps_budget_minus_one_text_elements_and_an_ellipsis()
    {
        SpineLabel.Shorten("Zimdrelsulorvpelltavgrov", 10).Should().Be("Zimdrelsu" + Ellipsis);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_title_that_fits_is_returned_unchanged_apart_from_trimming()
    {
        SpineLabel.Shorten("  Pell Tav Grov  ", 17).Should().Be("Pell Tav Grov");
        SpineLabel.Shorten("Pell Tav Grov", 13).Should().Be("Pell Tav Grov");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_title_is_never_cut_inside_an_emoji_or_a_combining_sequence()
    {
        var emoji = SpineLabel.Shorten("Voss \U0001F3B2\U0001F3B2\U0001F3B2 Tarn", 7);
        var combining = SpineLabel.Shorten("Orvä Kelmont", 5);

        emoji.Should().Be("Voss \U0001F3B2" + Ellipsis);
        combining.Should().Be("Orvä" + Ellipsis);
        IsWholeTextElements(emoji).Should().BeTrue();
        IsWholeTextElements(combining).Should().BeTrue();
    }

    [Theory]
    [InlineData("Keeper of the Ash Accord", 14, "Keeper" + Ellipsis)]
    [InlineData("Rise of the Dragon Court", 13, "Rise" + Ellipsis)]
    [InlineData("Tides and Embers of Vell", 11, "Tides" + Ellipsis)]
    [InlineData("Keeper of the Ash Accord", 18, "Keeper of the Ash" + Ellipsis)]
    [Trait("Category", "Layout")]
    public void A_shortened_label_never_ends_on_a_stop_word(string title, int budget, string expected)
    {
        SpineLabel.Shorten(title, budget).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Stop_words_are_trimmed_one_after_another_and_a_separator_left_behind_goes_too()
    {
        SpineLabel.Shorten("Wardens of the, Ash Accord", 17).Should().Be("Wardens" + Ellipsis);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_title_that_fits_keeps_its_last_stop_word()
    {
        SpineLabel.Shorten("Heart of the", 20).Should().Be("Heart of the");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_title_made_only_of_stop_words_keeps_its_cut()
    {
        SpineLabel.Shorten("Of the And In", 8).Should().Be("Of the " + Ellipsis);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    [Trait("Category", "Layout")]
    public void A_blank_title_gives_an_empty_label(string title)
    {
        SpineLabel.Shorten(title, 20).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    [Trait("Category", "Layout")]
    public void The_budget_never_goes_below_the_minimum(int budget)
    {
        SpineLabel.MinTextElements.Should().Be(3);
        SpineLabel.Shorten("Zimdrelsulorv", budget).Should().Be("Zi" + Ellipsis);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void In_the_edge_sample_every_spine_and_flat_label_is_a_shortened_form_of_its_untouched_title()
    {
        SyntheticCollections.TryGetSample("edge", out var items).Should().BeTrue();

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);
        var placements = layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).ToList();
        var titles = items.ToDictionary(item => item.BggId, item => item.Title);

        placements.Should().HaveCount(items.Count);
        placements.Select(placement => placement.Kind).Distinct().Should().Contain(PlacementKind.Spine);

        foreach (var placement in placements)
        {
            placement.Title.Should().Be(titles[placement.GameId]);

            if (placement.Kind == PlacementKind.Cover)
            {
                placement.Label.Should().Be(placement.Title);
            }
            else
            {
                IsShortenedForm(placement.Label, placement.Title).Should().BeTrue(
                    "label '{0}' must come from title '{1}'",
                    placement.Label,
                    placement.Title);
            }
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_desktop_design_counts_one_label_character_per_fourteen_millimetres()
    {
        SectionDesigns.Desktop.LabelCharPitchMm.Should().Be(14);
    }

    private static bool IsShortenedForm(string label, string title)
    {
        var trimmed = title.Trim();
        var body = label.EndsWith(Ellipsis, StringComparison.Ordinal) ? label[..^Ellipsis.Length] : label;

        return trimmed.StartsWith(body, StringComparison.Ordinal);
    }

    private static bool IsWholeTextElements(string text)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        var rebuilt = string.Empty;

        while (elements.MoveNext())
        {
            rebuilt += elements.GetTextElement();
        }

        return rebuilt == text && !char.IsLowSurrogate(text[0]) && !char.IsHighSurrogate(text[^1]);
    }
}
