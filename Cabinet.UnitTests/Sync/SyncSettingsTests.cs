using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Sync;

/// <summary>Verifies the sync settings bind from configuration, fall back to defaults and reject bad values with the key named.</summary>
[Trait("Category", "Sync")]
public class SyncSettingsTests
{
    private static readonly SyncOptions Defaults = new(
        true,
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromHours(3));

    [Fact]
    public void The_committed_appsettings_bind_to_the_documented_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        SyncSettings.FromConfiguration(configuration).Should().Be(Defaults);
    }

    [Fact]
    public void Missing_keys_fall_back_to_the_default_for_that_key()
    {
        SyncSettings.FromConfiguration(Configure()).Should().Be(Defaults);
        SyncSettings.FromConfiguration(Configure(("Sync:ManualCooldownMinutes", "5")))
            .Should().Be(Defaults with { ManualCooldown = TimeSpan.FromMinutes(5) });
    }

    [Fact]
    public void Every_key_can_be_set()
    {
        var options = SyncSettings.FromConfiguration(Configure(
            ("Sync:BackgroundEnabled", "false"),
            ("Sync:IntervalMinutes", "15"),
            ("Sync:ManualCooldownMinutes", "120"),
            ("Sync:StartupJitterMaxSeconds", "10"),
            ("Sync:StaleAfterHours", "168")));

        options.Should().Be(new SyncOptions(
            false,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(120),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromHours(168)));
    }

    [Theory]
    [InlineData("Sync:IntervalMinutes", "14")]
    [InlineData("Sync:IntervalMinutes", "1441")]
    [InlineData("Sync:IntervalMinutes", "hourly")]
    [InlineData("Sync:IntervalMinutes", "")]
    [InlineData("Sync:ManualCooldownMinutes", "0")]
    [InlineData("Sync:ManualCooldownMinutes", "121")]
    [InlineData("Sync:ManualCooldownMinutes", "2.5")]
    [InlineData("Sync:StartupJitterMaxSeconds", "9")]
    [InlineData("Sync:StartupJitterMaxSeconds", "3601")]
    [InlineData("Sync:StaleAfterHours", "0")]
    [InlineData("Sync:StaleAfterHours", "169")]
    [InlineData("Sync:StaleAfterHours", "-3")]
    public void A_number_out_of_range_or_not_a_whole_number_is_rejected_naming_the_key(string key, string text)
    {
        var act = () => SyncSettings.FromConfiguration(Configure((key, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    public void A_switch_that_is_not_true_or_false_is_rejected_naming_the_key(string text)
    {
        var act = () => SyncSettings.FromConfiguration(Configure(("Sync:BackgroundEnabled", text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Sync:BackgroundEnabled*");
    }

    [Fact]
    public void The_background_switch_accepts_true_and_false_in_any_letter_case()
    {
        SyncSettings.FromConfiguration(Configure(("Sync:BackgroundEnabled", "FALSE"))).BackgroundEnabled.Should().BeFalse();
        SyncSettings.FromConfiguration(Configure(("Sync:BackgroundEnabled", " True "))).BackgroundEnabled.Should().BeTrue();
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

        throw new DirectoryNotFoundException("Cabinet.Service was not found above the test output directory.");
    }
}
