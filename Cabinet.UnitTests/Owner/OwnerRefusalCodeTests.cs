using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves that a refusal a form post can come back with keeps its own code and its own words instead of the general failure.</summary>
[Trait("Category", "Owner")]
public sealed class OwnerRefusalCodeTests
{
    [Fact]
    public void A_delete_that_still_needs_confirming_keeps_its_own_code_and_words()
    {
        var code = OwnerResponses.DoneCode(new OwnerResult(OwnerOutcome.Refused, null, "confirm-needed"));

        OwnerResponses.NoticeFor(code).Should().Be("This location still holds games. Confirm the delete first.");
        OwnerResponses.IsProblem(code).Should().BeTrue();
    }

    [Fact]
    public void A_refusal_the_page_does_not_know_falls_back_to_the_general_failure()
    {
        var code = OwnerResponses.DoneCode(new OwnerResult(OwnerOutcome.Refused, null, "something-new"));

        OwnerResponses.NoticeFor(code).Should().Be("Couldn't save that. Try again.");
    }
}
