using Cabinet.Service.Collection;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.UnitTests.Sync;

/// <summary>
/// Proves where the service keeps its files: the configured directory first, then the first directory the service manager
/// provides, then a folder under the content root in Development only; anywhere else a missing setting stops the app at
/// start-up with a message naming the key, as does a sync setting that is out of range.
/// </summary>
[Trait("Category", "Sync")]
public class StorageLocationTests
{
    private const string StorageKey = "Storage:Directory";
    private const string ServiceManagerKey = "STATE_DIRECTORY";

    [Fact]
    public void A_configured_directory_wins_over_the_service_manager_and_development_and_is_trimmed_and_created()
    {
        using var root = new TemporaryDirectory();
        using var serviceManager = new TemporaryDirectory();
        var configured = Path.Combine(root.FullPath, "state", "cabinet");
        var environment = Development(root.FullPath);

        var resolved = StorageLocation.Resolve(Configuration((StorageKey, $"  {configured}  "), (ServiceManagerKey, serviceManager.FullPath)), environment);

        resolved.Should().Be(configured);
        Directory.Exists(configured).Should().BeTrue();
        Directory.Exists(Path.Combine(root.FullPath, ".cabinet-state")).Should().BeFalse();
    }

    [Fact]
    public void A_relative_configured_directory_is_made_absolute()
    {
        using var root = new TemporaryDirectory();
        var target = Path.Combine(root.FullPath, "relative-state");
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), target);

        var resolved = StorageLocation.Resolve(Configuration((StorageKey, relative)), new TestEnvironment("Production"));

        Path.IsPathFullyQualified(resolved).Should().BeTrue();
        resolved.Should().Be(target);
        Directory.Exists(target).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_configured_directory_the_first_service_manager_directory_is_used(string? configured)
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var separator = Path.PathSeparator;
        var list = $"{separator} {separator}  {first.FullPath}  {separator}{second.FullPath}";

        var resolved = StorageLocation.Resolve(Configuration((StorageKey, configured), (ServiceManagerKey, list)), new TestEnvironment("Production"));

        resolved.Should().Be(first.FullPath);
    }

    [Fact]
    public void Without_either_setting_development_uses_a_folder_under_the_content_root()
    {
        using var root = new TemporaryDirectory();

        var resolved = StorageLocation.Resolve(Configuration(), Development(root.FullPath));

        resolved.Should().Be(Path.Combine(root.FullPath, ".cabinet-state"));
        Directory.Exists(resolved).Should().BeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void Without_either_setting_any_other_environment_stops_with_a_message_naming_the_key(string environmentName)
    {
        using var root = new TemporaryDirectory();
        var environment = new TestEnvironment(environmentName) { ContentRootPath = root.FullPath };

        var resolve = () => StorageLocation.Resolve(Configuration(), environment);

        resolve.Should().Throw<InvalidOperationException>().WithMessage($"*{StorageKey}*");
        Directory.Exists(Path.Combine(root.FullPath, ".cabinet-state")).Should().BeFalse();
    }

    [Fact]
    public void Registering_the_sync_outside_development_without_a_storage_directory_stops_at_once_naming_the_key()
    {
        var register = () => new ServiceCollection().AddCabinetSync(Configuration(), new TestEnvironment("Production"));

        register.Should().Throw<InvalidOperationException>().WithMessage($"*{StorageKey}*");
    }

    [Fact]
    public void Registering_the_sync_with_an_interval_below_the_minimum_stops_at_once_naming_the_key()
    {
        using var root = new TemporaryDirectory();
        var configuration = Configuration((StorageKey, root.FullPath), ("Sync:IntervalMinutes", "5"));

        var register = () => new ServiceCollection().AddCabinetSync(configuration, new TestEnvironment("Production"));

        register.Should().Throw<InvalidOperationException>().WithMessage("*Sync:IntervalMinutes*");
    }

    private static TestEnvironment Development(string contentRoot) =>
        new("Development") { ContentRootPath = contentRoot };

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
}
