using System.Net;
using System.Text.Json;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the app answers only to the hostnames listed in the server env file and to loopback, which the deploy health
/// check and the selfcheck use. A request naming any other host is refused with 400 on every listener.
/// </summary>
public class HostFilteringTests
{
    private const string AllowedHostsKey = "AllowedHosts";
    private const string PublicHost = "cabinet.example.com";
    private const string OwnerHost = "owner.example.com";
    private const string ForeignHost = "elsewhere.example.org";
    private const string WithLoopback = "cabinet.example.com;owner.example.com;127.0.0.1;localhost";
    private const string WithoutLoopback = "cabinet.example.com;owner.example.com";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_listed_public_host_is_served()
    {
        await using var factory = Filtered(WithLoopback);

        var (status, _) = await RawHttp.SendAsync(factory.PublicPort, "/", Host(PublicHost), Token);

        status.Should().Be(200);
    }

    [Fact]
    public async Task A_foreign_host_on_the_public_port_is_refused_with_400()
    {
        await using var factory = Filtered(WithLoopback);

        var (status, _) = await RawHttp.SendAsync(factory.PublicPort, "/", Host(ForeignHost), Token);

        status.Should().Be(400);
    }

    [Fact]
    public async Task The_loopback_host_is_served_on_the_public_port()
    {
        await using var factory = Filtered(WithLoopback);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/", Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_on_the_ops_port_answers_through_its_loopback_host()
    {
        await using var factory = Filtered(WithLoopback);
        using var ops = factory.CreateOpsClient();

        var body = await ops.GetStringAsync("/health", Token);
        using var document = JsonDocument.Parse(body);

        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task The_owner_port_serves_its_listed_host_and_refuses_a_foreign_one()
    {
        await using var factory = Filtered(WithLoopback);

        var (served, _) = await RawHttp.SendAsync(factory.OwnerPort, "/owner/pictures", Host(OwnerHost), Token);
        var (refused, _) = await RawHttp.SendAsync(factory.OwnerPort, "/owner/pictures", Host(ForeignHost), Token);

        served.Should().Be(200);
        refused.Should().Be(400);
    }

    [Fact]
    public async Task A_foreign_host_is_refused_on_the_ops_port_too()
    {
        await using var factory = Filtered(WithLoopback);

        var (status, _) = await RawHttp.SendAsync(factory.OpsPort, "/health", Host(ForeignHost), Token);

        status.Should().Be(400);
    }

    [Fact]
    public async Task Health_is_refused_when_loopback_is_missing_from_the_list_so_the_env_file_must_name_it()
    {
        await using var factory = Filtered(WithoutLoopback);
        using var ops = factory.CreateOpsClient();

        using var response = await ops.GetAsync("/health", Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_committed_setting_allows_every_host_so_development_and_tests_are_unfiltered()
    {
        await using var factory = new CabinetWebApplicationFactory();

        var (status, _) = await RawHttp.SendAsync(factory.PublicPort, "/", Host(ForeignHost), Token);

        status.Should().Be(200);
    }

    private static CabinetWebApplicationFactory Filtered(string allowedHosts) =>
        new(new Dictionary<string, string?> { [AllowedHostsKey] = allowedHosts });

    private static Dictionary<string, string> Host(string host) => new() { ["Host"] = host };
}
