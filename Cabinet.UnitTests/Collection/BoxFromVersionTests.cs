using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves a reported version becomes a believable box and anything else becomes the default for its kind.</summary>
[Trait("Category", "Snapshot")]
public class BoxFromVersionTests
{
    private static readonly BoxDimensions BaseDefault = new(225, 300, 60);
    private static readonly BoxDimensions ExpansionDefault = new(200, 260, 40);

    [Fact]
    public void The_factor_is_inches_to_millimetres()
    {
        BoxFromVersion.MillimetresPerUnit.Should().Be(25.4);
    }

    [Fact]
    public void A_plausible_triple_converts_with_the_factor_and_the_longer_side_is_the_height()
    {
        var box = BoxFromVersion.Map(new VersionDimensions(9.5, 11.75, 3.1), ItemKind.Base);

        box.Should().Be(new BoxDimensions(241, 298, 79));
    }

    [Fact]
    public void The_longer_front_side_is_the_height_even_when_the_source_lists_it_as_the_width()
    {
        var box = BoxFromVersion.Map(new VersionDimensions(11.75, 9.5, 3.1), ItemKind.Base);

        box.Should().Be(new BoxDimensions(241, 298, 79));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(6.3, 0, 2.09)]
    [InlineData(-6.3, 8.27, 2.09)]
    [InlineData(double.NaN, 8.27, 2.09)]
    [InlineData(6.3, 8.27, double.PositiveInfinity)]
    public void A_missing_zero_or_negative_part_gives_the_default(double width, double length, double depth)
    {
        BoxFromVersion.Map(new VersionDimensions(width, length, depth), ItemKind.Base).Should().Be(BaseDefault);
        BoxFromVersion.Map(new VersionDimensions(width, length, depth), ItemKind.Expansion).Should().Be(ExpansionDefault);
    }

    [Theory]
    [InlineData(1.0, 1.5, 0.5)]
    [InlineData(40, 60, 3)]
    [InlineData(6.3, 8.27, 0.1)]
    [InlineData(6.3, 8.27, 12)]
    public void An_implausible_triple_gives_the_default(double width, double length, double depth)
    {
        BoxFromVersion.Map(new VersionDimensions(width, length, depth), ItemKind.Base).Should().Be(BaseDefault);
    }

    [Fact]
    public void Dimensions_at_the_edge_of_the_believable_range_are_accepted()
    {
        var smallest = BoxFromVersion.Map(
            new VersionDimensions(BoxFromVersion.MinFrontMm / BoxFromVersion.MillimetresPerUnit, 2.1, BoxFromVersion.MinDepthMm / BoxFromVersion.MillimetresPerUnit),
            ItemKind.Base);

        smallest.WidthMm.Should().Be(BoxFromVersion.MinFrontMm);
        smallest.DepthMm.Should().Be(BoxFromVersion.MinDepthMm);
    }

    [Fact]
    public void No_version_gives_the_default_for_the_kind()
    {
        BoxFromVersion.Map(null, ItemKind.Base).Should().Be(BaseDefault);
        BoxFromVersion.Map(null, ItemKind.Expansion).Should().Be(ExpansionDefault);
    }

    [Fact]
    public void Try_map_gives_the_same_box_as_map_when_it_is_believable_and_nothing_otherwise()
    {
        var believable = new VersionDimensions(9.5, 11.75, 3.1);

        BoxFromVersion.TryMap(believable).Should().Be(BoxFromVersion.Map(believable, ItemKind.Base));
        BoxFromVersion.TryMap(null).Should().BeNull();
        BoxFromVersion.TryMap(new VersionDimensions(0, 0, 0)).Should().BeNull();
        BoxFromVersion.TryMap(new VersionDimensions(40, 60, 3)).Should().BeNull();
    }
}
