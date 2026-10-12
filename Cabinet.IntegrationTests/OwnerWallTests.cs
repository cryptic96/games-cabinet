using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the owner tools exist only on their own port and answer only to clients inside the configured ranges.</summary>
public class OwnerWallTests
{
    private const string NotAvailableText = "Not available from this address.";

    [Fact]
    public async Task The_settings_page_answers_on_the_owner_port_from_a_home_address()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync("/owner/settings", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("text/html");
        html.Should().Contain("Owner tools");
        html.Should().Contain("<html lang=\"en\">");
    }

    [Fact]
    public async Task A_client_address_outside_every_range_gets_a_bare_refusal()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/owner/settings");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");

        using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.ShouldHaveMediaType("text/plain");
        body.Should().Be(NotAvailableText);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task A_forwarded_client_address_inside_a_range_gets_through()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/owner/settings");
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");

        using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_settings_page_does_not_exist_on_the_public_port()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        using var response = await publicClient.GetAsync("/owner/settings", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task A_path_the_site_does_not_know_answers_the_same_empty_not_found_as_an_owner_path()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        using var unknown = await publicClient.GetAsync("/nothing-here", TestContext.Current.CancellationToken);
        using var owner = await publicClient.GetAsync("/owner/settings", TestContext.Current.CancellationToken);

        unknown.StatusCode.Should().Be(owner.StatusCode);
        (await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be(await owner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_cabinet_answers_on_both_ports()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();
        using var owner = factory.CreateOwnerClient();

        using var onPublic = await publicClient.GetAsync("/", TestContext.Current.CancellationToken);
        using var onOwner = await owner.GetAsync("/", TestContext.Current.CancellationToken);

        onPublic.StatusCode.Should().Be(HttpStatusCode.OK);
        onOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Without_an_owner_endpoint_the_owner_page_answers_not_found_on_the_public_port()
    {
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?> { ["Kestrel:Endpoints:Owner:Url"] = null });
        using var publicClient = factory.CreatePublicClient();

        using var response = await publicClient.GetAsync("/owner/settings", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_owner_page_sets_no_cookie_other_than_the_antiforgery_cookie()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync("/owner/settings", TestContext.Current.CancellationToken);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        cookies.Should().OnlyContain(cookie => cookie.StartsWith("cabinet-owner-af", StringComparison.Ordinal));
    }
}
