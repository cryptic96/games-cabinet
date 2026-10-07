using Cabinet.Domain.Collection;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Sync;

/// <summary>Verifies the details settings fall back to their defaults, bind from configuration and reject bad values with the key named.</summary>
[Trait("Category", "Enrichment")]
public sealed class EnrichmentSettingsTests
{
    [Fact]
    public void The_committed_appsettings_bind_to_the_documented_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        EnrichmentSettings.FromConfiguration(configuration).Should().Be(EnrichmentOptions.Default);
        EnrichmentSettings.FromConfiguration(Configure()).Should().Be(EnrichmentOptions.Default);
        EnrichmentOptions.Default.Should().Be(new EnrichmentOptions(25, 1, TimeSpan.FromDays(7)));
    }

    [Fact]
    public void Values_that_are_set_replace_the_defaults_key_by_key()
    {
        var options = EnrichmentSettings.FromConfiguration(Configure(
            ("Enrichment:MaxThingRequestsPerRun", "3"),
            ("Enrichment:RefreshBatchesPerRun", "0"),
            ("Enrichment:RefreshAfterDays", "30")));

        options.Should().Be(new EnrichmentOptions(3, 0, TimeSpan.FromDays(30)));
    }

    [Theory]
    [InlineData("Enrichment:MaxThingRequestsPerRun", "0")]
    [InlineData("Enrichment:MaxThingRequestsPerRun", "101")]
    [InlineData("Enrichment:MaxThingRequestsPerRun", "many")]
    [InlineData("Enrichment:MaxThingRequestsPerRun", "2.5")]
    [InlineData("Enrichment:RefreshBatchesPerRun", "-1")]
    [InlineData("Enrichment:RefreshBatchesPerRun", "26")]
    [InlineData("Enrichment:RefreshBatchesPerRun", "")]
    [InlineData("Enrichment:RefreshAfterDays", "0")]
    [InlineData("Enrichment:RefreshAfterDays", "91")]
    [InlineData("Enrichment:RefreshAfterDays", "weekly")]
    public void A_value_out_of_range_or_not_a_whole_number_stops_startup_naming_the_key(string key, string value)
    {
        var read = () => EnrichmentSettings.FromConfiguration(Configure((key, value)));

        read.Should().Throw<InvalidOperationException>().WithMessage($"{key} must be a whole number between*");
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}
