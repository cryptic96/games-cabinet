using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Verifies the allow-list of every look field, how a stored look is normalised one field at a time, and the edit that sets the look.</summary>
[Trait("Category", "Owner")]
public sealed class CabinetLooksTests
{
    [Fact]
    public void Each_field_allows_exactly_its_own_values_in_the_order_they_are_offered()
    {
        CabinetLook.Designs.Should().Equal("classic", "modern", "display", "legs", "library", "plywood", "plywood-legs");
        CabinetLook.Woods.Should().Equal("brown", "walnut", "oak", "birch", "mahogany");
        CabinetLook.BackChoices.Should().Equal("wood", "painted");
        CabinetLook.LightChoices.Should().Equal("off", "on", "evenings");
        CabinetLook.DepthChoices.Should().Equal("flat", "shaded", "deep");
    }

    [Fact]
    public void The_default_look_is_classic_warm_brown_wood_backs_lights_off_and_shaded()
    {
        CabinetLook.Default.Should().Be(new CabinetLook("classic", "brown", "wood", "off", "shaded"));
        OwnerData.Empty.Look.Should().Be(CabinetLook.Default);
        CabinetLook.Default.Normalized().Should().Be(CabinetLook.Default);
    }

    [Theory]
    [InlineData("sideways", "oak", "painted", "evenings", "deep", "classic", "oak", "painted", "evenings", "deep")]
    [InlineData("legs", "teak", "painted", "evenings", "deep", "legs", "brown", "painted", "evenings", "deep")]
    [InlineData("legs", "oak", "glossy", "evenings", "deep", "legs", "oak", "wood", "evenings", "deep")]
    [InlineData("legs", "oak", "painted", "sometimes", "deep", "legs", "oak", "painted", "off", "deep")]
    [InlineData("legs", "oak", "painted", "evenings", "bottomless", "legs", "oak", "painted", "evenings", "shaded")]
    [InlineData("LEGS", "oak", "painted", "evenings", "deep", "classic", "oak", "painted", "evenings", "deep")]
    [InlineData("legs", " oak", "painted", "evenings", "deep", "legs", "brown", "painted", "evenings", "deep")]
    [InlineData("legs", "oak\" onload=\"x", "painted", "evenings", "deep", "legs", "brown", "painted", "evenings", "deep")]
    public void An_unknown_value_changes_that_field_alone_to_its_default(
        string design,
        string wood,
        string backs,
        string lights,
        string depth,
        string expectedDesign,
        string expectedWood,
        string expectedBacks,
        string expectedLights,
        string expectedDepth)
    {
        new CabinetLook(design, wood, backs, lights, depth).Normalized()
            .Should().Be(new CabinetLook(expectedDesign, expectedWood, expectedBacks, expectedLights, expectedDepth));
    }

    [Fact]
    public void Fields_that_are_missing_normalise_to_their_defaults()
    {
        var missing = new CabinetLook(null!, null!, null!, null!, null!);

        missing.Normalized().Should().Be(CabinetLook.Default);
    }

    [Fact]
    public void Every_design_and_wood_pair_is_accepted_and_stored_as_chosen()
    {
        var pairs = 0;

        foreach (var design in CabinetLook.Designs)
        {
            foreach (var wood in CabinetLook.Woods)
            {
                var change = CabinetLooks.SetLook(OwnerData.Empty with { Look = new CabinetLook("classic", "brown", "painted", "on", "deep") }, design, wood, "painted", "on", "deep");

                pairs++;

                if (design == "classic" && wood == "brown")
                {
                    change.Changed.Should().BeFalse();
                    change.IsNone.Should().BeTrue();
                    continue;
                }

                change.Changed.Should().BeTrue($"{design} with {wood} is allowed");
                change.Data.Look.Should().Be(new CabinetLook(design, wood, "painted", "on", "deep"));
            }
        }

        pairs.Should().Be(35);
    }

