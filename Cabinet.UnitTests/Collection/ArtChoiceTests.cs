using Cabinet.Domain.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Pins the detector verdict and the choice between the owned edition's picture and the main picture.</summary>
public class ArtChoiceTests
{
    private const double WhiteGreyScoreFloor = 0.8;
    private const double NearBlackScoreFloor = 0.6;

    private static readonly ArtThresholds Defaults = ArtThresholds.Default;

    public static TheoryData<string, double, double, double, double, int, ArtVerdict> Fixtures => new()
    {
        { "flat cover, full bleed", 0.00, 1.00, 0.00, 0.00, 4, ArtVerdict.Flat },
        { "flat wide banner", 0.00, 1.00, 0.00, 0.00, 4, ArtVerdict.Flat },
        { "flat cover in thick white frame", 0.35, 1.00, 0.00, 0.00, 0, ArtVerdict.Flat },
        { "flat cover in thin black frame", 0.07, 0.99, 0.07, 0.07, 2, ArtVerdict.Flat },
        { "all-white cover", 0.99, 1.00, 0.00, 0.00, 0, ArtVerdict.Flat },
        { "3D box on white", 0.59, 0.78, 0.83, 0.72, 0, ArtVerdict.ThreeD },
        { "3D box on grey gradient", 0.61, 0.80, 0.77, 0.69, 0, ArtVerdict.ThreeD },
        { "3D box on near black", 0.65, 0.88, 0.69, 0.58, 0, ArtVerdict.ThreeD },
        { "3D box, transparent PNG", 0.65, 0.88, 0.69, 0.58, 0, ArtVerdict.ThreeD },
    };

    public static TheoryData<ArtVerdict?, ArtVerdict?, ArtPick> ChooserTable => new()
    {
        { ArtVerdict.Flat, ArtVerdict.Flat, ArtPick.VersionImage },
        { ArtVerdict.Flat, ArtVerdict.Unsure, ArtPick.VersionImage },
        { ArtVerdict.Flat, ArtVerdict.ThreeD, ArtPick.VersionImage },
        { ArtVerdict.Flat, null, ArtPick.VersionImage },
        { ArtVerdict.Unsure, ArtVerdict.Flat, ArtPick.MainImage },
        { ArtVerdict.Unsure, ArtVerdict.Unsure, ArtPick.VersionImage },
        { ArtVerdict.Unsure, ArtVerdict.ThreeD, ArtPick.VersionImage },
        { ArtVerdict.Unsure, null, ArtPick.VersionImage },
        { ArtVerdict.ThreeD, ArtVerdict.Flat, ArtPick.MainImage },
        { ArtVerdict.ThreeD, ArtVerdict.Unsure, ArtPick.VersionImage },
        { ArtVerdict.ThreeD, ArtVerdict.ThreeD, ArtPick.VersionImage },
        { ArtVerdict.ThreeD, null, ArtPick.VersionImage },
        { null, ArtVerdict.Flat, ArtPick.MainImage },
        { null, ArtVerdict.Unsure, ArtPick.MainImage },
        { null, ArtVerdict.ThreeD, ArtPick.MainImage },
        { null, null, ArtPick.GeneratedCover },
    };

