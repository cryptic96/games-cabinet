using Cabinet.Service.Prototype;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;

namespace Cabinet.UnitTests.Prototype;

/// <summary>
/// Verifies the invented collections can only be switched on outside production, and that a bad switch value stops the
/// app in every environment.
/// </summary>
public class SampleCatalogTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    [Trait("Category", "Configuration")]
    public void Production_ignores_the_switch_whatever_it_says(string environmentName)
    {
        var catalog = SampleCatalog.FromConfiguration(Configuration("true"), Environment(environmentName));

        catalog.Enabled.Should().BeFalse();
        catalog.TryResolve("65", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    [Trait("Category", "Configuration")]
    public void Other_environments_honour_the_switch(string environmentName)
    {
        SampleCatalog.FromConfiguration(Configuration("true"), Environment(environmentName)).Enabled.Should().BeTrue();
        SampleCatalog.FromConfiguration(Configuration("false"), Environment(environmentName)).Enabled.Should().BeFalse();
        SampleCatalog.FromConfiguration(Configuration(null), Environment(environmentName)).Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Testing")]
    [Trait("Category", "Configuration")]
    public void A_value_other_than_true_or_false_fails_startup_in_every_environment(string environmentName)
    {
        var read = () => SampleCatalog.FromConfiguration(Configuration("maybe"), Environment(environmentName));

        read.Should().Throw<InvalidOperationException>().WithMessage("Prototype:Enabled must be true or false.");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void An_enabled_catalog_honours_only_exact_sample_names()
    {
        var catalog = SampleCatalog.FromConfiguration(Configuration("true"), Environment("Development"));

        catalog.TryResolve("65", out var name).Should().BeTrue();
        name.Should().Be("65");
        catalog.TryResolve("64", out _).Should().BeFalse();
        catalog.TryResolve(" 65", out _).Should().BeFalse();
        catalog.TryResolve(null, out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void The_committed_development_settings_switch_the_prototype_on_and_the_committed_default_leaves_it_off()
    {
        var directory = FindServiceDirectory();
        var committed = new ConfigurationBuilder().AddJsonFile(Path.Combine(directory, "appsettings.json")).Build();
        var development = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(directory, "appsettings.json"))
            .AddJsonFile(Path.Combine(directory, "appsettings.Development.json"))
            .Build();

        committed[SampleCatalog.EnabledKey].Should().Be("False");
        development[SampleCatalog.EnabledKey].Should().Be("True");
    }

    private static IConfiguration Configuration(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(value is null ? [] : new Dictionary<string, string?> { [SampleCatalog.EnabledKey] = value })
            .Build();

    private static HostingEnvironment Environment(string name) => new() { EnvironmentName = name };

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
