using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the app itself sends a strict content security policy on the public listener, for the page, the layout data,
/// the static files and refusals alike, and that the policy never allows inline code or eval.
/// </summary>
public class ContentSecurityPolicyTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string ExpectedPolicy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/?sample=400", HttpStatusCode.OK)]
    [InlineData("/cabinet/layout?sample=65&profile=desktop", HttpStatusCode.OK)]
    [InlineData("/cabinet/layout?sample=65&profile=phone", HttpStatusCode.OK)]
    [InlineData("/js/render.js", HttpStatusCode.OK)]
    [InlineData("/img/powered-by-bgg.svg", HttpStatusCode.OK)]
    [InlineData("/cabinet/layout?profile=tablet", HttpStatusCode.NotFound)]
    [InlineData("/no-such-page", HttpStatusCode.NotFound)]
    public async Task Every_public_response_carries_the_strict_policy(string path, HttpStatusCode status)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(status);
        PolicyOf(response).Should().Be(ExpectedPolicy);
    }

    [Fact]
    public async Task The_policy_allows_neither_inline_code_nor_eval_nor_other_origins()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        using var layout = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);

        foreach (var policy in new[] { PolicyOf(page), PolicyOf(layout) })
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
    public async Task A_layout_revalidation_answered_not_modified_still_carries_the_policy()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync("/cabinet/layout?sample=12&profile=desktop", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/layout?sample=12&profile=desktop");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        PolicyOf(second).Should().Be(ExpectedPolicy);
    }

    [Fact]
    public async Task The_page_with_the_prototype_off_carries_the_policy()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PolicyOf(response).Should().Be(ExpectedPolicy);
    }

    private static string PolicyOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Content-Security-Policy", out var values)
            ? values.Should().ContainSingle("the policy is sent exactly once").Subject
            : throw new InvalidOperationException($"The response for {response.RequestMessage?.RequestUri} has no Content-Security-Policy header.");
}
