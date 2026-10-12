using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves that every way a pasted picture address can go wrong has its own words, a key a redirect can carry, and no echo of what was pasted.</summary>
[Trait("Category", "Owner")]
public sealed class LinkErrorTests
{
    private const string Host = "img.example.org";

    [Theory]
    [InlineData(LinkProblem.Blank, "Paste a picture link first.")]
    [InlineData(LinkProblem.NotAWebAddress, "That isn't a web address. Paste the full picture link, starting with https://.")]
    [InlineData(LinkProblem.PageLink, "That's a page link, not a picture link. In BGG's gallery, open the picture and choose \"Copy image address\", then paste that here.")]
    [InlineData(LinkProblem.OtherHost, "That link isn't from BGG's picture host. Only links from img.example.org work here. Copy the picture's address from BGG's gallery.")]
    [InlineData(LinkProblem.FetchFailed, "Couldn't fetch that picture. The link may be wrong or the picture host may be busy. Check the link and try again.")]
    [InlineData(LinkProblem.NotFound, "That picture isn't there. Check that you copied the whole link.")]
    [InlineData(LinkProblem.NotAPicture, "That isn't a picture the cabinet can read. Try another one.")]
    [InlineData(LinkProblem.TooManyPixels, "That picture has too many pixels to process. Pick a smaller version from the gallery.")]
    [InlineData(LinkProblem.ExtremeShape, "That picture is very long and thin, so it can't be a box front. Try another one.")]
    public void Each_problem_has_its_own_words(LinkProblem problem, string expected)
    {
        OwnerStrings.LinkError(problem, Host, null, null).Should().Be(expected);
    }

    [Fact]
    public void A_picture_that_is_too_large_names_its_size_and_the_limit_in_megabytes()
    {
        OwnerStrings.LinkError(LinkProblem.TooLarge, Host, 5_400_000, 12_582_912)
            .Should().Be("That picture is too large to use (5.4 MB; the limit is 12.6 MB). Pick a smaller version from the gallery.");
    }

    [Fact]
    public void A_picture_that_is_too_large_without_an_announced_size_still_says_so()
    {
        OwnerStrings.LinkError(LinkProblem.TooLarge, Host, null, 12_582_912)
            .Should().Be("That picture is too large to use (the limit is 12.6 MB). Pick a smaller version from the gallery.");
    }

    [Theory]
    [InlineData(5_400_000, "5.4")]
    [InlineData(1_000_000, "1")]
    [InlineData(12_582_912, "12.6")]
    [InlineData(0, "0")]
    public void Megabytes_are_millions_of_bytes_with_at_most_one_decimal(long bytes, string expected)
    {
        OwnerStrings.Megabytes(bytes).Should().Be(expected);
    }

    [Fact]
    public void Every_problem_has_a_status_key_that_leads_back_to_the_same_problem_and_a_message_of_the_same_words()
    {
        foreach (var problem in Enum.GetValues<LinkProblem>())
        {
            var key = OwnerStrings.LinkKey(problem);

            OwnerStrings.LinkProblemOf(key).Should().Be(problem, key);
            OwnerStrings.Message(key).Should().Be(OwnerStrings.LinkError(problem, OwnerStrings.AnyPictureHost, null, null), key);
        }

        Enum.GetValues<LinkProblem>().Select(OwnerStrings.LinkKey).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("link-")]
    [InlineData("link-expired")]
    [InlineData("name-empty")]
    [InlineData("LINK-PAGE")]
    public void A_text_that_is_not_the_key_of_a_problem_is_not_a_problem(string? key)
    {
        OwnerStrings.LinkProblemOf(key).Should().BeNull();
    }

    [Fact]
    public void The_message_of_a_refused_link_never_contains_what_the_owner_pasted()
    {
        const string pasted = "https://evil.example.net/<script>alert(1)</script>.png";

        foreach (var problem in Enum.GetValues<LinkProblem>())
        {
            OwnerStrings.LinkError(problem, Host, 1, 2).Should().NotContain(pasted).And.NotContain("<");
        }
    }
}
