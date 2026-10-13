using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves the rules a location name has to meet before it is stored, and that each way of breaking them has its own reason.</summary>
[Trait("Category", "Owner")]
public sealed class LocationNamesTests
{
    private static readonly OwnerLocation[] Existing = [new("a1", "Shelf B"), new("a2", "Cabinet A")];

    [Fact]
    public void A_name_is_trimmed_before_it_is_stored()
    {
        var check = LocationNames.Validate(Existing, "  Shelf C  ");
        var added = LocationEdits.Add(OwnerData.Empty, "  Shelf B  ", NewId);

        check.Problem.Should().Be(LocationNameProblem.None);
        check.Name.Should().Be("Shelf C");
        added.Data.Locations.Should().ContainSingle().Which.Name.Should().Be("Shelf B");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void An_empty_or_blank_name_is_refused_as_empty(string candidate)
    {
        LocationNames.Validate(Existing, candidate).Problem.Should().Be(LocationNameProblem.Empty);
    }

    [Fact]
    public void A_name_of_sixty_characters_is_accepted_and_one_of_sixty_one_is_refused()
    {
        LocationNames.Validate([], new string('x', 60)).Problem.Should().Be(LocationNameProblem.None);
        LocationNames.Validate([], new string('x', 61)).Problem.Should().Be(LocationNameProblem.TooLong);
    }

    [Fact]
    public void The_length_is_counted_after_trimming()
    {
        LocationNames.Validate([], "  " + new string('x', 60) + "  ").Problem.Should().Be(LocationNameProblem.None);
    }

    [Fact]
    public void A_fifty_first_location_is_refused()
    {
        var fifty = Enumerable.Range(1, 50).Select(index => new OwnerLocation($"id{index}", $"Spot {index}")).ToList();

        LocationNames.Validate(fifty.Take(49).ToList(), "One more").Problem.Should().Be(LocationNameProblem.None);
        LocationNames.Validate(fifty, "One more").Problem.Should().Be(LocationNameProblem.TooMany);
    }

    [Theory]
    [InlineData("shelf b")]
    [InlineData("SHELF B")]
    [InlineData(" Shelf B ")]
    public void A_name_equal_to_an_existing_one_ignoring_case_and_edge_spaces_is_refused_as_taken(string candidate)
    {
        LocationNames.Validate(Existing, candidate).Problem.Should().Be(LocationNameProblem.Taken);
    }

    [Fact]
    public void A_location_may_keep_its_own_name_when_it_is_the_one_being_renamed()
    {
        LocationNames.Validate(Existing, "shelf b", exceptId: "a1").Problem.Should().Be(LocationNameProblem.None);
        LocationNames.Validate(Existing, "cabinet a", exceptId: "a1").Problem.Should().Be(LocationNameProblem.Taken);
    }

    [Theory]
    [InlineData("Shelf\u0007B")]
    [InlineData("Shelf‮B")]
    [InlineData("Shelf⁦B")]
    [InlineData("Shelf‪B")]
    [InlineData("Shelf⁩B")]
    [InlineData("Shelf\tB")]
    [InlineData("Shelf\nB")]
    [InlineData("Shelf\u0085B")]
    [InlineData("Shelf\u200EB")]
    [InlineData("Shelf\u200FB")]
    [InlineData("Shelf\u061CB")]
    [InlineData("Shelf\u200BB")]
    [InlineData("Shelf\u200CB")]
    [InlineData("Shelf\u200DB")]
    [InlineData("Shelf\u2060B")]
    [InlineData("Shelf\uFEFFB")]
    [InlineData("Shelf\u00ADB")]
    [InlineData("Shelf\U000E0041B")]
    public void A_name_holding_a_control_or_invisible_format_character_is_refused_with_its_own_reason(string candidate)
    {
        LocationNames.Validate(Existing, candidate).Problem.Should().Be(LocationNameProblem.ForbiddenCharacter);
    }

    [Theory]
    [InlineData("Étagère du salon")]
    [InlineData("Plank אב 3")]
    [InlineData("Kast 2 / boven")]
    public void Ordinary_letters_from_any_script_and_punctuation_are_accepted(string candidate)
    {
        LocationNames.Validate(Existing, candidate).Problem.Should().Be(LocationNameProblem.None);
    }

    [Theory]
    [InlineData("", "name-empty")]
    [InlineData("Shelf‮B", "name-characters")]
    [InlineData("shelf b", "name-taken")]
    public void Adding_a_bad_name_is_refused_with_a_status_key_and_changes_nothing(string candidate, string statusKey)
    {
        var data = OwnerData.Empty with { Locations = [new OwnerLocation("a1", "Shelf B")] };

        var refused = LocationEdits.Add(data, candidate, NewId);

        refused.Changed.Should().BeFalse();
        refused.StatusKey.Should().Be(statusKey);
        refused.Data.Should().BeSameAs(data);
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
