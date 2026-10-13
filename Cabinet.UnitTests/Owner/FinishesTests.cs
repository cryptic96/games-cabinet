using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Verifies the allowed finishes, how a stored value is normalised and the edit that sets the finish.</summary>
public sealed class FinishesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("walnut")]
    [InlineData("CLASSIC")]
    [InlineData(" classic")]
    [InlineData("classic\" onload=\"x")]
    public void A_value_that_is_not_exactly_an_allowed_finish_normalises_to_classic(string? stored)
    {
        Finishes.Normalize(stored).Should().Be("classic");
    }

    [Fact]
    public void Classic_is_the_only_allowed_finish_and_it_is_the_default()
    {
        Finishes.Allowed.Should().Equal("classic");
        OwnerData.DefaultFinish.Should().Be("classic");
        Finishes.Normalize("classic").Should().Be("classic");
    }

    [Fact]
    public void Setting_the_finish_the_data_already_has_changes_nothing()
    {
        var change = Finishes.SetFinish(OwnerData.Empty, "classic");

        change.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Setting_a_finish_that_is_not_allowed_is_refused()
    {
        var change = Finishes.SetFinish(OwnerData.Empty, "walnut");

        change.Changed.Should().BeFalse();
        change.IsNone.Should().BeFalse();
        change.StatusKey.Should().Be("unknown-finish");
        change.Data.Should().BeSameAs(OwnerData.Empty);
    }

    [Fact]
    public void Setting_an_allowed_finish_that_differs_from_the_stored_value_stores_it_and_logs_it()
    {
        var stored = OwnerData.Empty with { Finish = "walnut" };

        var change = Finishes.SetFinish(stored, "classic");

        change.Changed.Should().BeTrue();
        change.Data.Finish.Should().Be("classic");
        change.LogText.Should().Be("Cabinet finish: Classic");
    }
}
