using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the site publishes no robots.txt on any listener, so crawlers can read the noindex tag on the pages
/// instead of being told to stay away from them.
/// </summary>
public class RobotsAbsenceTests
{
    [Theory]
    [InlineData("public")]
    [InlineData("owner")]
    [InlineData("ops")]
    public async Task No_listener_serves_a_robots_file(string listener)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = listener switch
        {
            "public" => factory.CreatePublicClient(),
            "owner" => factory.CreateOwnerClient(),
            _ => factory.CreateOpsClient(),
        };

        using var response = await client.GetAsync("/robots.txt", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_public_home_page_asks_crawlers_not_to_index_it()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var body = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        body.Should().Contain("<meta name=\"robots\" content=\"noindex\"");
    }
}
