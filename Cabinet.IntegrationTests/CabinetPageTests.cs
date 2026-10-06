using System.Net;
using System.Text.RegularExpressions;
using Cabinet.Domain.Samples;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the page mounts the cabinet, links served fingerprinted assets and stays free of inline styles and scripts.</summary>
public partial class CabinetPageTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task Page_has_the_cabinet_mount()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().Contain("id=\"cabinet\"");
    }

    [Fact]
    public async Task Page_links_a_fingerprinted_cabinet_stylesheet_that_is_served()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var hrefs = StylesheetLink().Matches(html).Select(match => match.Groups["href"].Value).ToList();
        var cabinetHref = hrefs.SingleOrDefault(href => href.StartsWith("/css/cabinet.", StringComparison.Ordinal));

        cabinetHref.Should().NotBeNull("the page links the cabinet stylesheet");
        cabinetHref.Should().MatchRegex(@"^/css/cabinet\..+\.css$");
        hrefs[0].Should().MatchRegex(@"^/css/site\..+\.css$", "the shared stylesheet stays first");

        using var stylesheet = await client.GetAsync(cabinetHref, TestContext.Current.CancellationToken);

        stylesheet.StatusCode.Should().Be(HttpStatusCode.OK);
        stylesheet.Content.Headers.ContentType?.MediaType.Should().Be("text/css");
    }

    [Fact]
    public async Task Page_links_a_fingerprinted_module_script_that_is_served()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var match = ModuleScript().Match(html);

        match.Success.Should().BeTrue("the page loads the cabinet as a module script");
        match.Groups["src"].Value.Should().MatchRegex(@"^/js/cabinet\..+\.js$");

        using var script = await client.GetAsync(match.Groups["src"].Value, TestContext.Current.CancellationToken);

        script.StatusCode.Should().Be(HttpStatusCode.OK);
        script.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
    }

    [Theory]
    [InlineData("/js/render.js")]
    [InlineData("/js/copy.js")]
    public async Task Modules_imported_by_relative_path_are_served_at_their_original_paths(string path)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
    }

    [Fact]
    public async Task Page_carries_no_inline_style_attributes_and_no_inline_script_bodies()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        StyleAttribute().IsMatch(html).Should().BeFalse("inline styles are blocked by a strict content security policy");
        InlineScriptBody().IsMatch(html).Should().BeFalse("inline scripts are blocked by a strict content security policy");
    }

    [Fact]
    public async Task Default_page_shows_the_being_filled_message_above_the_bare_cabinet_mount()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("The cabinet is being filled.");
        html.Should().Contain("The games are being copied over from BoardGameGeek. Check back in a few minutes.");
        html.Should().Contain("class=\"cabinet-message cabinet-filling\"");
        html.IndexOf("cabinet-filling", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("id=\"cabinet\"", StringComparison.Ordinal));
        html.Should().MatchRegex("<div id=\"cabinet\" class=\"cabinet\"></div>");
        html.Should().NotContain("data-sample");
        html.Should().Contain("type=\"module\"");
        html.Should().NotContain("<nav");
        html.Should().NotContain("The cabinet is being built");
        html.Should().NotContain("Invented collection");
    }

    [Fact]
    public async Task Default_page_ignores_the_sample_parameter_entirely()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=65", TestContext.Current.CancellationToken);

        html.Should().Contain("The cabinet is being filled.");
        html.Should().NotContain("data-sample");
        html.Should().NotContain("Invented collection");
        html.Should().NotContain("<nav");
    }

    [Fact]
    public async Task Sample_page_shows_the_sixty_five_item_sample_with_its_status_line_and_no_being_filled_message()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=65", TestContext.Current.CancellationToken);

        html.Should().Contain("Invented collection of 65 items.");
        html.Should().Contain("data-sample=\"65\"");
        html.Should().NotContain("The cabinet is being filled.");
    }

    [Fact]
    public async Task Page_lists_one_sample_link_per_catalog_name_and_marks_the_current_one()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=65", TestContext.Current.CancellationToken);
        var hrefs = SampleLink().Matches(html).Select(match => match.Groups["name"].Value).ToList();

        html.Should().Contain("<nav aria-label=\"Collection to show\">");
        html.Should().NotContain("Sample collection size");
        hrefs.Should().Equal(SyntheticCollections.SampleNames);
        html.Should().Contain("<a href=\"/\">Synced</a>");
        html.Should().Contain("<a href=\"/?sample=65\" aria-current=\"page\">65</a>");
        CurrentLink().Matches(html).Should().ContainSingle();
        html.Should().Contain(">Edge cases</a>");
    }

    [Fact]
    public async Task Synced_view_with_the_prototype_on_lists_synced_first_marks_it_current_and_shows_no_status_line()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var navigation = html[html.IndexOf("<nav", StringComparison.Ordinal)..html.IndexOf("</nav>", StringComparison.Ordinal)];

        html.Should().Contain("<nav aria-label=\"Collection to show\">");
        navigation.Should().Contain("<a href=\"/\" aria-current=\"page\">Synced</a>");
        CurrentLink().Matches(navigation).Should().ContainSingle();
        navigation.IndexOf(">Synced</a>", StringComparison.Ordinal)
            .Should().BeLessThan(navigation.IndexOf("?sample=", StringComparison.Ordinal));
        html.Should().Contain("The cabinet is being filled.");
        html.Should().NotContain("Invented collection");
        html.Should().NotContain("data-sample");
    }

    [Fact]
    public async Task Chosen_sample_moves_the_current_marker_off_synced()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=12", TestContext.Current.CancellationToken);

        html.Should().Contain("<a href=\"/\">Synced</a>");
        html.Should().Contain("<a href=\"/?sample=12\" aria-current=\"page\">12</a>");
        CurrentLink().Matches(html).Should().ContainSingle();
    }

    [Fact]
    public async Task Page_with_the_prototype_off_never_renders_the_switcher_for_any_sample_value()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=12", TestContext.Current.CancellationToken);

        html.Should().NotContain("<nav");
        html.Should().NotContain("Synced");
        html.Should().NotContain("Collection to show");
        html.Should().NotContain("sample");
    }

    [Fact]
    public async Task Empty_sample_page_states_zero_items_and_carries_its_name()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=0", TestContext.Current.CancellationToken);

        html.Should().Contain("Invented collection of 0 items.");
        html.Should().Contain("data-sample=\"0\"");
    }

    [Fact]
    public async Task Single_item_sample_page_uses_the_singular_status_line()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/?sample=1", TestContext.Current.CancellationToken);

        html.Should().Contain("Invented collection of 1 item.");
        html.Should().NotContain("1 items");
    }

    [Fact]
    public async Task Unknown_sample_value_shows_the_synced_collection_and_is_never_echoed()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();
        var hostile = Uri.EscapeDataString("<script>alert(1)</script>");

        using var response = await client.GetAsync($"/?sample={hostile}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("data-sample");
        html.Should().Contain("The cabinet is being filled.");
        html.Should().NotContain("alert(1)");
        html.Should().NotContain("alert%281%29");
    }

    [Fact]
    public async Task Layout_endpoint_serves_the_synced_collection_when_the_prototype_is_off_whatever_sample_is_asked_for()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var withSample = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var without = await client.GetAsync("/cabinet/layout?profile=desktop", TestContext.Current.CancellationToken);

        withSample.StatusCode.Should().Be(HttpStatusCode.OK);
        withSample.Headers.ETag.Should().Be(without.Headers.ETag);
        withSample.Headers.ETag!.Tag.Should().EndWith("-collection-empty-desktop\"");
    }

    [Fact]
    public async Task Page_shows_the_script_free_message_and_the_version_footer()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().Contain("<noscript>");
        html.Should().Contain("The cabinet needs JavaScript to be drawn. Please turn it on and reload.");
        html.Should().MatchRegex("<footer>\\s*<p class=\"version\">Version [^<]+</p>\\s*<a class=\"bgg-credit\"");
    }

    [Fact]
    public void Host_with_an_invalid_prototype_setting_fails_to_start()
    {
        var start = () => new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Prototype:Enabled"] = "maybe" });

        start.Should().Throw<Exception>().Which.ToString().Should().Contain("Prototype:Enabled must be true or false.");
    }

    [Fact]
    public async Task Every_script_element_has_a_source_and_no_import_map_exists()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        ScriptWithoutSource().IsMatch(html).Should().BeFalse("every script is an external file");
        html.Should().NotContain("importmap");
        ScriptElement().Matches(html).Should().NotBeEmpty();
    }

    [GeneratedRegex("<a href=\"/\\?sample=(?<name>[^\"]+)\"")]
    private static partial Regex SampleLink();

    [GeneratedRegex("aria-current=\"page\"")]
    private static partial Regex CurrentLink();

    [GeneratedRegex("<link[^>]*rel=\"stylesheet\"[^>]*href=\"(?<href>[^\"]+)\"")]
    private static partial Regex StylesheetLink();

    [GeneratedRegex("<script[^>]*type=\"module\"[^>]*src=\"(?<src>[^\"]+)\"")]
    private static partial Regex ModuleScript();

    [GeneratedRegex("<[a-zA-Z][^>]*\\sstyle\\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex("<script(?![^>]*\\ssrc=)[^>]*>\\s*\\S", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScriptBody();

    [GeneratedRegex("<script(?![^>]*\\ssrc=)", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptWithoutSource();

    [GeneratedRegex("<script\\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptElement();
}
