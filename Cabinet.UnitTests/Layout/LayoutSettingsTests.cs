using Cabinet.Domain.Layout;
using Cabinet.Service.Layout;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Layout;

/// <summary>Verifies the layout settings bind from configuration, fall back to defaults and reject bad values with the key named.</summary>
public class LayoutSettingsTests
{
    [Fact]
    [Trait("Category", "Layout")]
    public void The_committed_appsettings_bind_to_the_documented_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        var options = LayoutSettings.FromConfiguration(configuration);

        options.Should().Be(new LayoutOptions(25, CoverStrategy.SizeWeighted, 6, 12));
        options.Should().Be(LayoutOptions.Default);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Missing_keys_fall_back_to_the_default_for_that_key()
    {
        var options = LayoutSettings.FromConfiguration(Configure(("Layout:CoverSharePercent", "40")));

        options.Should().Be(LayoutOptions.Default with { CoverSharePercent = 40 });
        LayoutSettings.FromConfiguration(Configure()).Should().Be(LayoutOptions.Default);
    }

    [Theory]
    [InlineData("SizeWeighted", CoverStrategy.SizeWeighted)]
    [InlineData("Random", CoverStrategy.Random)]
    [InlineData("OversizeOnly", CoverStrategy.OversizeOnly)]
    [InlineData("random", CoverStrategy.Random)]
    [InlineData("OVERSIZEONLY", CoverStrategy.OversizeOnly)]
    [InlineData("sizeweighted", CoverStrategy.SizeWeighted)]
    [Trait("Category", "Layout")]
    public void The_cover_strategy_accepts_the_known_names_in_any_letter_case(string text, CoverStrategy expected)
    {
        LayoutSettings.FromConfiguration(Configure(("Layout:CoverStrategy", text))).CoverStrategy.Should().Be(expected);
    }

    [Theory]
    [InlineData("Sideways")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("Random,OversizeOnly")]
    [InlineData("")]
    [Trait("Category", "Layout")]
    public void The_cover_strategy_rejects_other_words_and_numbers(string text)
    {
        var act = () => LayoutSettings.FromConfiguration(Configure(("Layout:CoverStrategy", text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Layout:CoverStrategy*");
    }

    [Theory]
    [InlineData("Layout:CoverSharePercent", "-1")]
    [InlineData("Layout:CoverSharePercent", "101")]
    [InlineData("Layout:CoverSharePercent", "many")]
    [InlineData("Layout:CoverSharePercent", "12.5")]
    [InlineData("Layout:CoverSharePercent", "")]
    [InlineData("Layout:ExpansionStackMax", "0")]
    [InlineData("Layout:ExpansionStackMax", "21")]
    [InlineData("Layout:ExpansionStackMax", "six")]
    [InlineData("Layout:FewGamesThreshold", "-1")]
    [InlineData("Layout:FewGamesThreshold", "101")]
    [InlineData("Layout:FewGamesThreshold", "a dozen")]
    [Trait("Category", "Layout")]
    public void A_number_out_of_range_or_not_a_whole_number_is_rejected_naming_the_key(string key, string text)
    {
        var act = () => LayoutSettings.FromConfiguration(Configure((key, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("Layout:CoverSharePercent", "0")]
    [InlineData("Layout:CoverSharePercent", "100")]
    [InlineData("Layout:ExpansionStackMax", "1")]
    [InlineData("Layout:ExpansionStackMax", "20")]
    [InlineData("Layout:FewGamesThreshold", "0")]
    [InlineData("Layout:FewGamesThreshold", "100")]
    [Trait("Category", "Layout")]
    public void The_edges_of_each_range_are_accepted(string key, string text)
    {
        var act = () => LayoutSettings.FromConfiguration(Configure((key, text)));

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_changed_setting_changes_the_fingerprint()
    {
        var fingerprints = new[]
        {
            LayoutOptions.Default,
            LayoutOptions.Default with { CoverSharePercent = 26 },
            LayoutOptions.Default with { CoverStrategy = CoverStrategy.Random },
            LayoutOptions.Default with { ExpansionStackMax = 7 },
            LayoutOptions.Default with { FewGamesThreshold = 13 },
        }.Select(options => options.Fingerprint).ToList();

        fingerprints.Distinct().Should().HaveCount(fingerprints.Count);
        fingerprints.Should().OnlyContain(fingerprint => fingerprint.Length == 16);
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    private static string FindServiceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Cabinet.Service");

            if (File.Exists(Path.Combine(candidate, "Cabinet.Service.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Cabinet.Service above the test output directory.");
    }
}
