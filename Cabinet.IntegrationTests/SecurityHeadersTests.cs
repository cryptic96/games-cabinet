using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Hosting;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the app itself sends the full security-header set, each header exactly once with its expected value, for the
/// page, the layout data, the static files, redirects and refusals alike, and that the content security policy never
/// allows inline code or eval.
/// </summary>
public class SecurityHeadersTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string ExpectedPolicy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/?sample=400", HttpStatusCode.OK)]
    [InlineData("/cabinet/layout?sample=65&profile=desktop", HttpStatusCode.OK)]
    [InlineData("/cabinet/layout?sample=65&profile=phone", HttpStatusCode.OK)]
    [InlineData("/cabinet/cards?sample=65&profile=desktop", HttpStatusCode.OK)]
    [InlineData("/cabinet/cards?profile=desktop", HttpStatusCode.OK)]
    [InlineData("/cabinet/cards?profile=tablet", HttpStatusCode.NotFound)]
    [InlineData("/js/render.js", HttpStatusCode.OK)]
    [InlineData("/lib/signalr/signalr.min.js", HttpStatusCode.OK)]
    [InlineData("/img/powered-by-bgg.svg", HttpStatusCode.OK)]
    [InlineData("/img/icons.svg", HttpStatusCode.OK)]
    [InlineData("/cabinet/status", HttpStatusCode.OK)]
    [InlineData("/cabinet/layout?profile=tablet", HttpStatusCode.NotFound)]
    [InlineData("/no-such-page", HttpStatusCode.NotFound)]
    public async Task Every_public_response_carries_the_full_header_set(string path, HttpStatusCode status)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(status);
        AssertFullSet(response);
    }

    [Fact]
    public async Task The_language_switch_redirect_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = LanguageTests.PlainClient(factory);

        using var response = await client.GetAsync("/language/en", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        AssertFullSet(response);
    }

    [Fact]
    public async Task The_live_negotiation_carries_the_full_header_set_and_no_cross_origin_permission()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.PostAsync(
            "/cabinet/live/negotiate?negotiateVersion=1",
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertFullSet(response);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task The_policy_allows_neither_inline_code_nor_eval_nor_other_origins()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        using var layout = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);

        foreach (var policy in new[] { SingleValue(page, "Content-Security-Policy"), SingleValue(layout, "Content-Security-Policy") })
        {
            policy.Should().NotContain("unsafe-inline");
            policy.Should().NotContain("unsafe-eval");
            policy.Should().NotContain("*");
            policy.Should().NotContain("data:");
            policy.Should().NotContain("http");
            policy.Should().Contain("default-src 'self'");
            policy.Should().Contain("frame-ancestors 'none'");
        }
    }

    [Fact]
    public async Task A_layout_revalidation_answered_not_modified_still_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync("/cabinet/layout?sample=12&profile=desktop", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/layout?sample=12&profile=desktop");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        AssertFullSet(second);
    }

    [Fact]
    public async Task The_page_with_the_prototype_off_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertFullSet(response);
    }

    /// <summary>Asserts every header of the set appears exactly once on the response with its expected value.</summary>
    internal static void AssertFullSet(HttpResponseMessage response)
    {
        SingleValue(response, "Content-Security-Policy").Should().Be(SecurityHeaders.ContentSecurityPolicy);
        SingleValue(response, "Content-Security-Policy").Should().Be(ExpectedPolicy);
        SingleValue(response, "X-Content-Type-Options").Should().Be(SecurityHeaders.ContentTypeOptions);
        SingleValue(response, "Referrer-Policy").Should().Be(SecurityHeaders.ReferrerPolicy);
        SingleValue(response, "X-Frame-Options").Should().Be(SecurityHeaders.FrameOptions);
        SingleValue(response, "Permissions-Policy").Should().Be(SecurityHeaders.PermissionsPolicy);
        SingleValue(response, "Cross-Origin-Opener-Policy").Should().Be(SecurityHeaders.CrossOriginOpenerPolicy);
        SingleValue(response, "Cross-Origin-Resource-Policy").Should().Be(SecurityHeaders.CrossOriginResourcePolicy);
    }

    private static string SingleValue(HttpResponseMessage response, string name)
    {
        var found = response.Headers.TryGetValues(name, out var values);
        if (!found && response.Content is not null)
        {
            found = response.Content.Headers.TryGetValues(name, out values);
        }

        return found
            ? values!.Should().ContainSingle($"{name} is sent exactly once").Subject
            : throw new InvalidOperationException($"The response for {response.RequestMessage?.RequestUri} has no {name} header.");
    }
}
