using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the public hello page shows the running version and links a served, fingerprinted stylesheet.</summary>
public partial class HelloPageTests
{
    [Fact]
    public async Task Hello_page_shows_the_running_version()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var ops = factory.CreateOpsClient();
        using var publicClient = factory.CreatePublicClient();

        var health = await ops.GetStringAsync("/health", TestContext.Current.CancellationToken);
        using var healthDocument = JsonDocument.Parse(health);
        var version = healthDocument.RootElement.GetProperty("version").GetString();

        using var response = await publicClient.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("text/html");
        version.Should().NotBeNullOrWhiteSpace();
        html.Should().Contain($"Version {version}");
    }

    [Fact]
    public async Task Hello_page_stylesheet_is_fingerprinted_and_served()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        var html = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var match = StylesheetLink().Match(html);

        match.Success.Should().BeTrue("the page links a stylesheet");
        match.Groups["href"].Value.Should().MatchRegex(@"^/css/site\..+\.css$");

        using var stylesheet = await publicClient.GetAsync(match.Groups["href"].Value, TestContext.Current.CancellationToken);

        stylesheet.StatusCode.Should().Be(HttpStatusCode.OK);
        stylesheet.ShouldHaveMediaType("text/css");
    }

    [GeneratedRegex("<link[^>]*rel=\"stylesheet\"[^>]*href=\"(?<href>[^\"]+)\"")]
    private static partial Regex StylesheetLink();
}