    public static IEnumerable<object?[]> EveryCandidateState =>
    [[ArtVerdict.Flat], [ArtVerdict.Unsure], [ArtVerdict.ThreeD], [null]];

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

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.ThreeD);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_full_bleed_cover_is_flat()
    {
        var features = new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4);

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.Flat);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    [Trait("Category", "Enrichment")]
    public void Every_fixture_picture_classifies_as_expected_with_the_default_thresholds(
        string name,
        double backdrop,
        double fill,
        double corner1,
        double corner2,
        int sides,
        ArtVerdict expected)
    {
        ArtVerdicts.Classify(new ArtFeatures(backdrop, fill, corner1, corner2, sides), Defaults).Should().Be(expected, name);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_fill_and_corner_exactly_at_the_flat_thresholds_count_as_flat()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.1, 0.97, 0.15, 0.15, 0), Defaults).Should().Be(ArtVerdict.Flat);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_fill_just_under_the_flat_threshold_is_not_flat()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.1, 0.9699, 0.0, 0.0, 0), Defaults).Should().NotBe(ArtVerdict.Flat);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_corner_just_over_the_flat_threshold_is_not_flat()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.1, 1.0, 0.1501, 0.0, 0), Defaults).Should().NotBe(ArtVerdict.Flat);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_fill_and_corner_exactly_at_the_3D_thresholds_count_as_a_3D_shot()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.5, 0.93, 0.9, 0.40, 0), Defaults).Should().Be(ArtVerdict.ThreeD);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_fill_just_over_the_3D_threshold_is_unsure()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.5, 0.9301, 0.9, 0.9, 0), Defaults).Should().Be(ArtVerdict.Unsure);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_second_corner_just_under_the_3D_threshold_is_unsure()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.5, 0.9, 0.9, 0.3999, 0), Defaults).Should().Be(ArtVerdict.Unsure);
    }

    [Theory]
    [InlineData(0.0, 0.5, 0.9, 0.9)]
    [InlineData(0.0, 1.0, 0.0, 0.0)]
    [InlineData(0.5, 0.2, 1.0, 1.0)]
    [Trait("Category", "Enrichment")]
    public void A_picture_that_is_almost_all_backdrop_is_flat_whatever_else_was_measured(double ignored, double fill, double corner1, double corner2)
    {
        ArtVerdicts.Classify(new ArtFeatures(0.97, fill, corner1, corner2, 0), Defaults).Should().Be(ArtVerdict.Flat, ignored.ToString());
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_cut_out_that_fills_its_frame_is_a_3D_shot_and_scores_one()
    {
        var features = new ArtFeatures(0.13, 1.0, 0.0, 0.0, 0, CutOut: true);

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.ThreeD);
        ArtVerdicts.Score(features).Should().Be(1.0);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_box_photographed_close_is_a_3D_shot_and_scores_one()
    {
        var features = new ArtFeatures(0.06, 0.94, 0.31, 0.26, 4, CutOut: false, TightCrop: true);

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.ThreeD);
        ArtVerdicts.Score(features).Should().Be(1.0);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_close_crop_that_is_almost_all_backdrop_is_still_flat()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.97, 1.0, 0.0, 0.0, 4, TightCrop: true), Defaults).Should().Be(ArtVerdict.Flat);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_cut_out_that_is_almost_all_backdrop_is_still_flat()
    {
        ArtVerdicts.Classify(new ArtFeatures(0.97, 1.0, 0.0, 0.0, 0, CutOut: true), Defaults).Should().Be(ArtVerdict.Flat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [Trait("Category", "Enrichment")]
    public void Features_that_are_not_a_cut_out_classify_and_score_as_before(bool? cutOut)
    {
        var features = new ArtFeatures(0.13, 1.0, 0.0, 0.0, 0, cutOut, cutOut);

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.Flat);
        ArtVerdicts.Score(features).Should().Be(0.0);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_stricter_flat_fill_turns_a_near_flat_picture_into_unsure()
    {
        var features = new ArtFeatures(0.1, 0.98, 0.0, 0.0, 0);
        var stricter = Defaults with { FlatMinFill = 0.99 };

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.Flat);
        ArtVerdicts.Classify(features, stricter).Should().Be(ArtVerdict.Unsure);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_looser_3D_corner_turns_an_unsure_picture_into_a_3D_shot()
    {
        var features = new ArtFeatures(0.5, 0.9, 0.9, 0.3, 0);
        var looser = Defaults with { ThreeDMinCorner = 0.25 };

        ArtVerdicts.Classify(features, Defaults).Should().Be(ArtVerdict.Unsure);
        ArtVerdicts.Classify(features, looser).Should().Be(ArtVerdict.ThreeD);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void A_full_flat_picture_scores_zero()
    {
        ArtVerdicts.Score(new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4)).Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.59, 0.78, 0.72, WhiteGreyScoreFloor)]
    [InlineData(0.61, 0.80, 0.69, WhiteGreyScoreFloor)]
    [InlineData(0.65, 0.88, 0.58, NearBlackScoreFloor)]
    [Trait("Category", "Enrichment")]
    public void A_3D_fixture_scores_high(double backdrop, double fill, double corner2, double floor)
    {
        ArtVerdicts.Score(new ArtFeatures(backdrop, fill, 0.8, corner2, 0)).Should().BeGreaterThanOrEqualTo(floor);
    }

    [Theory]
    [InlineData(-5.0, 9.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(2.0, 5.0)]
    [Trait("Category", "Enrichment")]
    public void The_score_never_leaves_zero_to_one(double fill, double corner2)
    {
        ArtVerdicts.Score(new ArtFeatures(0.5, fill, 0.5, corner2, 0)).Should().BeInRange(0.0, 1.0);
    }

    [Theory]
    [MemberData(nameof(ChooserTable))]
    [Trait("Category", "Enrichment")]
    public void The_chooser_follows_its_table_for_every_combination(ArtVerdict? version, ArtVerdict? main, ArtPick expected)
    {
        ArtChooser.Choose(version, main).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EveryCandidateState))]
    [Trait("Category", "Enrichment")]
    public void The_same_verdict_for_both_candidates_never_gives_the_main_image(ArtVerdict? verdict)
    {
        ArtChooser.Choose(verdict, verdict).Should().NotBe(ArtPick.MainImage);
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void The_default_thresholds_are_the_starting_values()
    {
        Defaults.Should().Be(new ArtThresholds(0.97, 0.15, 0.93, 0.40, 0.97));
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void The_default_fingerprint_is_stable_invariant_text()
    {
        Defaults.Fingerprint.Should().Be("0.97|0.15|0.93|0.4|0.97");
    }

    [Fact]
    [Trait("Category", "Enrichment")]
    public void Changing_any_threshold_changes_the_fingerprint()
    {
        var variants = new[]
        {
            Defaults with { FlatMinFill = 0.98 },
            Defaults with { FlatMaxCorner = 0.16 },
            Defaults with { ThreeDMaxFill = 0.92 },
            Defaults with { ThreeDMinCorner = 0.41 },
            Defaults with { DegenerateBackdropShare = 0.96 },
        };

        variants.Select(variant => variant.Fingerprint).Append(Defaults.Fingerprint).Should().OnlyHaveUniqueItems();
    }
}
