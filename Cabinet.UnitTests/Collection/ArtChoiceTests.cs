using Cabinet.Domain.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Pins the detector verdict and the choice between the owned edition's picture and the main picture.</summary>
public class ArtChoiceTests
{
    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_slanted_owned_edition_gives_way_to_a_flat_main_cover()
    {
        ArtChooser.Choose(ArtVerdict.ThreeD, ArtVerdict.Flat).Should().Be(ArtPick.MainImage);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_box_photographed_on_a_backdrop_is_a_3D_shot()
    {
        var features = new ArtFeatures(0.59, 0.78, 0.83, 0.72, 0);

        ArtVerdicts.Classify(features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_full_bleed_cover_is_flat()
    {
        var features = new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4);

        ArtVerdicts.Classify(features, ArtThresholds.Default).Should().Be(ArtVerdict.Flat);
    }
}
