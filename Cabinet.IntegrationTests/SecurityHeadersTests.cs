using System.Net;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Hosting;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the app itself sends the full security-header set, each header exactly once with its expected value, for the
/// page, the layout data, the static files, redirects and refusals alike, and that the content security policy never
/// allows inline code or eval. The matrix covers pages, static files, served box art, JSON, the live negotiation, redirects,
/// 304, 404 and 429 answers on the public and owner listeners and the ops health answer. A few answers are produced before
/// the pipeline runs and carry none of these headers: host filtering's 400, Kestrel's own 408, 413 and 431 answers to a
/// request it cannot read, and the 500 of an unhandled exception that unwinds past the first middleware. They are empty
/// of content the browser would render, so the omission is accepted, and the host filtering case is proven below so the
/// exclusion is a tested fact.
/// </summary>
public class SecurityHeadersTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string LiveNegotiatePath = "/cabinet/live/negotiate?negotiateVersion=1";

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

    [Fact]
    public async Task A_served_box_picture_carries_the_full_header_set_with_nosniff_once()
    {
        using var storage = new TemporaryDirectory();
        var art = Directory.CreateDirectory(Path.Combine(storage.FullPath, "art"));
        await File.WriteAllBytesAsync(Path.Combine(art.FullName, "0123456789abcdef-240.webp"), [0x52, 0x49, 0x46, 0x46], TestContext.Current.CancellationToken);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/art/0123456789abcdef-240.webp", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        AssertFullSet(response);
    }

    [Fact]
    public async Task A_missing_box_picture_answers_not_found_with_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/art/ffffffffffffffff-240.webp", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertFullSet(response);
    }

    [Fact]
    public async Task The_per_client_sync_refusal_carries_the_full_header_set()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(
            handler,
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["ClientLimits:SyncPressesPerWindow"] = "1", ["ClientLimits:SyncWindowSeconds"] = "60" });
        using var client = factory.CreatePublicClient();

        using var first = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        using var refused = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        AssertFullSet(first);
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        AssertFullSet(refused);
        await SyncHarness.WaitForRunToEnd(client);
    }

    [Fact]
    public async Task The_live_negotiate_refusal_has_an_empty_body_and_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsPerWindow"] = "1" });
        using var client = factory.CreatePublicClient();

        using var admitted = await client.PostAsync(LiveNegotiatePath, content: null, TestContext.Current.CancellationToken);
        using var refused = await client.PostAsync(LiveNegotiatePath, content: null, TestContext.Current.CancellationToken);

        admitted.StatusCode.Should().Be(HttpStatusCode.OK);
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        AssertFullSet(refused);
    }

    [Theory]
    [InlineData("/owner/pictures", HttpStatusCode.OK)]
    [InlineData("/owner/settings", HttpStatusCode.OK)]
    [InlineData("/js/owner-look.js", HttpStatusCode.OK)]
    [InlineData("/js/owner-look-sample.json", HttpStatusCode.OK)]
    [InlineData("/owner/no-such-page", HttpStatusCode.NotFound)]
    public async Task Every_owner_response_carries_the_full_header_set(string path, HttpStatusCode status)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(status);
        AssertFullSet(response);
    }

    [Fact]
    public async Task The_owner_refusal_for_an_address_outside_every_range_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/owner/settings");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");

        using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AssertFullSet(response);
    }

    [Fact]
    public async Task The_owner_live_negotiation_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.PostAsync(LiveNegotiatePath, content: null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertFullSet(response);
    }

    [Fact]
    public async Task The_ops_health_answer_carries_the_full_header_set()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var ops = factory.CreateOpsClient();

        using var response = await ops.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertFullSet(response);
    }

    [Fact]
    public async Task A_request_for_a_host_that_is_not_allowed_is_refused_before_the_pipeline_without_the_policy()
    {
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?> { ["AllowedHosts"] = "cabinet.example.com;127.0.0.1" });
        using var client = factory.CreatePublicClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "elsewhere.example.org";

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Contains("Content-Security-Policy").Should().BeFalse("host filtering answers before the header middleware runs");
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
