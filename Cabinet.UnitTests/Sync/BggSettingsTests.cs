using Cabinet.Domain.Collection;
using Cabinet.Repository.Bgg;
using Cabinet.Service.Collection;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cabinet.UnitTests.Sync;

/// <summary>Proves the BGG settings are read from configuration only, validated, and reported once at start-up without their values.</summary>
[Trait("Category", "Configuration")]
public class BggSettingsTests
{
    private const string Username = "sentinel-user-name";
    private const string Token = "sentinel-token-value";

    [Fact]
    public void In_production_a_configured_base_address_is_ignored_and_reported()
    {
        var options = Read("Production", ("Bgg:BaseUri", "http://127.0.0.1:6190/xmlapi2/"));

        options.BaseUri.Should().Be(BggOptions.DefaultBaseUri);
        options.BaseUriOverrideIgnored.Should().BeTrue();
    }

    [Fact]
    public void In_production_without_a_base_address_nothing_is_reported_as_ignored()
    {
        var options = Read("Production");

        options.BaseUri.Should().Be(BggOptions.DefaultBaseUri);
        options.BaseUriOverrideIgnored.Should().BeFalse();
    }

    [Fact]
    public void In_production_even_an_invalid_base_address_is_ignored_rather_than_fatal()
    {
        var options = Read("Production", ("Bgg:BaseUri", "not an address"));

        options.BaseUri.Should().Be(BggOptions.DefaultBaseUri);
        options.BaseUriOverrideIgnored.Should().BeTrue();
    }

    [Fact]
    public void In_development_the_base_address_is_honoured()
    {
        var options = Read("Development", ("Bgg:BaseUri", "http://127.0.0.1:6190/xmlapi2/"));

        options.BaseUri.Should().Be(new Uri("http://127.0.0.1:6190/xmlapi2/"));
        options.BaseUriOverrideIgnored.Should().BeFalse();
    }

    [Fact]
    public void In_development_an_invalid_base_address_stops_the_app_and_names_the_key()
    {
        var act = () => Read("Development", ("Bgg:BaseUri", "not an address"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bgg:BaseUri*");
    }

    [Theory]
    [InlineData("4")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("often")]
    public void A_gap_below_the_minimum_stops_the_app_and_names_the_key(string gap)
    {
        var act = () => Read("Production", ("Bgg:MinRequestGapSeconds", gap));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bgg:MinRequestGapSeconds*");
    }

    [Fact]
    public void The_minimum_gap_is_accepted()
    {
        Read("Production", ("Bgg:MinRequestGapSeconds", "5")).MinRequestGap.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(Username, null)]
    [InlineData(null, Token)]
    [InlineData("   ", Token)]
    public void A_missing_username_or_token_is_allowed_and_means_not_configured(string? username, string? token)
    {
        var options = Read("Production", ("Bgg:Username", username), ("Bgg:Token", token));

        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Both_a_username_and_a_token_mean_configured()
    {
        Read("Production", ("Bgg:Username", Username), ("Bgg:Token", Token)).IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Private_info_is_off_unless_it_is_switched_on()
    {
        Read("Production").IncludePrivateInfo.Should().BeFalse();
        Read("Production", ("Bgg:IncludePrivateInfo", "true")).IncludePrivateInfo.Should().BeTrue();
    }

    [Fact]
    public async Task Start_up_says_once_that_the_credentials_are_missing_and_never_prints_a_value()
    {
        using var storage = new TemporaryDirectory();
        var logger = new ListLogger();
        var options = Read("Production", ("Bgg:Username", Username));

        await Startup(storage, options, logger).StartAsync(TestContext.Current.CancellationToken);

        logger.Lines.Should().ContainSingle().Which.Should().Be("BGG username or token is not configured; syncs are skipped until both are set.");
        logger.Lines.Should().NotContain(line => line.Contains(Username, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_up_says_once_that_the_base_address_is_ignored_and_stays_silent_when_all_is_well()
    {
        using var storage = new TemporaryDirectory();
        var ignored = new ListLogger();
        var quiet = new ListLogger();

        await Startup(storage, Read("Production", ("Bgg:Username", Username), ("Bgg:Token", Token), ("Bgg:BaseUri", "http://127.0.0.1/xmlapi2/")), ignored)
            .StartAsync(TestContext.Current.CancellationToken);
        await Startup(storage, Read("Production", ("Bgg:Username", Username), ("Bgg:Token", Token)), quiet)
            .StartAsync(TestContext.Current.CancellationToken);

        ignored.Lines.Should().ContainSingle().Which.Should().Be("Bgg:BaseUri is ignored outside Development.");
        quiet.Lines.Should().BeEmpty();
    }

    private static SyncStartup Startup(TemporaryDirectory storage, BggOptions options, ILogger<SyncStartup> logger) =>
        new(new StorageDirectory(storage.FullPath), new EmptyStore(), new CollectionStore(), options, ImageSettings.FromConfiguration(new ConfigurationBuilder().Build(), new TestEnvironment("Production")), ArtRules.Default, logger);

    private static BggOptions Read(string environment, params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        return BggSettings.FromConfiguration(configuration, new TestEnvironment(environment), "1.2.3");
    }

    private sealed class EmptyStore : ISnapshotStore
    {
        public CollectionSnapshot? Load() => null;

        public void Save(CollectionSnapshot snapshot)
        {
        }
    }

    private sealed class ListLogger : ILogger<SyncStartup>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
