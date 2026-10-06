using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins how many expansion layers a stack draws and how many it hands to the marker.</summary>
public class StackLayoutTests
{
    private const int CubbyHeightMm = 300;
    private const int MarkerHeightMm = 40;

    [Fact]
    [Trait("Category", "Layout")]
    public void A_family_with_no_expansions_draws_no_layers_and_no_marker()
    {
        var result = StackLayout.Layout([], CubbyHeightMm, MarkerHeightMm, 6);

        result.Should().Be(new StackResult(0, 0));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Layers_that_all_fit_are_all_drawn_and_need_no_marker()
    {
        var result = StackLayout.Layout([50, 50, 50], CubbyHeightMm, MarkerHeightMm, 6);

        result.Should().Be(new StackResult(3, 0));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Layers_that_exactly_fill_the_cubby_are_all_drawn_without_a_marker()
    {
        var result = StackLayout.Layout([60, 60, 60, 60, 60], CubbyHeightMm, MarkerHeightMm, 6);

        result.Should().Be(new StackResult(5, 0));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_stack_never_draws_more_layers_than_the_setting_allows()
    {
        var result = StackLayout.Layout([40, 40, 40, 40, 40], 400, MarkerHeightMm, 2);

        result.Should().Be(new StackResult(2, 3));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_stack_taller_than_the_cubby_keeps_room_for_the_marker()
    {
        var result = StackLayout.Layout(Enumerable.Repeat(50, 10).ToList(), 260, MarkerHeightMm, 20);

        result.Should().Be(new StackResult(4, 6));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void When_the_last_layer_only_fits_without_the_marker_it_is_hidden_once_more_arrive()
    {
        StackLayout.Layout([100, 100, 60], 260, MarkerHeightMm, 6).Should().Be(new StackResult(3, 0));
        StackLayout.Layout([100, 100, 61], 260, MarkerHeightMm, 6).Should().Be(new StackResult(2, 1));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_cubby_too_low_for_even_one_layer_and_the_marker_hides_every_expansion()
    {
        var result = StackLayout.Layout([20, 20], 30, MarkerHeightMm, 6);

        result.Should().Be(new StackResult(0, 2));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_negative_maximum_is_rejected()
    {
        var act = () => StackLayout.Layout([50], CubbyHeightMm, MarkerHeightMm, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(40)]
    [InlineData(55)]
    [InlineData(70)]
    [Trait("Category", "Layout")]
    public void Once_the_marker_shows_more_expansions_only_raise_the_hidden_count(int layerHeightMm)
    {
        var previousVisible = int.MaxValue;
        var hiddenSeen = false;

        for (var count = 1; count <= 20; count++)
        {
            var heights = Enumerable.Repeat(layerHeightMm, count).ToList();

            var result = StackLayout.Layout(heights, CubbyHeightMm, MarkerHeightMm, 20);

            (result.Visible + result.Hidden).Should().Be(count);

            if (hiddenSeen)
            {
                result.Visible.Should().Be(previousVisible, "count {0} only raises the hidden number", count);
            }

            if (result.Hidden > 0)
            {
                hiddenSeen = true;
                (result.Visible * layerHeightMm + MarkerHeightMm).Should().BeLessThanOrEqualTo(CubbyHeightMm);
            }
            else
            {
                (result.Visible * layerHeightMm).Should().BeLessThanOrEqualTo(CubbyHeightMm);
            }

            previousVisible = result.Visible;
        }

        hiddenSeen.Should().BeTrue("twenty layers cannot fit a cubby of three hundred millimetres");
    }
}
