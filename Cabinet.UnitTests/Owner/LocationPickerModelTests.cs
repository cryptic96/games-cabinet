using Cabinet.Domain.Owner;
using Cabinet.Service.Pages.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves the choices the shared location picker offers for each kind of game.</summary>
[Trait("Category", "Owner")]
public sealed class LocationPickerModelTests
{
    private static readonly OwnerLocation[] Locations = [new("loc1", "Cabinet A"), new("loc2", "Shelf B")];

    [Fact]
    public void A_base_game_offers_Not_placed_first_then_the_locations_in_order()
    {
        var model = LocationPickerModel.For(Row(1, "Game 2"), Locations, 7);

        model.Options.Select(option => option.Text).Should().Equal("Not placed", "Cabinet A", "Shelf B");
        model.Options.Select(option => option.Value).Should().Equal(string.Empty, "loc1", "loc2");
        model.Options.Single(option => option.Selected).Value.Should().BeEmpty();
        model.Revision.Should().Be(7);
    }

    [Fact]
    public void An_expansion_with_an_owned_base_offers_Same_as_the_base_instead_of_Not_placed()
    {
        var model = LocationPickerModel.For(Row(2, "Game 2: Extra Pack", baseId: 1, baseTitle: "Game 2"), Locations, 1);

        model.Options.Select(option => option.Text).Should().Equal("Same as Game 2", "Cabinet A", "Shelf B");
        model.Options.Should().NotContain(option => option.Text == "Not placed");
        model.Options.Single(option => option.Selected).Text.Should().Be("Same as Game 2");
    }

    [Fact]
    public void An_expansion_without_an_owned_base_offers_Not_placed()
    {
        var model = LocationPickerModel.For(Row(3, "Orphan Pack", isExpansion: true), Locations, 1);

        model.Options.Select(option => option.Text).Should().Equal("Not placed", "Cabinet A", "Shelf B");
    }

    [Fact]
    public void The_game_s_own_location_is_the_selected_choice()
    {
        var model = LocationPickerModel.For(Row(1, "Game 2", own: "loc2"), Locations, 1);

        model.Options.Single(option => option.Selected).Text.Should().Be("Shelf B");
    }

    [Fact]
    public void With_no_locations_the_only_choice_is_Not_placed()
    {
        var model = LocationPickerModel.For(Row(1, "Game 2"), [], 1);

        model.Options.Select(option => option.Text).Should().Equal("Not placed");
    }

    [Fact]
    public void A_visible_label_is_carried_only_when_asked_for()
    {
        LocationPickerModel.For(Row(1, "Game 2"), Locations, 1).VisibleLabel.Should().BeNull();
        LocationPickerModel.For(Row(1, "Game 2"), Locations, 1, "Location").VisibleLabel.Should().Be("Location");
    }

    private static LocationGameRow Row(
        int gameId,
        string title,
        int? baseId = null,
        string? baseTitle = null,
        bool isExpansion = false,
        string? own = null) =>
        new(gameId, title, isExpansion || baseId is not null, own, baseTitle, null, baseId);
}
