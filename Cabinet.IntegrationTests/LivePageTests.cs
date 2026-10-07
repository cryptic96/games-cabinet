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

    private static async Task<string> GetScript(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript", path);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("<script type=\"module\" src=\"(?<src>[^\"]+)\"")]
    private static partial Regex PageModuleSource();
}
