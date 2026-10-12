using Cabinet.Domain.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves the detector's verdict maps to exactly one of the five kinds the owner reads.</summary>
[Trait("Category", "Images")]
public sealed class ArtVerdictKindTests
{
    private static readonly ArtFeatures Plain = new(0.1, 0.9, 0.2, 0.1, 2);

    [Fact]
    public void A_flat_verdict_is_flat_whatever_the_features_say()
    {
        ArtVerdictKinds.Of(ArtVerdict.Flat, Plain).Should().Be(ArtVerdictKind.Flat);
        ArtVerdictKinds.Of(ArtVerdict.Flat, null).Should().Be(ArtVerdictKind.Flat);
        ArtVerdictKinds.Of(ArtVerdict.Flat, Plain with { CutOut = true }).Should().Be(ArtVerdictKind.Flat);
    }

    [Fact]
    public void An_unsure_verdict_is_unsure()
    {
        ArtVerdictKinds.Of(ArtVerdict.Unsure, Plain).Should().Be(ArtVerdictKind.Unsure);
        ArtVerdictKinds.Of(ArtVerdict.Unsure, null).Should().Be(ArtVerdictKind.Unsure);
    }

    [Fact]
    public void A_3D_verdict_with_a_cut_out_picture_is_a_cut_out()
    {
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, Plain with { CutOut = true }).Should().Be(ArtVerdictKind.CutOut);
    }

    [Fact]
    public void A_3D_verdict_with_a_tight_crop_and_no_cut_out_is_a_tight_crop()
    {
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, Plain with { CutOut = false, TightCrop = true }).Should().Be(ArtVerdictKind.TightCrop);
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, Plain with { TightCrop = true }).Should().Be(ArtVerdictKind.TightCrop);
    }

    [Fact]
    public void A_cut_out_wins_over_a_tight_crop_when_both_are_set()
    {
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, Plain with { CutOut = true, TightCrop = true }).Should().Be(ArtVerdictKind.CutOut);
    }

    [Fact]
    public void A_3D_verdict_with_neither_mark_or_no_features_is_a_plain_3D_shot()
    {
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, Plain).Should().Be(ArtVerdictKind.ThreeD);
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, Plain with { CutOut = false, TightCrop = false }).Should().Be(ArtVerdictKind.ThreeD);
        ArtVerdictKinds.Of(ArtVerdict.ThreeD, null).Should().Be(ArtVerdictKind.ThreeD);
    }

    [Fact]
    public void Every_kind_is_produced_by_some_input()
    {
        var verdicts = Enum.GetValues<ArtVerdict>();
        var featureSets = new ArtFeatures?[] { null, Plain, Plain with { CutOut = true }, Plain with { TightCrop = true } };

        var produced = verdicts.SelectMany(verdict => featureSets.Select(features => ArtVerdictKinds.Of(verdict, features))).Distinct();

        produced.Should().BeEquivalentTo(Enum.GetValues<ArtVerdictKind>());
    }
}