    [Theory]
    [InlineData("sideways", "oak", "wood", "off", "shaded")]
    [InlineData("legs", "teak", "wood", "off", "shaded")]
    [InlineData("legs", "oak", "glossy", "off", "shaded")]
    [InlineData("legs", "oak", "wood", "dim", "shaded")]
    [InlineData("legs", "oak", "wood", "off", "bottomless")]
    [InlineData(null, "oak", "wood", "off", "shaded")]
    [InlineData("legs", null, "wood", "off", "shaded")]
    [InlineData("legs", "oak", null, "off", "shaded")]
    [InlineData("legs", "oak", "wood", null, "shaded")]
    [InlineData("legs", "oak", "wood", "off", null)]
    [InlineData("Legs", "oak", "wood", "off", "shaded")]
    public void A_look_with_any_value_that_is_not_allowed_is_refused_and_the_data_is_unchanged(string? design, string? wood, string? backs, string? lights, string? depth)
    {
        var change = CabinetLooks.SetLook(OwnerData.Empty, design, wood, backs, lights, depth);

        change.Changed.Should().BeFalse();
        change.IsNone.Should().BeFalse();
        change.StatusKey.Should().Be("unknown-look");
        change.Data.Should().BeSameAs(OwnerData.Empty);
    }

    [Fact]
    public void Setting_the_look_the_data_already_shows_changes_nothing()
    {
        CabinetLooks.SetLook(OwnerData.Empty, "classic", "brown", "wood", "off", "shaded").IsNone.Should().BeTrue();
    }

    [Fact]
    public void A_stored_look_with_an_unknown_field_counts_as_its_normalised_look_when_the_same_look_is_set()
    {
        var stored = OwnerData.Empty with { Look = new CabinetLook("classic", "teak", "wood", "off", "shaded") };

        CabinetLooks.SetLook(stored, "classic", "brown", "wood", "off", "shaded").IsNone.Should().BeTrue();
    }

    [Fact]
    public void A_new_look_is_stored_and_logged_with_the_words_of_the_controls()
    {
        var change = CabinetLooks.SetLook(OwnerData.Empty, "library", "mahogany", "painted", "evenings", "deep");

        change.Changed.Should().BeTrue();
        change.StatusKey.Should().Be("look-set");
        change.Data.Look.Should().Be(new CabinetLook("library", "mahogany", "painted", "evenings", "deep"));
        change.LogText.Should().Be("Cabinet look set to Library · Mahogany · Painted backs · Lights: Evenings only · Depth: Deep");
    }

    [Fact]
    public void The_default_look_is_summarised_with_wood_backs_lights_off_and_shaded()
    {
        CabinetLooks.Summary(CabinetLook.Default).Should().Be("Classic · Warm brown · Wood backs · Lights: Off · Depth: Shaded");
    }

    [Fact]
    public void The_summary_uses_the_display_names_of_every_design_wood_and_choice()
    {
        CabinetLooks.Summary(new CabinetLook("legs", "walnut", "wood", "on", "flat")).Should().Be("On legs · Walnut · Wood backs · Lights: Always on · Depth: Flat");
        CabinetLooks.Summary(new CabinetLook("plywood-legs", "birch", "painted", "off", "shaded")).Should().Be("Plywood on legs · Birch · Painted backs · Lights: Off · Depth: Shaded");
        CabinetLooks.Summary(new CabinetLook("sideways", "teak", "glossy", "dim", "bottomless")).Should().Be(CabinetLooks.Summary(CabinetLook.Default));
    }

    [Theory]
    [InlineData("birch", true)]
    [InlineData("walnut", true)]
    [InlineData("brown", false)]
    [InlineData("oak", false)]
    [InlineData("mahogany", false)]
    public void Birch_and_walnut_have_a_light_wall_and_the_other_woods_a_dark_one(string wood, bool light)
    {
        CabinetLook.HasLightWall(wood).Should().Be(light);
    }
}
