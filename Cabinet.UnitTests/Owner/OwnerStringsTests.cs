using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that every kind of verdict has an English tag on the pictures page, so a kind added later cannot reach the owner
/// without words, and that the sub-lines of the tiles read as the owner tools promise.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerStringsTests
{
    public static TheoryData<ArtVerdictKind> EveryKind() => [.. Enum.GetValues<ArtVerdictKind>()];

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void Every_verdict_kind_has_a_non_empty_tag(ArtVerdictKind kind)
    {
        OwnerStrings.VerdictTag(kind).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void The_tags_read_as_the_five_phrases_and_no_two_are_the_same()
    {
        var tags = Enum.GetValues<ArtVerdictKind>().Select(OwnerStrings.VerdictTag).ToList();

        tags.Should().OnlyHaveUniqueItems();
        OwnerStrings.VerdictTag(ArtVerdictKind.Flat).Should().Be("Reads as a flat cover");
        OwnerStrings.VerdictTag(ArtVerdictKind.ThreeD).Should().Be("Reads as a 3D shot");
        OwnerStrings.VerdictTag(ArtVerdictKind.CutOut).Should().Be("Reads as a cut-out");
        OwnerStrings.VerdictTag(ArtVerdictKind.TightCrop).Should().Be("Reads as a tight crop");
        OwnerStrings.VerdictTag(ArtVerdictKind.Unsure).Should().Be("Shape unsure");
    }

    [Fact]
    public void A_kind_the_page_does_not_know_falls_back_to_shape_unsure()
    {
        OwnerStrings.VerdictTag((ArtVerdictKind)99).Should().Be("Shape unsure");
    }

    [Theory]
    [InlineData(AutomaticSource.OwnedEdition, "Same as Owned edition")]
    [InlineData(AutomaticSource.MainPicture, "Same as Main picture")]
    [InlineData(AutomaticSource.GeneratedCover, "Uses a generated cover")]
    public void The_automatic_tile_names_what_it_duplicates(AutomaticSource source, string expected)
    {
        OwnerStrings.AutomaticSubLine(source).Should().Be(expected);
    }

    [Fact]
    public void The_empty_pictures_page_says_no_games_yet_in_two_parts()
    {
        OwnerStrings.PicturesNoGamesHeading.Should().Be("No games yet.");
        OwnerStrings.PicturesNoGamesBody.Should().Be("Your games show up here after the first sync.");
    }

    [Fact]
    public void The_tile_words_match_the_copy_the_owner_reads()
    {
        OwnerStrings.TileBggHasNone.Should().Be("BGG has none");
        OwnerStrings.TileNoLinkYet.Should().Be("None yet");
        OwnerStrings.TileFetching.Should().Be("Fetching...");
        OwnerStrings.AutomaticsPick.Should().Be("Automatic's pick");
        OwnerStrings.TileLabel(PictureChoice.Automatic).Should().Be("Automatic");
        OwnerStrings.TileLabel(PictureChoice.OwnedEdition).Should().Be("Owned edition");
        OwnerStrings.TileLabel(PictureChoice.MainPicture).Should().Be("Main picture");
        OwnerStrings.TileLabel(PictureChoice.Link).Should().Be("Your link");
    }
}
