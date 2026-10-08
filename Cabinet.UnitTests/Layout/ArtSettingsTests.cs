using Cabinet.Domain.Collection;
using Cabinet.Service.Layout;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Layout;

/// <summary>Verifies the picture-choice settings bind from configuration, fall back to the defaults and reject bad values with the key named.</summary>
[Trait("Category", "Layout")]
public class ArtSettingsTests
{
    [Fact]
    public void The_committed_appsettings_bind_to_the_default_rules()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        ArtSettings.FromConfiguration(configuration).Should().Be(ArtRules.Default);
    }

    [Fact]
    public void Absent_keys_give_the_default_rules()
    {
        ArtSettings.FromConfiguration(Configure()).Should().Be(ArtRules.Default);
    }

    [Fact]
    public void A_single_key_changes_only_its_own_threshold()
    {
        var rules = ArtSettings.FromConfiguration(Configure(("Art:FlatMinFillPercent", "99")));

        rules.Thresholds.Should().Be(ArtThresholds.Default with { FlatMinFill = 0.99 });
        rules.Fingerprint.Should().NotBe(ArtRules.Default.Fingerprint);
    }

    [Fact]
    public void The_percentages_become_fractions()
    {
        var rules = ArtSettings.FromConfiguration(Configure(
            ("Art:FlatMinFillPercent", "96"),
            ("Art:FlatMaxCornerPercent", "10"),
            ("Art:ThreeDMaxFillPercent", "90"),
            ("Art:ThreeDMinCornerPercent", "35")));

        rules.Thresholds.Should().Be(new ArtThresholds(0.96, 0.10, 0.90, 0.35, ArtThresholds.Default.DegenerateBackdropShare));
    }

    [Theory]
    [InlineData("Art:FlatMinFillPercent", "49")]
    [InlineData("Art:FlatMinFillPercent", "101")]
    [InlineData("Art:FlatMinFillPercent", "high")]
    [InlineData("Art:FlatMinFillPercent", "97.5")]
    [InlineData("Art:FlatMinFillPercent", "")]
    [InlineData("Art:FlatMaxCornerPercent", "-1")]
    [InlineData("Art:FlatMaxCornerPercent", "101")]
    [InlineData("Art:FlatMaxCornerPercent", "few")]
    [InlineData("Art:ThreeDMaxFillPercent", "-1")]
    [InlineData("Art:ThreeDMaxFillPercent", "101")]
    [InlineData("Art:ThreeDMaxFillPercent", "0.9")]
    [InlineData("Art:ThreeDMinCornerPercent", "-1")]
    [InlineData("Art:ThreeDMinCornerPercent", "101")]
    [InlineData("Art:ThreeDMinCornerPercent", "forty")]
    public void A_value_out_of_range_or_not_a_whole_number_is_rejected_naming_the_key(string key, string text)
    {
        var act = () => ArtSettings.FromConfiguration(Configure((key, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("Art:FlatMinFillPercent", "100")]
    [InlineData("Art:FlatMaxCornerPercent", "0")]
    [InlineData("Art:FlatMaxCornerPercent", "100")]
    [InlineData("Art:ThreeDMaxFillPercent", "0")]
    [InlineData("Art:ThreeDMinCornerPercent", "0")]
    [InlineData("Art:ThreeDMinCornerPercent", "100")]
    public void The_edges_of_each_range_are_accepted(string key, string text)
    {
        var act = () => ArtSettings.FromConfiguration(Configure((key, text)));

        act.Should().NotThrow();
    }

    [Fact]
    public void A_photographed_box_fill_limit_above_the_flat_cover_fill_minimum_is_refused_naming_both_keys()
    {
        var act = () => ArtSettings.FromConfiguration(Configure(("Art:ThreeDMaxFillPercent", "98")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Art:ThreeDMaxFillPercent*Art:FlatMinFillPercent*");
    }

    [Fact]
    public void The_least_flat_fill_is_accepted_when_the_photographed_box_limit_is_lower()
    {
        var act = () => ArtSettings.FromConfiguration(Configure(("Art:FlatMinFillPercent", "50"), ("Art:ThreeDMaxFillPercent", "40")));

        act.Should().NotThrow();
    }

    [Fact]
    public void Equal_fill_limits_are_accepted()
    {
        var act = () => ArtSettings.FromConfiguration(Configure(("Art:FlatMinFillPercent", "95"), ("Art:ThreeDMaxFillPercent", "95")));

        act.Should().NotThrow();
    }

    [Fact]
    public void The_shape_settings_bind_and_change_the_fingerprint()
    {
        var rules = ArtSettings.FromConfiguration(Configure(("Art:ShapeMarginPercent", "20"), ("Art:OrientFromCover", "false")));

        rules.ShapeMarginPercent.Should().Be(20);
        rules.OrientFromCover.Should().BeFalse();
        rules.Fingerprint.Should().NotBe(ArtRules.Default.Fingerprint);
        ArtSettings.FromConfiguration(Configure(("Art:ShapeMarginPercent", "20"))).Fingerprint.Should().NotBe(ArtRules.Default.Fingerprint);
        ArtSettings.FromConfiguration(Configure(("Art:OrientFromCover", "false"))).Fingerprint.Should().NotBe(ArtRules.Default.Fingerprint);
    }

    [Theory]
    [InlineData("Art:ShapeMarginPercent", "0")]
    [InlineData("Art:ShapeMarginPercent", "51")]
    [InlineData("Art:ShapeMarginPercent", "twelve")]
    [InlineData("Art:ShapeMarginPercent", "12.5")]
    [InlineData("Art:ShapeMarginPercent", "")]
    [InlineData("Art:OrientFromCover", "yes")]
    [InlineData("Art:OrientFromCover", "")]
    [InlineData("Art:OrientFromCover", "1")]
    [InlineData("Art:UnsureLandscapeMarginPercent", "0")]
    [InlineData("Art:UnsureLandscapeMarginPercent", "101")]
    [InlineData("Art:UnsureLandscapeMarginPercent", "twenty")]
    [InlineData("Art:UnsureLandscapeMarginPercent", "20.5")]
    [InlineData("Art:UnsureLandscapeMarginPercent", "")]
    public void A_bad_shape_setting_stops_startup_naming_the_key(string key, string text)
    {
        var act = () => ArtSettings.FromConfiguration(Configure((key, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("50")]
    public void The_edges_of_the_shape_margin_range_are_accepted(string text)
    {
        var act = () => ArtSettings.FromConfiguration(Configure(("Art:ShapeMarginPercent", text)));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("100")]
    public void The_edges_of_the_unsure_landscape_margin_range_are_accepted(string text)
    {
        var act = () => ArtSettings.FromConfiguration(Configure(("Art:UnsureLandscapeMarginPercent", text)));

        act.Should().NotThrow();
    }

    [Fact]
    public void The_unsure_landscape_margin_binds_and_changes_the_fingerprint()
    {
        ArtSettings.FromConfiguration(Configure()).UnsureLandscapeMarginPercent.Should().Be(20);

        var rules = ArtSettings.FromConfiguration(Configure(("Art:UnsureLandscapeMarginPercent", "35")));

        rules.UnsureLandscapeMarginPercent.Should().Be(35);
        rules.Fingerprint.Should().NotBe(ArtRules.Default.Fingerprint);
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}
