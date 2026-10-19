using System.Net;
using System.Text.RegularExpressions;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Proves the scripts that keep an open page current are reachable from the page the visitor loads.</summary>
[Trait("Category", "Live")]
public partial class LivePageTests
{
    [Fact]
    public async Task The_module_graph_from_the_page_serves_the_live_script_as_javascript()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var entry = PageModuleSource().Match(html).Groups["src"].Value;
        var cabinet = await GetScript(client, entry);
        var live = await GetScript(client, "/js/live.js");

        cabinet.Should().Contain("./live.js");
        live.Should().Contain("./status.js");
    }

    [Fact]
    public async Task The_page_loads_exactly_one_vendored_classic_script_before_the_module_script()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var classic = ClassicLibraryScripts().Matches(html);
        var module = PageModuleSource().Match(html);

        classic.Should().ContainSingle();
        classic[0].Groups["src"].Value.Should().StartWith("/lib/signalr/signalr.min").And.EndWith(".js");
        module.Success.Should().BeTrue();
        classic[0].Index.Should().BeLessThan(module.Index);
        var dockHead = DockHeadScript().Match(html);
        dockHead.Success.Should().BeTrue("the filter dock's state is set by an external head script before the first paint");
        dockHead.Index.Should().BeLessThan(module.Index);
        ScriptElements().Matches(html).Should().HaveCount(3, "the vendored client and the filter dock's head script are the only scripts besides the page's own module");

        using (var head = await client.GetAsync(dockHead.Groups["src"].Value, TestContext.Current.CancellationToken))
        {
            head.StatusCode.Should().Be(HttpStatusCode.OK);
            head.ShouldHaveMediaType("text/javascript");
        }

        using var response = await client.GetAsync(classic[0].Groups["src"].Value, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("text/javascript");
    }

    private static async Task<string> GetScript(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        response.ShouldHaveMediaType("text/javascript", path);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("<script src=\"(?<src>/lib/[^\"]+)\"[^>]*></script>")]
    private static partial Regex ClassicLibraryScripts();

    [GeneratedRegex("<script src=\"(?<src>/js/filter-dock-head[^\"]*\\.js[^\"]*)\"[^>]*></script>")]
    private static partial Regex DockHeadScript();

    [GeneratedRegex("<script\\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptElements();

    [GeneratedRegex("<script type=\"module\" src=\"(?<src>[^\"]+)\"")]
    private static partial Regex PageModuleSource();
}
